using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.View;

namespace CameraXVideo
{
    [Activity(Name = "com.android.example.cameraxvideo.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity
    {
        private FrameLayout container;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            SetContentView(Resource.Layout.activity_main);
            container = FindViewById(Resource.Id.fragment_container) as FrameLayout;

            // Fix the screen orientation for this sample to focus on cameraX API
            // rather than UI
            RequestedOrientation = ScreenOrientation.Portrait;
        }

        protected override void OnResume()
        {
            base.OnResume();
            // Before setting full screen flags, we must wait a bit to let UI settle; otherwise, we may
            // be trying to set app to immersive mode before it's ready and the flags do not stick
            container.PostDelayed(() => WindowCompat.SetDecorFitsSystemWindows(Window, false),
                ImmersiveFlagTimeout);
        }

        // Combination of all flags required to put activity into immersive mode
        private const int FlagsFullscreen =
            (int)SystemUiFlags.LowProfile |
            (int)SystemUiFlags.Fullscreen |
            (int)SystemUiFlags.LayoutStable |
            (int)SystemUiFlags.ImmersiveSticky;

        // Milliseconds used for UI animations
        public const long AnimationFastMillis = 50L;
        public const long AnimationSlowMillis = 100L;
        private const long ImmersiveFlagTimeout = 500L;
    }
}
