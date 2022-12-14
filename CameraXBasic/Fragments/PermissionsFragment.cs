using System.Linq;
using Android;
using Android.Content;
using Android.Content.PM;
using Android.Widget;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using AndroidX.Navigation;

namespace CameraXBasic.Fragments
{
    // The sole purpose of this fragment is to request permissions and, once granted, display the camera fragment to the user.
    [Android.App.Activity(Name = "com.android.example.cameraxbasic.fragments.PermissionsFragment")]
    public class PermissionsFragment : Fragment
    {
        private const int PermissionsRequestCode = 10;
        private static readonly string[] PermissionsRequired = { Manifest.Permission.Camera };

        public override void OnStart()
        {
            base.OnStart();

            if (!HasPermissions(RequireContext()))
            {
                // Request camera-related permissions
                RequestPermissions(PermissionsRequired, PermissionsRequestCode);
            }
            else
            {
                // If permissions have already been granted, proceed
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(Resource.Id.action_permissions_to_camera);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            if (requestCode == PermissionsRequestCode)
            {
                if (grantResults.FirstOrDefault() == Permission.Granted)
                {
                    // Take the user to the success fragment when permission is granted
                    Toast.MakeText(this.Context, "Permission request granted", ToastLength.Long).Show();
                    Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(Resource.Id.action_permissions_to_camera);
                }
                else
                {
                    Toast.MakeText(this.Context, "Permission request denied", ToastLength.Long).Show();
                }
            }
        }

        // Convenience method used to check if all permissions required by this app are granted
        public static bool HasPermissions(Context context)
        {
            return PermissionsRequired.All(x => ContextCompat.CheckSelfPermission(context, x) == Permission.Granted);
        }
    }
}
