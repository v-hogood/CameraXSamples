using Android.App;
using Android.OS;
using Android.Runtime;
using Android.Widget;
using AndroidX.AppCompat.App;

namespace CameraXBasic
{
// Main entry point into our app. This app follows the single-activity pattern, and all
// functionality is implemented in the form of fragments.
    [Activity(Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
private FrameLayout container;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            container = findViewById(Resource.Id.fragment_container);
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Before setting full screen flags, we must wait a bit to let UI settle; otherwise, we may
            // be trying to set app to immersive mode before it's ready and the flags do not stick
            container.po
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }
    }
}
