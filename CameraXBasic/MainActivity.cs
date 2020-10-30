using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.LocalBroadcastManager.Content;

namespace CameraXBasic
{
    // Main entry point into our app. This app follows the single-activity pattern, and all
    // functionality is implemented in the form of fragments.
    [Activity(Name = "com.android.example.cameraxbasic.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        public const string KeyEventAction = "key_event_action";
        public const string KeyEventExtra = "key_event_extra";
        private const long ImmersiveFlagTimeout = 500L;

        private FrameLayout container;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            container = FindViewById(Resource.Id.fragment_container) as FrameLayout;
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Before setting full screen flags, we must wait a bit to let UI settle; otherwise, we may
            // be trying to set app to immersive mode before it's ready and the flags do not stick
            container.PostDelayed(() => container.SystemUiVisibility =
                (Android.Views.StatusBarVisibility) Android.Views.SystemUiFlags.Fullscreen,
                ImmersiveFlagTimeout);
        }

        // When key down event is triggered, relay it via local broadcast so fragments can handle it
        public override bool OnKeyDown(Keycode keyCode, KeyEvent msg)
        {
            if (keyCode == Keycode.VolumeDown)
            {
                var intent = new Intent(KeyEventAction);
                intent.PutExtra(KeyEventExtra, (int) keyCode);
                LocalBroadcastManager.GetInstance(this).SendBroadcast(intent);
                return true;
            }
            else
            {
                return base.OnKeyDown(keyCode, msg);
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
        }

        // Use external media if it is available, our app's file directory otherwise
        public static Java.IO.File GetOutputDirectory(Context context)
        {
            var appContext = context.ApplicationContext;
            var mediaDir = new Java.IO.File(context.GetExternalMediaDirs().FirstOrDefault(),
                appContext.GetString(Resource.String.app_name));
            mediaDir?.Mkdirs();
            return mediaDir != null && mediaDir.Exists() ?
                mediaDir : appContext.FilesDir;
        }
    }
}
