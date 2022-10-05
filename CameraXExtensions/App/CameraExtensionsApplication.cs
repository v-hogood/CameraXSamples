using Android.App;
using Android.Runtime;
using Android.Util;
using AndroidX.Camera.Camera2;
using AndroidX.Camera.Core;
using Java.Lang;
using Java.Util.Concurrent;
using Kotlin.Coroutines;
using Kotlin.Jvm;
using Xamarin.Coil;
using Xamarin.KotlinX.Coroutines;

namespace CameraXExtensions
{
    [Application(Name = "com.android.example.cameraextensions.CameraExtensionsApplication")]
    class CameraExtensionsApplication : Application, CameraXConfig.IProvider, IImageLoaderFactory
    {
        public CameraExtensionsApplication(System.IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        { }

        public CameraXConfig CameraXConfig
        {
            get
            {
                return CameraXConfig.Builder.FromConfig(Camera2Config.DefaultConfig())
                    .SetCameraExecutor(new DispatcherExecutor(Dispatchers.IO))
                    .SetMinimumLoggingLevel((int)LogPriority.Error)
                    .Build();
            }
        }

        public IImageLoader NewImageLoader()
        {
            return new ImageLoaderBuilder(this)
                .Crossfade(true)
                .Build();
        }
    }

    public class DispatcherExecutor : Object, IExecutor
    {
        CoroutineDispatcher dispatcher;
        public DispatcherExecutor(CoroutineDispatcher dispatcher) => this.dispatcher = dispatcher;

        public void Execute(IRunnable command) => dispatcher.Dispatch(EmptyCoroutineContext.Instance, command);

        public override string ToString() => dispatcher.ToString();
    }
}
