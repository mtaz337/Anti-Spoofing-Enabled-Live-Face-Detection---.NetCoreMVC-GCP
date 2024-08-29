using Microsoft.AspNetCore.Mvc;
using Google.Cloud.VideoIntelligence.V1;
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace LiveFaceDetectionAPP.Controllers
{
    public class LivenessController : Controller
    {
        private readonly VideoIntelligenceServiceClient _videoIntelligenceService;
        private readonly ILogger<LivenessController> _logger;
        private readonly HashSet<string> _allowedWearables;

        public LivenessController(ILogger<LivenessController> logger)
        {
            _videoIntelligenceService = VideoIntelligenceServiceClient.Create();
            _logger = logger;
            _allowedWearables = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "headphones", "hat", "cap", "glasses", "sunglasses", "earrings", "necklace", "bracelet", "watch"
                // Add more allowed wearables as needed
            };
        }

        [HttpGet]
        public IActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> CheckLiveness(IFormFile videoFile, IFormFile screenshotFile)
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

                    var (isValid, message) = IsLivenessValid(result);

                    // Save the screenshot
                    await SaveScreenshot(screenshotFile, isValid);

                    return Json(new { success = isValid, message = message });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred during the liveness check.");
                return Json(new { success = false, message = $"An error occurred during the liveness check: {ex.Message}" });
            }
        }

        private async Task SaveScreenshot(IFormFile screenshotFile, bool isValid)
        {
            if (screenshotFile != null)
            {
                var folderName = isValid ? "Successful_Detections" : "Unsuccessful_Detections";
                var folderPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", folderName);
                Directory.CreateDirectory(folderPath);  // Ensure the directory exists

                var fileName = $"screenshot_{DateTime.Now:yyyyMMddHHmmss}.png";
                var filePath = Path.Combine(folderPath, fileName);

                using (var fileStream = new FileStream(filePath, FileMode.Create))
                {
                    await screenshotFile.CopyToAsync(fileStream);
                }
            }
        }

        private (bool isValid, string message) IsLivenessValid(AnnotateVideoResponse result)
        {
            try
            {
                _logger.LogInformation($"AnnotationResults count: {result.AnnotationResults.Count}");
                if (result.AnnotationResults.Count > 0)
                {
                    _logger.LogInformation($"FaceDetectionAnnotations count: {result.AnnotationResults[0].FaceDetectionAnnotations.Count}");
                    _logger.LogInformation($"ObjectAnnotations count: {result.AnnotationResults[0].ObjectAnnotations.Count}");
                }

                var faceDetections = result.AnnotationResults[0].FaceDetectionAnnotations;
                var objectTrackings = result.AnnotationResults[0].ObjectAnnotations;

                if (faceDetections.Count == 0)
                {
                    _logger.LogWarning("No faces detected in the video.");
                    return (false, "No face detected. Liveness check failed.");
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

                var suspiciousObjects = objectTrackings
                    .Where(obj => !_allowedWearables.Contains(obj.Entity.Description))
                    .Where(obj =>
                        obj.Entity.Description.ToLower().Contains("phone") ||
                        obj.Entity.Description.ToLower().Contains("screen") ||
                        obj.Entity.Description.ToLower().Contains("tablet") ||
                        obj.Entity.Description.ToLower().Contains("laptop") ||
                        obj.Entity.Description.ToLower().Contains("paper"))
                    .ToList();

                if (suspiciousObjects.Any())
                {
                    _logger.LogInformation($"Suspicious objects detected: {string.Join(", ", suspiciousObjects.Select(o => o.Entity.Description))}");
                    return (false, "Malicious attempt detected. Liveness check failed.");
                }

                if (!consistentFacePresence || !naturalMovements)
                {
                    return (false, "Inconsistent face presence or unnatural movements detected. Liveness check failed.");
                }

                return (true, "Liveness check passed.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "An error occurred while validating liveness.");
                return (false, "An error occurred during liveness validation.");
            }
        }
    }
}