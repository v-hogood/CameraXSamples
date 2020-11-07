using System.Diagnostics;
using Android.Content;
using Android.Graphics;
using Android.Media;
using Android.Renderscripts;

namespace Camera.Utils
{
    // Helper class used to efficiently convert a [Media.Image] object from
    // [ImageFormat.YUV_420_888] format to an RGB [Bitmap] object.
    //
    // The [yuvToRgb] method is able to achieve the same FPS as the CameraX image
    // analysis use case on a Pixel 3 XL device at the default analyzer resolution,
    // which is 30 FPS with 640x480.
    //
    // NOTE: This has been tested in a limited number of devices and is not
    // considered production-ready code. It was created for illustration purposes,
    // since this is not an efficient camera pipeline due to the multiple copies
    // required to convert each frame.
    public class YuvToRgbConverter
    {
        public YuvToRgbConverter(Context context)
        {
            rs = RenderScript.Create(context);
            scriptYuvToRgb = ScriptIntrinsicYuvToRGB.Create(rs, Element.U8_4(rs));
        }

        private RenderScript rs;
        private ScriptIntrinsicYuvToRGB scriptYuvToRgb;

        private int pixelCount = -1;
        private byte[] yuvBuffer;
        private Allocation inputAllocation;
        private Allocation outputAllocation;

        public void YuvToRgb(Image image, Bitmap output)
        {
            // Ensure that the intermediate output byte buffer is allocated
            if (yuvBuffer == null)
            {
                pixelCount = image.CropRect.Width() * image.CropRect.Height();
                // Bits per pixel is an average for the whole image, so it's useful to compute the size
                // of the full buffer but should not be used to determine pixel offsets
                var pixelSizeBits = ImageFormat.GetBitsPerPixel(ImageFormatType.Yuv420888);
                yuvBuffer = new byte[pixelCount * pixelSizeBits / 8];
            }

            // Get the YUV data in byte array form using NV21 format
            ImageToByteArray(image, yuvBuffer);

            // Ensure that the RenderScript inputs and outputs are allocated
            if (inputAllocation == null)
            {
                // Explicitly create an element with type NV21, since that's the pixel format we use
                var elemType = new Type.Builder(rs, Element.YUV(rs)).SetYuvFormat((int)ImageFormatType.Nv21).Create();
                inputAllocation = Allocation.CreateSized(rs, elemType.Element, yuvBuffer.Length);
            }
            if (outputAllocation == null)
            {
                outputAllocation = Allocation.CreateFromBitmap(rs, output);
            }

            // Convert NV21 format YUV to RGB
            inputAllocation.CopyFrom(yuvBuffer);
            scriptYuvToRgb.SetInput(inputAllocation);
            scriptYuvToRgb.ForEach(outputAllocation);
            outputAllocation.CopyTo(output);
        }

        private void ImageToByteArray(Image image, byte[] outputBuffer)
        {
            Debug.Assert(image.Format == ImageFormatType.Yuv420888);

            var imageCrop = image.CropRect;
            var imagePlanes = image.GetPlanes();

            for (int planeIndex = 0; planeIndex < imagePlanes.Length; planeIndex++)
            {
                // How many values are read in input for each output value written
                // Only the Y plane has a value for every pixel, U and V have half the resolution i.e.
                //
                // Y Plane            U Plane    V Plane
                // ===============    =======    =======
                // Y Y Y Y Y Y Y Y    U U U U    V V V V
                // Y Y Y Y Y Y Y Y    U U U U    V V V V
                // Y Y Y Y Y Y Y Y    U U U U    V V V V
                // Y Y Y Y Y Y Y Y    U U U U    V V V V
                // Y Y Y Y Y Y Y Y
                // Y Y Y Y Y Y Y Y
                // Y Y Y Y Y Y Y Y
                int outputStride;

                // The index in the output buffer the next value will be written at
                // For Y it's zero, for U and V we start at the end of Y and interleave them i.e.
                //
                // First chunk        Second chunk
                // ===============    ===============
                // Y Y Y Y Y Y Y Y    V U V U V U V U
                // Y Y Y Y Y Y Y Y    V U V U V U V U
                // Y Y Y Y Y Y Y Y    V U V U V U V U
                // Y Y Y Y Y Y Y Y    V U V U V U V U
                // Y Y Y Y Y Y Y Y
                // Y Y Y Y Y Y Y Y
                // Y Y Y Y Y Y Y Y
                int outputOffset;

                switch (planeIndex)
                {
                    case 0:
                        outputStride = 1;
                        outputOffset = 0;
                        break;
                    case 1:
                        outputStride = 2;
                        // For NV21 format, U is in odd-numbered indices
                        outputOffset = pixelCount + 1;
                        break;
                    case 2:
                        outputStride = 2;
                        // For NV21 format, V is in even-numbered indices
                        outputOffset = pixelCount;
                        break;
                    default:
                        // Image contains more than 3 planes, something strange is going on
                        return;
                }

                var plane = imagePlanes[planeIndex];
                var planeBuffer = plane.Buffer;
                var rowStride = plane.RowStride;
                var pixelStride = plane.PixelStride;

                // We have to divide the width and height by two if it's not the Y plane
                var planeCrop = planeIndex == 0 ?
                    imageCrop :
                    new Rect(
                        imageCrop.Left / 2,
                        imageCrop.Top / 2,
                        imageCrop.Right / 2,
                        imageCrop.Bottom / 2
                    );

                var planeWidth = planeCrop.Width();
                var planeHeight = planeCrop.Height();

                // Intermediate buffer used to store the bytes of each row
                var rowBuffer = new byte[plane.RowStride];

                // Size of each row in bytes
                var rowLength = (pixelStride == 1 && outputStride == 1) ?
                    planeWidth :
                    // Take into account that the stride may include data from pixels other than this
                    // particular plane and row, and that could be between pixels and not after every
                    // pixel:
                    //
                    // |---- Pixel stride ----|                    Row ends here --> |
                    // | Pixel 1 | Other Data | Pixel 2 | Other Data | ... | Pixel N |
                    //
                    // We need to get (N-1) * (pixel stride bytes) per row + 1 byte for the last pixel
                    (planeWidth - 1) * pixelStride + 1;

                for (int row = 0; row < planeHeight; row++)
                {
                    // Move buffer position to the beginning of this row
                    planeBuffer.Position(
                        (row + planeCrop.Top) * rowStride + planeCrop.Left * pixelStride);

                    if (pixelStride == 1 && outputStride == 1)
                    {
                        // When there is a single stride value for pixel and output, we can just copy
                        // the entire row in a single step
                        planeBuffer.Get(outputBuffer, outputOffset, rowLength);
                        outputOffset += rowLength;
                    }
                    else
                    {
                        // When either pixel or output have a stride > 1 we must copy pixel by pixel
                        planeBuffer.Get(rowBuffer, 0, rowLength);
                        for (int col = 0; col < planeWidth; col++)
                        {
                            outputBuffer[outputOffset] = rowBuffer[col * pixelStride];
                            outputOffset += outputStride;
                        }
                    }
                }
            }
        }
    }
}
