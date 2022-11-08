using System;
using Android.App;
using Android.Runtime;
using Android.Util;
using AndroidX.Camera.Camera2;
using AndroidX.Camera.Core;
using Xamarin.Coil;
using Xamarin.KotlinX.Coroutines;
using static Xamarin.KotlinX.Coroutines.ExecutorsKt;

namespace CameraXExtensions
{
    [Application(Name = "com.android.example.cameraextensions.CameraExtensionsApplication")]
    class CameraExtensionsApplication : Application, CameraXConfig.IProvider, IImageLoaderFactory
    {
        public CameraExtensionsApplication(IntPtr handle, JniHandleOwnership transfer)
            : base(handle, transfer) { }

        public CameraXConfig CameraXConfig =>
            CameraXConfig.Builder.FromConfig(Camera2Config.DefaultConfig())
                .SetCameraExecutor(AsExecutor(Dispatchers.IO))
                .SetMinimumLoggingLevel((int)LogPriority.Error)
                .Build();

        public IImageLoader NewImageLoader() =>
            new ImageLoaderBuilder(this)
                .Crossfade(true)
                .Build();
    }
}
