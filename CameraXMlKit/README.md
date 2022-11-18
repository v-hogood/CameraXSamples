# CameraX-MLKit

This example uses CameraX's MlKitAnalyzer to perform QR Code scanning. For QR Codes that encode Urls, this app will prompt the user to open the Url in a broswer. This app can be adapted to handle other types of QR Code data.

The interesting part of the code is in `MainActivity.cs` in the `StartCamera()` function. There, we set up BarcodeScannerOptions to match on QR Codes. Then we call `CameraController.SetImageAnalysisAnalyzer` with an `MlKitAnalyzer` (available as of CameraX 1.2). We also pass in `CoordinateSystemViewReferenced` so that CameraX will handle the cordinates coming off of the camera sensor, making it easy to draw a box around the QR Code. Finally, we create a QrCodeDrawable, which is a class defined in this sample, extending View, for displaying an overlay on the QR Code and handling tap events on the QR Code.

You can open this project in Visual Studio to explore the code further, and to build and run the application on a test device.

## Command line options 

### Build

To build the app directly from the command line, run:
```sh
./msbuild -p:Configuration=Debug
```
