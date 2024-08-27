**Anti-Spoofing Enabled Live Face Detection-.NetCoreMVC-GPC :**

This project implements a liveness detection system using .NET Core MVC and Google Cloud's Video Intelligence API. The application ensures that the person interacting with the system is live and not using a spoofed video or image, enhancing security against spoofing attacks.

**Features**
Live Face Detection: Capture live video from the user's webcam to detect and validate face presence.
Anti-Spoofing: Identify suspicious objects like phones or screens to prevent spoofing attempts.
Real-time Feedback: Display real-time processing status and final results to the user.

**Technology Stack**
.NET Core MVC for the backend framework.
Google Cloud Video Intelligence API for video analysis and face detection.
JavaScript with RecordRTC for handling video recording in the browser.

**Getting Started**

**Prerequisites**
.NET Core SDK 6.0+
A Google Cloud account with the Video Intelligence API enabled.
Credentials JSON file for Google Cloud.

Setup
1. Clone the repository:
git clone https://github.com/YourUsername/Anti-Spoofing-Enabled-Live-Face-Detection-.NetCoreMVC-GPC.git

2. Navigate to the project directory:
cd Anti-Spoofing-Enabled-Live-Face-Detection-.NetCoreMVC-GPC

3.Set up the Google Cloud credentials:
Place your client_secret.json in a secure location.
In Program.cs, set the environment variable for Google Cloud credentials:

Environment.SetEnvironmentVariable("GOOGLE_APPLICATION_CREDENTIALS", @"path\to\client_secret.json");

4. Restore the NuGet packages:
dotnet restore

5. Run the application
dotnet run

**Usage**
Open the application in your browser.
Navigate to the liveness check page.
Click "Start Recording" to capture live video.
The system will process the video and display the result (either a success message or a warning).

**Repository Structure**

Controllers/
LivenessController.cs: Handles video file processing and liveness detection.

Views/Liveness/
Index.cshtml: View for interacting with the liveness detection system.

wwwroot/: Static files like JavaScript and CSS.

**License**
This project is licensed under the MIT License - see the LICENSE file for details.

**Acknowledgements**
Google Cloud Video Intelligence API for providing the core video analysis capabilities.
RecordRTC for handling video recording in the browser.

