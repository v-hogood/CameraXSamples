using System;
using Java.Lang;

namespace CameraXVideo
{
    public static class VideoQualityExt
    {
        //
        // a helper function to retrieve the aspect ratio from a QualitySelector enum.
        //
        public static int GetAspectRatio(Quality quality)
        {
            return quality switch
            {
                Quality.UHD => AspectRatio.RATIO_16_9,
                Quality.FHD => AspectRatio.RATIO_16_9,
                Quality.HD => AspectRatio.RATIO_16_9,
                Quality.SD => AspectRatio.RATIO_4_3,
                _ => throw new UnsupportedOperationException()
            };
        }

        //
        // a helper function to retrieve the aspect ratio string from a Quality enum.
        //
        public static string GetAspectRatioString(Quality quality, bool portraitMode)
        {
            Tuple<int,int> ratio = quality switch
            {
                Quality.UHD => new Tuple<int, int>(16, 9),
                Quality.FHD => new Tuple<int, int>(16, 9),
                Quality.HD => new Tuple<int, int>(16, 9),
                Quality.SD => new Tuple<int, int>(4, 3),
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
            return quality switch
            {
                Quality.UHD => "QUALITY_UHD(2160p)",
                Quality.FHD => "QUALITY_FHD(1080p)",
                Quality.HD => "QUALITY_HD(720p)",
                Quality.SD => "QUALITY_SD(480p)",
                _ => throw new IllegalArgumentException("Quality $this is NOT supported")
            };
        }

        //
        // Translate Video.Quality name(a string) to its Quality object.
        //
        public static Quality GetQualityObject(string name)
        {
            return name switch
            {
                Quality.UHD.GetNameString() => Quality.UHD,
                Quality.FHD.GetNameString() => Quality.FHD,
                Quality.HD.GetNameString() => Quality.HD,
                Quality.SD.GetNameString() => Quality.SD,
                _ => throw new IllegalArgumentException("Quality string $name is NOT supported")
            };
        }
    }
}
