using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.Core.Content;
using AndroidX.Navigation;
using Java.Util;
using Java.Util.Functions;
using Fragment = AndroidX.Fragment.App.Fragment;
using Object = Java.Lang.Object;

namespace CameraXVideo
{
    //
    // This [Fragment] requests permissions and, once granted, it will navigate to the next fragment
    //
    [Android.App.Activity(Name = "com.android.example.cameraxvideo.fragments.PermissionsFragment")]
    class PermissionsFragment : Fragment,
        IActivityResultCallback,
        IBiConsumer
    {
        private string[] PermissionsRequired = new string[] {
            Manifest.Permission.Camera,
            Manifest.Permission.RecordAudio };

        private ActivityResultLauncher activityResultLauncher;

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // add the storage access permission request for Android 9 and below.
            if (Build.VERSION.SdkInt <= BuildVersionCodes.P)
            {
                var permissionList = PermissionsRequired.ToList();
                permissionList.Add(Manifest.Permission.WriteExternalStorage);
                PermissionsRequired = permissionList.ToArray();
            }

            activityResultLauncher =
                RegisterForActivityResult(new ActivityResultContracts.RequestMultiplePermissions(), this);
        }

        public override void OnStart()
        {
            base.OnStart();

            if (!HasPermissions(RequireContext()))
            {
                // Request camera-related permissions
                activityResultLauncher.Launch(PermissionsRequired);
            }
            else
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(
                    Resource.Id.action_permissions_to_capture);
            }
        }

        public override View OnCreateView(
            LayoutInflater inflater,
            ViewGroup container,
            Bundle savedInstanceState
        )
        {
            return inflater.Inflate(Resource.Layout.fragment_permission, container, false);
        }

        // Convenience method used to check if all permissions required by this app are granted */
        private bool HasPermissions(Context context)
        {
            return PermissionsRequired.All(it => ContextCompat.CheckSelfPermission(context, it) == Permission.Granted);
        }

        private bool permissionGranted;

        public void OnActivityResult(Object result)
        {
            var permissions = result as IMap;

            // Handle Permission granted/rejected
            permissionGranted = true;
            permissions.ForEach(this);

            if (permissionGranted && !permissions.IsEmpty)
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(
                    Resource.Id.action_permissions_to_capture);
            }
            if (!permissionGranted)
                Toast.MakeText(Context, "Permission request denied", ToastLength.Long).Show();
        }

        public void Accept(Object t, Object u)
        {
            if (PermissionsRequired.Contains((string)t) && (bool)u == false)
                permissionGranted = false;
        }
    }
}
