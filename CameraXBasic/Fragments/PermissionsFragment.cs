using System;
using System.Linq;
using Android;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using AndroidX.Navigation;
using Java.Util;
using Java.Util.Functions;
using Object = Java.Lang.Object;

namespace CameraXBasic.Fragments
{
    //
    // The sole purpose of this fragment is to request permissions and, once granted, display the
    // camera fragment to the user.
    //
    [Android.App.Activity(Name = "com.android.example.cameraxbasic.fragments.PermissionsFragment")]
    public class PermissionsFragment : Fragment,
        IActivityResultCallback,
        IBiConsumer
    {
        private static string[] PermissionsRequired = { Manifest.Permission.Camera };

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
                // If permissions have already been granted, proceed
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(
                    Resource.Id.action_permissions_to_camera);
            }
        }

        private bool permissionGranted;

        public void OnActivityResult(Object result)
        {
            var permissions = result as IMap;

            // Handle Permission granted/rejected
            permissionGranted = true;
            permissions.ForEach(this);
            if (!permissionGranted)
            {
                Toast.MakeText(Context, "Permission request denied", ToastLength.Long).Show();
            }
            else
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(
                    Resource.Id.action_permissions_to_camera);
            }
        }

        public void Accept(Object t, Object u)
        {
            if (PermissionsRequired.Contains((string)t) && (bool)u == false)
                permissionGranted = false;
        }

        // Convenience method used to check if all permissions required by this app are granted
        public static bool HasPermissions(Context context)
        {
            return PermissionsRequired.All(it =>
                ContextCompat.CheckSelfPermission(context, it) == Permission.Granted);
        }
    }
}
