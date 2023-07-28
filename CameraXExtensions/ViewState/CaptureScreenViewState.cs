using Object = Java.Lang.Object;

namespace CameraXExtensions
{
    //
    // Capture Screen is the top level view state. A capture screen contains a camera preview screen
    // and a post capture screen.
    //
    public class CaptureScreenViewState : Object
    {
        public CaptureScreenViewState() { }
        public CaptureScreenViewState(CaptureScreenViewState captureScreenViewState)
        {
            this.cameraPreviewScreenViewState = captureScreenViewState.cameraPreviewScreenViewState;
            this.postCaptureScreenViewState = captureScreenViewState.postCaptureScreenViewState;
        }

        public CameraPreviewScreenViewState cameraPreviewScreenViewState = new CameraPreviewScreenViewState();
        public PostCaptureScreenViewState postCaptureScreenViewState = new PostCaptureScreenViewState.HiddenViewState();

        public CaptureScreenViewState UpdateCameraScreen(Func<CameraPreviewScreenViewState, CameraPreviewScreenViewState> block)
        {
            return new CaptureScreenViewState(this)
            {
                cameraPreviewScreenViewState = block(this.cameraPreviewScreenViewState)
            };
        }

        public CaptureScreenViewState UpdatePostCaptureScreen(Func<PostCaptureScreenViewState> block)
        {
            return new CaptureScreenViewState(this)
            {
                postCaptureScreenViewState = block()
            };
        }
    }
}
