using System.Runtime.InteropServices;
using Android.Runtime;
using Android.Views;
using AndroidX.Camera.Core;
using AndroidX.Core.View;
using AndroidX.Lifecycle;
using Java.Interop;
using Java.Lang;
using Java.Util.Concurrent;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;

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
        public static void DoOnLaidOut(this View view, System.Action action)
        {
            if (view.IsAttachedToWindow && ViewCompat.IsLaidOut(view))
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

    public class OnGlobalLayoutListener : Java.Lang.Object,
        ViewTreeObserver.IOnGlobalLayoutListener
    {
        View view;
        System.Action action;
        public OnGlobalLayoutListener(View view, System.Action action) =>
            (this.view, this.action) = (view, action);
        public void OnGlobalLayout()
        {
            view.ViewTreeObserver.RemoveOnGlobalLayoutListener(this);
            action();
        }
    }

    public static class BuildersKtx
    {
        static System.IntPtr class_ref = JNIEnv.FindClass("kotlinx/coroutines/BuildersKt");
        static System.IntPtr id_launch;
        public static Object Launch(this LifecycleCoroutineScope scope, IFunction2 block)
        {
            ICoroutineContext context = EmptyCoroutineContext.Instance;
            CoroutineStart start = CoroutineStart.Default;

            if (id_launch == System.IntPtr.Zero)
                id_launch = JNIEnv.GetStaticMethodID(class_ref,
                    "launch", "(Lkotlinx/coroutines/CoroutineScope;Lkotlin/coroutines/CoroutineContext;Lkotlinx/coroutines/CoroutineStart;Lkotlin/jvm/functions/Function2;)Lkotlinx/coroutines/Job;");

            System.IntPtr obj = JNIEnv.CallStaticObjectMethod(class_ref, id_launch,
                new JValue(scope), new JValue(context), new JValue(start), new JValue(block));
            return Object.GetObject<Object>(obj, JniHandleOwnership.TransferLocalRef);
        }
    }
}
