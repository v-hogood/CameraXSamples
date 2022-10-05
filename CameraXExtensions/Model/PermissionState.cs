using Java.Lang;

namespace CameraXExtensions
{
    public class PermissionState : Object
    {
        sealed public class Granted : PermissionState { }
        sealed public class Denied : PermissionState
        {
            public Denied(bool shouldShowRationale) =>
                ShouldShowRationale = shouldShowRationale;
            public bool ShouldShowRationale;
        }
    }
}
