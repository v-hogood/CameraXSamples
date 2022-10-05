using AndroidX.Camera.Extensions;
using Java.Lang;

namespace CameraXExtensions
{
    //
    // User initiated actions related to camera operations.
    //
    public class CameraUiAction : Object
    {
        public sealed class RequestPermissionClick : CameraUiAction { };
        public sealed class SwitchCameraClick : CameraUiAction { };
        public sealed class ShutterButtonClick : CameraUiAction { };
        public sealed class ClosePhotoPreviewClick : CameraUiAction { };
        public sealed class SelectCameraExtension : CameraUiAction
        {
            public SelectCameraExtension(int extension) =>
                Extension = extension;
            public int Extension;
        };
    }
}
