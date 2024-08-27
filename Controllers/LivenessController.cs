using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Google.Cloud.VideoIntelligence.V1;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace YourNamespace.Controllers
{
    public class LivenessController : Controller
    {
        private readonly VideoIntelligenceServiceClient _videoIntelligenceService;
        private readonly ILogger<LivenessController> _logger;

        public LivenessController(ILogger<LivenessController> logger)
        {
            _videoIntelligenceService = VideoIntelligenceServiceClient.Create();
            _logger = logger;
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckLiveness(IFormFile videoFile)
        {
            if (videoFile == null || videoFile.Length == 0)
            {
                return BadRequest("No video file uploaded.");
            }

            try
            {
                using (var ms = new MemoryStream())
                {
                    await videoFile.CopyToAsync(ms);
                    var videoBytes = ms.ToArray();

                    var request = new AnnotateVideoRequest
                    {
                        InputContent = Google.Protobuf.ByteString.CopyFrom(videoBytes),
                        Features = { Feature.FaceDetection, Feature.ObjectTracking },
                    };

                    var operation = await _videoIntelligenceService.AnnotateVideoAsync(request);
                    var response = await operation.PollUntilCompletedAsync();

                    var result = response.Result;

                    if (IsLivenessValid(result))
                    {
                        return Json(new { success = true, message = "Liveness check passed." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Malicious attempt detected. Liveness check failed." });
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the liveness check.");
                return Json(new { success = false, message = $"An error occurred during the liveness check: {ex.Message}" });
            }
        }

        private bool IsLivenessValid(AnnotateVideoResponse result)
        {
            try
            {
                var faceDetections = result.AnnotationResults[0].FaceDetectionAnnotations;
                var objectTrackings = result.AnnotationResults[0].ObjectAnnotations;

                if (faceDetections.Count == 0)
                {
                    _logger.LogWarning("No faces detected in the video.");
                    return false; // No face detected
                }

                bool consistentFacePresence = true;
                bool naturalMovements = true;

                foreach (var faceDetection in faceDetections)
                {
                    if (faceDetection.Tracks.Count == 0)
                    {
                        _logger.LogWarning("No tracks found for a face detection.");
                        continue;
                    }

                    var totalDuration = faceDetection.Tracks.Max(t => t.Segment.EndTimeOffset.Seconds) -
                                        faceDetection.Tracks.Min(t => t.Segment.StartTimeOffset.Seconds);

                    var faceTrackingDuration = faceDetection.Tracks.Sum(t =>
                        t.Segment.EndTimeOffset.Seconds - t.Segment.StartTimeOffset.Seconds);

                    if (faceTrackingDuration < (totalDuration * 0.8))
                    {
                        _logger.LogInformation("Face not consistently present.");
                        consistentFacePresence = false;
                        break;
                    }

                    if (faceDetection.Tracks[0].TimestampedObjects.Count < 2)
                    {
                        _logger.LogWarning("Not enough timestamped objects to check for natural movements.");
                        continue;
                    }

                    var prevBoundingBox = faceDetection.Tracks[0].TimestampedObjects[0].NormalizedBoundingBox;
                    for (int i = 1; i < faceDetection.Tracks[0].TimestampedObjects.Count; i++)
                    {
                        var currentBoundingBox = faceDetection.Tracks[0].TimestampedObjects[i].NormalizedBoundingBox;
                        if (Math.Abs(currentBoundingBox.Left - prevBoundingBox.Left) > 0.1 ||
                            Math.Abs(currentBoundingBox.Top - prevBoundingBox.Top) > 0.1)
                        {
                            _logger.LogInformation("Unnatural movements detected.");
                            naturalMovements = false;
                            break;
                        }
                        prevBoundingBox = currentBoundingBox;
                    }

                    if (!naturalMovements)
                    {
                        break;
                    }
                }

                bool suspiciousObjectDetected = objectTrackings.Any(obj =>
                    obj.Entity.Description.ToLower().Contains("phone") ||
                    obj.Entity.Description.ToLower().Contains("screen"));

                if (suspiciousObjectDetected)
                {
                    _logger.LogInformation("Suspicious object (phone or screen) detected.");
                }

                return consistentFacePresence && naturalMovements && !suspiciousObjectDetected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while validating liveness.");
                return false;
            }
        }
    }
}
