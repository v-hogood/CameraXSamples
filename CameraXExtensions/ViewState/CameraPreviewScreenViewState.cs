using Android.Graphics;
using static CameraXExtensions.CameraPreviewScreenViewState;

namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}

namespace CameraXExtensions
{
    //
    // Represents the camera preview screen view state. The camera preview screen shows camera controls
    // and the camera preview.
    //
    public record CameraPreviewScreenViewState(
        ShutterButtonViewState shutterButtonViewState,
        SwitchLensButtonViewState switchLensButtonViewState,
        CameraExtensionSelectorViewState extensionsSelectorViewState,
        ProcessProgressIndicatorViewState processProgressViewState,
        PostviewViewState postviewViewState)
    {
        public CameraPreviewScreenViewState() :
            this(new ShutterButtonViewState(), new SwitchLensButtonViewState(), new CameraExtensionSelectorViewState(),
                 new ProcessProgressIndicatorViewState(), new PostviewViewState())
        { }

        public CameraPreviewScreenViewState HideCameraControls() =>
            this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isVisible = false },
                switchLensButtonViewState = this.switchLensButtonViewState with { isVisible = false },
                extensionsSelectorViewState = this.extensionsSelectorViewState with { isVisible = false }
            };

        public CameraPreviewScreenViewState ShowCameraControls() =>
            this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isVisible = true },
                switchLensButtonViewState = this.switchLensButtonViewState with { isVisible = true },
                extensionsSelectorViewState = this.extensionsSelectorViewState with { isVisible = true }
            };

        public CameraPreviewScreenViewState EnableCameraShutter(bool isEnabled) =>
            this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isEnabled = isEnabled }
            };

        public CameraPreviewScreenViewState EnableSwitchLens(bool isEnabled) =>
            this with
            {
                switchLensButtonViewState = this.switchLensButtonViewState with { isEnabled = isEnabled }
            };

        public CameraPreviewScreenViewState SetAvailableExtensions(List<CameraExtensionItem> extensions) =>
            this with
            {
                extensionsSelectorViewState = this.extensionsSelectorViewState with { extensions = extensions }
            };

        public CameraPreviewScreenViewState ShowPostview(Bitmap bitmap) =>
            this with
            {
                postviewViewState = this.postviewViewState with { isVisible = true, bitmap = bitmap },
            };

        public CameraPreviewScreenViewState HidePostview() =>
            this with
            {
                postviewViewState = new PostviewViewState()
            };

        public CameraPreviewScreenViewState ShowProcessProgressViewState(int progress) =>
            this with
            {
                processProgressViewState = new ProcessProgressIndicatorViewState(isVisible: true, progress: progress)
            };

        public CameraPreviewScreenViewState HideProcessProgressViewState()
        {
            return this with
            {
                processProgressViewState = new ProcessProgressIndicatorViewState()
            };
        }

        public record CameraExtensionSelectorViewState(
            bool isVisible,
            List<CameraExtensionItem> extensions)
        {
            public CameraExtensionSelectorViewState() :
                this(false, new List<CameraExtensionItem>())
            { }
        }

        public record ShutterButtonViewState(
            bool isVisible = false,
            bool isEnabled = false
        );

        public record SwitchLensButtonViewState(
            bool isVisible = false,
            bool isEnabled = false
        );

        public record ProcessProgressIndicatorViewState(
            bool isVisible = false,
            int progress = 0
        );

        public record PostviewViewState(
            bool isVisible = false,
            Bitmap bitmap = null
        );
    }
}
