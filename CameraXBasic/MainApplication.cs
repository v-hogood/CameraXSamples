using System;
using Android.App;
using Android.Runtime;
using Android.Util;
using AndroidX.Camera.Camera2;
using AndroidX.Camera.Core;

namespace CameraXBasic
{
    // Set CameraX logging level to Log.ERROR to avoid excessive logcat messages.
    // Refer to https://developer.android.com/reference/androidx/camera/core/CameraXConfig.Builder#setMinimumLoggingLevel(int)
    // for details.
    [Application(Name = "com.android.example.cameraxbasic.MainApplication")]
    class MainApplication : Application, CameraXConfig.IProvider
    {
        public MainApplication(IntPtr handle, JniHandleOwnership transfer) : base(handle, transfer)
        { }

        CameraXConfig CameraXConfig.IProvider.CameraXConfig
        {
            get => AndroidX.Camera.Core.CameraXConfig.Builder.FromConfig(Camera2Config.DefaultConfig())
                .SetMinimumLoggingLevel((int)LogPriority.Error).Build();
        }
    }
}
