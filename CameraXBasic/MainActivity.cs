using System.Linq;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.OS;
using Android.Runtime;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
using AndroidX.LocalBroadcastManager.Content;
using Java.IO;

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

            // Set our view from the "main" layout resource
            SetContentView(Resource.Layout.activity_main);
            container = FindViewById(Resource.Id.fragment_container) as FrameLayout;
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Before setting full screen flags, we must wait a bit to let UI settle; otherwise, we may
            // be trying to set app to immersive mode before it's ready and the flags do not stick
            container.PostDelayed(() => HideSystemUI(),
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

        public override void OnBackPressed()
        {
            if (Build.VERSION.SdkInt == BuildVersionCodes.P)
            {
                // Workaround for Android Q memory leak issue in IRequestFinishCallback$Stub.
                // (https://issuetracker.google.com/issues/139738913)
                FinishAfterTransition();
            }
            else
            {
                base.OnBackPressed();
            }
        }

        // Use external media if it is available, our app's file directory otherwise
        public static File GetOutputDirectory(Context context)
        {
            var appContext = context.ApplicationContext;
            var mediaDir = new File(context.GetExternalMediaDirs().FirstOrDefault(),
                appContext.GetString(Resource.String.app_name));
            mediaDir?.Mkdirs();
            return mediaDir != null && mediaDir.Exists() ?
                mediaDir : appContext.FilesDir;
        }

        private void HideSystemUI()
        {
            WindowCompat.SetDecorFitsSystemWindows(Window, false);
            WindowInsetsControllerCompat controller = new WindowInsetsControllerCompat(Window, container);
            controller.Hide(WindowInsetsCompat.Type.SystemBars());
            controller.SystemBarsBehavior = WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
        }
    }
}
