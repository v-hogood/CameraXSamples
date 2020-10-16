using Android.Support.V4.View;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;

namespace CameraXBasic.Utils
{
    public static class ViewExtensions
    {
        // Combination of all flags required to put activity into immersive mode
        private const SystemUiFlags FullscreenFlags = SystemUiFlags.LowProfile | SystemUiFlags.Fullscreen | SystemUiFlags.LayoutStable | SystemUiFlags.ImmersiveSticky | SystemUiFlags.LayoutHideNavigation | SystemUiFlags.HideNavigation;

        // Milliseconds used for UI animations
        private const int AnimationFastMillis = 50;
        private const int AnimationSlowMillis = 100;

        // Simulate a button click, including a small delay while it is being pressed to trigger the appropriate animations.
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
        public static void PadWithDisplayCutout(this View view)
        {
            // Apply padding using the display cutout designated "safe area"
            var c = view.RootWindowInsets.DisplayCutout;
            view.SetPadding(c.SafeInsetLeft, c.SafeInsetTop, c.SafeInsetRight, c.SafeInsetBottom);

            // Set a listener for window insets since view.rootWindowInsets may not be ready yet
            // TODO: Error on the next line: CS1503 Argument 1: cannot convert from 'CameraXBasic.ViewExtensions.OnApplyWindowInsetsListener' to 'Android.Views.View.IOnApplyWindowInsetsListener'
            //view.SetOnApplyWindowInsetsListener(new OnApplyWindowInsetsListener());
        }

        // Same as [AlertDialog.show] but setting immersive mode in the dialog's window
        public static void ShowImmersive(this AlertDialog dialog)
        {
            // Set the dialog to not focusable
            dialog.Window.SetFlags(WindowManagerFlags.NotFocusable, WindowManagerFlags.NotFocusable);

            // Make sure that the dialog's window is in full screen
            // TODO: The next line fails because of https://github.com/xamarin/xamarin-android/issues/4290
            //dialog.Window.DecorView.SystemUiVisibility = FullscreenFlags;

            // Show the dialog while still in immersive mode
            dialog.Show();

            // Set the dialog to focusable again
            dialog.Window.ClearFlags(WindowManagerFlags.NotFocusable);
        }

        private class OnApplyWindowInsetsListener : Java.Lang.Object, IOnApplyWindowInsetsListener
        {
            public WindowInsetsCompat OnApplyWindowInsets(View view, WindowInsetsCompat insets)
            {
                var c = insets.DisplayCutout;
                view.SetPadding(c.SafeInsetLeft, c.SafeInsetTop, c.SafeInsetRight, c.SafeInsetBottom);
                return insets;
            }
        }
    }
}
