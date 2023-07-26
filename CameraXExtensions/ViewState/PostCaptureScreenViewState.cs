using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace CameraXExtensions
{
    //
    // Represents the post capture screen view state. This can be either visible with a uri for the
    // photo captured or hidden.
    //
    public class PostCaptureScreenViewState : Object
    {
        sealed public class HiddenViewState : PostCaptureScreenViewState { }

        sealed public class VisibleViewState : PostCaptureScreenViewState
        {
            public Uri uri;
        }
    }
}
