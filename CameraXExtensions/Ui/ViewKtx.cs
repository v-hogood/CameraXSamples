using System;
using Android.Runtime;
using Android.Views;
using AndroidX.Core.View;
using Java.Lang;
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

        static IntPtr class_ref = JNIEnv.FindClass("kotlinx/coroutines/BuildersKt");
        static IntPtr id_launch;
        public static Object Launch(this ICoroutineScope scope, Action action)
        {
            var context = EmptyCoroutineContext.Instance;
            var start = CoroutineStart.Default;
            var block = new Function2(action);

            if (id_launch == IntPtr.Zero)
                id_launch = JNIEnv.GetStaticMethodID(class_ref,
                    "launch", "(Lkotlinx/coroutines/CoroutineScope;Lkotlin/coroutines/CoroutineContext;Lkotlinx/coroutines/CoroutineStart;Lkotlin/jvm/functions/Function2;)Lkotlinx/coroutines/Job;");

            IntPtr obj = JNIEnv.CallStaticObjectMethod(class_ref, id_launch,
                new JValue(scope), new JValue(context), new JValue(start), new JValue(block));
            return Object.GetObject<Object>(obj, JniHandleOwnership.TransferLocalRef);
        }
    }
}
