using Android.Graphics;
using Android.Media;
using Android.Util;

namespace Camera.Utils
{
    public static class ExifUtils
    {
        private const string Tag = "ExifUtils";

        // Transforms rotation and mirroring information into one of the [ExifInterface] constants
        public static Orientation ComputeExifOrientation(int rotationDegrees, bool mirrored)
        {
            return rotationDegrees switch
            {
                0 when !mirrored => Orientation.Normal,
                0 when mirrored => Orientation.FlipHorizontal,
                180 when !mirrored => Orientation.Rotate180,
                180 when mirrored => Orientation.FlipVertical,
                90 when !mirrored => Orientation.Rotate90,
                90 when mirrored => Orientation.Transpose,
                270 when !mirrored => Orientation.Rotate270,
                270 when mirrored => Orientation.Transverse,
                _ => Orientation.Undefined
            };
        }

        //
        // Helper function used to convert an EXIF orientation enum into a transformation matrix
        // that can be applied to a bitmap.
        //
        // @return matrix - Transformation required to properly display [Bitmap]
        //
        public static Matrix DecodeExifOrientation(Orientation exifOrientation)
        {
            var matrix = new Matrix();

            // Apply transformation corresponding to declared EXIF orientation
            switch(exifOrientation)
            {
                case Orientation.Normal: break;
                case Orientation.Rotate90: matrix.PostRotate(90F); break;
                case Orientation.Rotate180: matrix.PostRotate(180F); break;
                case Orientation.Rotate270: matrix.PostRotate(270F); break;
                case Orientation.FlipHorizontal: matrix.PostScale(-1F, 1F); break;
                case Orientation.FlipVertical: matrix.PostScale(1F, -1F); break;
                case Orientation.Transpose: matrix.PostRotate(90F); matrix.PostScale(-1F, 1F); break;
                case Orientation.Transverse: matrix.PostRotate(270F); matrix.PostScale(-1F, 1F); break;
                default:
                    // Error out if the EXIF orientation is invalid
                    Log.Error(Tag, "Invalid orientation: " + exifOrientation); break;
            };

            // Return the resulting matrix
            return matrix;
        }
    }
}
