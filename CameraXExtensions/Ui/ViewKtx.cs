using Android.Views;
using AndroidX.Core.View;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;
using Object = Java.Lang.Object;

namespace CameraXExtensions
{
    public static class ViewKtx
    {
        //
        // Apply the action when this view is attached to the window and has been measured.
        // If the view is already attached and measured then the action is immediately invoked.
        //
        // @param action The action to apply when the view is laid out
        //
        public static void DoOnLaidOut(this View view, Action action)
        {
#pragma warning disable CS0618
            if (view.IsAttachedToWindow && ViewCompat.IsLaidOut(view))
#pragma warning restore CS0618
            {
                action();
            }
            else
            {
                view.ViewTreeObserver.AddOnGlobalLayoutListener(
                    new OnGlobalLayoutListener(view, action));
            }
        }
    }

    public class OnGlobalLayoutListener : Object,
        ViewTreeObserver.IOnGlobalLayoutListener
    {
        View view;
        Action action;
        public OnGlobalLayoutListener(View view, Action action) =>
            (this.view, this.action) = (view, action);
        public void OnGlobalLayout()
        {
            view.ViewTreeObserver.RemoveOnGlobalLayoutListener(this);
            action();
        }
    }

    public static class BuildersKtx
    {
        public class Function2 : Object, IFunction2
        {
            Action action;
            public Function2(Action action) => this.action = action;
            public Object Invoke(Object p0, Object p1)
            {
                action();
                return null;
            }
        }

        public static IJob Launch(this ICoroutineScope scope, Action action) =>
            BuildersKt.Launch(scope, EmptyCoroutineContext.Instance, CoroutineStart.Default, new Function2(action));
    }
}
