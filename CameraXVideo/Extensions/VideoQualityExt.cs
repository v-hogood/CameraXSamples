using System;
using Android.Media;
using Android.Runtime;
using AndroidX.Camera.Core;
using AndroidX.Camera.Video;
using Java.Lang;

namespace CameraXVideo
{
    public static class VideoQualityExt
    {
        //
        // a helper function to retrieve the aspect ratio from a QualitySelector enum.
        //
        public static int GetAspectRatio(this Quality quality)
        {
            return quality.GetValue() switch
            {
                CamcorderQuality.Q2160p => AspectRatio.Ratio169,
                CamcorderQuality.Q1080p => AspectRatio.Ratio169,
                CamcorderQuality.Q720p => AspectRatio.Ratio169,
                CamcorderQuality.Q480p => AspectRatio.Ratio43,
                _ => throw new UnsupportedOperationException()
            };
        }

        //
        // a helper function to retrieve the aspect ratio string from a Quality enum.
        //
        public static string GetAspectRatioString(this Quality quality, bool portraitMode)
        {
            Tuple<int,int> ratio = quality.GetValue() switch
            {
                CamcorderQuality.Q2160p => new Tuple<int, int>(16, 9),
                CamcorderQuality.Q1080p => new Tuple<int, int>(16, 9),
                CamcorderQuality.Q720p => new Tuple<int, int>(16, 9),
                CamcorderQuality.Q480p => new Tuple<int, int>(4, 3),
                _ => throw new UnsupportedOperationException()
            };

            return portraitMode ?
                "V," + ratio.Item2 + ":" + ratio.Item1 :
                "H," + ratio.Item1 + ":" + ratio.Item2;
        }


        //
        // Get the name (a string) from the given Video.Quality object.
        //
        public static string GetNameString(this Quality quality)
        {
            return quality.GetValue() switch
            {
                CamcorderQuality.Q2160p => "QUALITY_UHD(2160p)",
                CamcorderQuality.Q1080p => "QUALITY_FHD(1080p)",
                CamcorderQuality.Q720p => "QUALITY_HD(720p)",
                CamcorderQuality.Q480p => "QUALITY_SD(480p)",
                _ => throw new IllegalArgumentException("Quality $this is NOT supported")
            };
        }

        //
        // Translate Video.Quality name(a string) to its Quality object.
        //
        public static CamcorderQuality GetQualityObject(this Quality quality, string name)
        {
            return name switch
            {
                "QUALITY_UHD(2160p)" => CamcorderQuality.Q2160p,
                "QUALITY_FHD(1080p)" => CamcorderQuality.Q1080p,
                "QUALITY_HD(720p)" => CamcorderQuality.Q720p,
                "QUALITY_SD(480p)" => CamcorderQuality.Q480p,
                _ => throw new IllegalArgumentException("Quality string $name is NOT supported")
            };
        }

        static System.IntPtr class_ref = JNIEnv.FindClass("androidx/camera/video/AutoValue_Quality_ConstantQuality");
        static System.IntPtr id_getValue;
        public static CamcorderQuality GetValue(this Quality quality)
        {
            if (id_getValue == System.IntPtr.Zero)
                id_getValue = JNIEnv.GetMethodID(class_ref, "getValue", "()I");

            return (CamcorderQuality) JNIEnv.CallIntMethod(((IJavaObject)quality).Handle, id_getValue);
        }
    }
}
