using System.Collections.Generic;
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
        CameraExtensionSelectorViewState extensionsSelectorViewState)
    {
        public CameraPreviewScreenViewState() :
            this(new ShutterButtonViewState(), new SwitchLensButtonViewState(), new CameraExtensionSelectorViewState())
        { }

        public CameraPreviewScreenViewState HideCameraControls()
        {
            return this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isVisible = false },
                switchLensButtonViewState = this.switchLensButtonViewState with { isVisible = false },
                extensionsSelectorViewState = this.extensionsSelectorViewState with { isVisible = false }
            };
        }

        public CameraPreviewScreenViewState ShowCameraControls()
        {
            return this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isVisible = true },
                switchLensButtonViewState = this.switchLensButtonViewState with { isVisible = true },
                extensionsSelectorViewState = this.extensionsSelectorViewState with { isVisible = true }
            };
        }

        public CameraPreviewScreenViewState EnableCameraShutter(bool isEnabled)
        {
            return this with
            {
                shutterButtonViewState = this.shutterButtonViewState with { isEnabled = isEnabled }
            };
        }

        public CameraPreviewScreenViewState EnableSwitchLens(bool isEnabled)
        {
            return this with
            {
                switchLensButtonViewState = this.switchLensButtonViewState with { isEnabled = isEnabled }
            };
        }

        public CameraPreviewScreenViewState SetAvailableExtensions(List<CameraExtensionItem> extensions)
        {
            return this with
            {
                extensionsSelectorViewState = this.extensionsSelectorViewState with { extensions = extensions }
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
    }
}
