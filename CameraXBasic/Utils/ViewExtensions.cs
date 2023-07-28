using Android.Annotation;
using Android.OS;
using Android.Views;
using AndroidX.Core.View;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Object = Java.Lang.Object;

namespace CameraXBasic.Utils
{
    public static class ViewExtensions
    {
        // Milliseconds used for UI animations
        public const int AnimationFastMillis = 50;
        public const int AnimationSlowMillis = 100;

        //
        // Simulate a button click, including a small delay while it is being pressed to trigger the
        // appropriate animations.
        //
        public static void SimulateClick(this ImageButton button, long delay = AnimationFastMillis)
        {
            button.PerformClick();
            button.Pressed = true;
            button.Invalidate();
            button.PostDelayed(() =>
            {
                button.Invalidate();
                button.Pressed = false;
            }, delay);
        }

        // Pad this view with the insets provided by the device cutout (i.e. notch)
        [ TargetApi (Value = (int) BuildVersionCodes.P) ]
        public static void PadWithDisplayCutout(this View view)
        {
            // Apply padding using the display cutout designated "safe area"
            var c = view.RootWindowInsets?.DisplayCutout;
            if (c != null)
            {
                view.SetPadding(c.SafeInsetLeft, c.SafeInsetTop, c.SafeInsetRight, c.SafeInsetBottom);
            }

            // Set a listener for window insets since view.rootWindowInsets may not be ready yet
            view.SetOnApplyWindowInsetsListener(new OnApplyWindowInsetsListener());
        }

        private class OnApplyWindowInsetsListener : Object, View.IOnApplyWindowInsetsListener
        {
            public WindowInsets OnApplyWindowInsets(View view, WindowInsets insets)
            {
                var c = insets.DisplayCutout;
                if (c != null)
                {
                    view.SetPadding(c.SafeInsetLeft, c.SafeInsetTop, c.SafeInsetRight, c.SafeInsetBottom);
                }
                return insets;
            }
        }

        // Same as [AlertDialog.show] but setting immersive mode in the dialog's window
        public static void ShowImmersive(this AlertDialog dialog)
        {
            // Set the dialog to not focusable
            dialog.Window?.SetFlags(
                WindowManagerFlags.NotFocusable,
                WindowManagerFlags.NotFocusable);

            // Make sure that the dialog's window is in full screen
            dialog.Window?.HideSystemUI();

            // Show the dialog while still in immersive mode
            dialog.Show();

            // Set the dialog to focusable again
            dialog.Window?.ClearFlags(WindowManagerFlags.NotFocusable);
        }

        private static void HideSystemUI(this Window window)
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.R)
            {
                WindowCompat.SetDecorFitsSystemWindows(window, false);
                var controller = new WindowInsetsControllerCompat(window, window.DecorView);
                controller.Hide(WindowInsetsCompat.Type.SystemBars());
                controller.SystemBarsBehavior =
                    WindowInsetsControllerCompat.BehaviorShowTransientBarsBySwipe;
            }
            else
            {
                // For api level 29 and before, set deprecated systemUiVisibility to the combination of all
                // flags required to put activity into immersive mode.
                var fullscreenFlags =
                    SystemUiFlags.LowProfile |
                    SystemUiFlags.Fullscreen |
                    SystemUiFlags.LayoutStable |
                    SystemUiFlags.ImmersiveSticky |
                    SystemUiFlags.LayoutHideNavigation |
                    SystemUiFlags.HideNavigation;
#pragma warning disable CA1422
                window.DecorView.SystemUiVisibility = (StatusBarVisibility)fullscreenFlags;
#pragma warning restore CA1422
            }
        }
    }
}
