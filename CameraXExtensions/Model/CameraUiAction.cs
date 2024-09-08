using AndroidX.Camera.Core;
using Object = Java.Lang.Object;

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
        public sealed class ProcessProgressComplete : CameraUiAction { };
        public sealed class SelectCameraExtension : CameraUiAction
            { public int Extension; };
        public sealed class Focus : CameraUiAction
            { public MeteringPoint meteringPoint; };
        public sealed class Scale : CameraUiAction
            { public float scaleFactor; };
    }
}
