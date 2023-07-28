using Android.Content;
using Android.OS;
using Android.Views;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;
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
#pragma warning disable 0618
                LocalBroadcastManager.GetInstance(this).SendBroadcast(intent);
#pragma warning restore 0618
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
#pragma warning disable CA1422
                base.OnBackPressed();
#pragma warning restore CA1422
            }
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
