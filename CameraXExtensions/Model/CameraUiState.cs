using Android.Hardware.Camera2;
using AndroidX.Camera.Core;
using Exception = Java.Lang.Exception;
using Object = Java.Lang.Object;

namespace CameraXExtensions
{
    //
    // Defines the current UI state of the camera during pre-capture.
    // The state encapsulates the available camera extensions, the available camera lenses to toggle,
    // the current camera lens, the current extension mode, and the state of the camera.
    //
    public class CameraUiState : Object
    {
        public CameraUiState() {}
        public CameraUiState(CameraUiState cameraUiState)
        {
            CameraState = cameraUiState.CameraState;
            AvailableExtensions = cameraUiState.AvailableExtensions;
            AvailableCameraLens = cameraUiState.AvailableCameraLens;
            CameraLens = cameraUiState.CameraLens;
            ExtensionMode = cameraUiState.ExtensionMode;
        }
        public CameraState CameraState = CameraState.NotReady;
        public List<int> AvailableExtensions = new List<int>();
        public List<int> AvailableCameraLens = new List<int>(new int[] { (int)LensFacing.Back });
        public int CameraLens = (int)LensFacing.Back;
        public int ExtensionMode = AndroidX.Camera.Extensions.ExtensionMode.None;
    }

    //
    // Defines the current state of the camera.
    //
    public enum CameraState
    {
        //
        // Camera hasn't been initialized.
        //
        NotReady,

        //
        // Camera is open and presenting a preview stream.
        //
        Ready,

        //
        // Camera is initialized but the preview has been stopped.
        //
        PreviewStopped
    }

    //
    // Defines the various states during post-capture.
    //
    public abstract class CaptureState : Object
    {
        //
        //  Capture capability isn't ready. This could be because the camera isn't initialized, or the
        // camera is changing the lens, or the camera is switching to a new extension mode.
        //
        sealed public class CaptureNotReady : CaptureState { };

        //
        // Capture capability is ready.
        //
        sealed public class CaptureReady : CaptureState { };

        //
        // Capture process has started.
        //
        sealed public class CaptureStarted : CaptureState { };

        //
        // Capture completed successfully.
        //
        sealed public class CaptureFinished : CaptureState
        {
            public CaptureFinished(ImageCapture.OutputFileResults outputResults) =>
                OutputResults = outputResults;
            public ImageCapture.OutputFileResults OutputResults;
        };

        //
        // Capture failed with an error.
        //
        sealed public class CaptureFailed : CaptureState
        {
            public CaptureFailed(Exception exception) =>
                this.exception = exception;
            Exception exception;
        }
    }
}
