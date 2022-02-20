using Android.Graphics;
using Android.Renderscripts;
using Android.Util;
using Android.Views;
using Java.Lang;

namespace HdrViewfinder
{
    //
    // Renderscript-based merger for an HDR viewfinder
    //
    public class ViewfinderProcessor : Object, Allocation.IOnBufferAvailableListener
    {
        private Allocation mInputHdrAllocation;
        private Allocation mPrevAllocation;
        private Allocation mOutputAllocation;

        private ScriptIntrinsicYuvToRGB mHdrYuvToRGBScript;
        private ScriptIntrinsicColorMatrix mHdrColorMatrixScript;
        private ScriptIntrinsicBlend mHdrBlendScript;

        private Size mDimensions;
        private int mFrameCounter;

        private int mMode;

        public const int ModeNormal = 0;
        public const int ModeHdr = 2;

        public ViewfinderProcessor(RenderScript rs, Size dimensions)
        {
            Type.Builder yuvTypeBuilder = new Type.Builder(rs, Element.YUV(rs));
            yuvTypeBuilder.SetX(dimensions.Width);
            yuvTypeBuilder.SetY(dimensions.Height);
            yuvTypeBuilder.SetYuvFormat((int) ImageFormatType.Yuv420888);
            mInputHdrAllocation = Allocation.CreateTyped(rs, yuvTypeBuilder.Create(),
                AllocationUsage.IoInput | AllocationUsage.Script);

            Type.Builder rgbTypeBuilder = new Type.Builder(rs, Element.RGBA_8888(rs));
            rgbTypeBuilder.SetX(dimensions.Width);
            rgbTypeBuilder.SetY(dimensions.Height);
            mPrevAllocation = Allocation.CreateTyped(rs, rgbTypeBuilder.Create(),
                AllocationUsage.Script);
            mOutputAllocation = Allocation.CreateTyped(rs, rgbTypeBuilder.Create(),
                AllocationUsage.IoOutput | AllocationUsage.Script);

            mHdrYuvToRGBScript = ScriptIntrinsicYuvToRGB.Create(rs, Element.RGBA_8888(rs));
            mHdrColorMatrixScript = ScriptIntrinsicColorMatrix.Create(rs);
            mHdrColorMatrixScript.SetColorMatrix(new Matrix4f(new float[] { 0.5f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0.5f, 0, 0, 0, 0, 0.5f }));
            mHdrBlendScript = ScriptIntrinsicBlend.Create(rs, Element.RGBA_8888(rs));

            mDimensions = dimensions;
            mFrameCounter = 0;

            SetRenderMode(ModeNormal);
        }

        public Surface GetInputHdrSurface()
        {
            return mInputHdrAllocation.Surface;
        }

        public void SetOutputSurface(Surface output)
        {
            mInputHdrAllocation.SetOnBufferAvailableListener(output == null ? null : this);
            mOutputAllocation.Surface = output;
        }

        public void SetRenderMode(int mode)
        {
            mMode = mode;
        }

        //
        // Simple class to keep track of incoming frame count,
        // and to process the newest one in the processing thread
        //
        public void OnBufferAvailable(Allocation a)
        {
            a.IoReceive();
            mHdrYuvToRGBScript.SetInput(a);

            // Run processing pass
            if (mMode != ModeNormal)
            {
                if (mMode == ModeHdr)
                {
                    mHdrColorMatrixScript.ForEach(mPrevAllocation, mOutputAllocation);
                }
                else
                {
                    int cutPointX = (mFrameCounter & 1) == 0 ? 0 : mDimensions.Width / 2;
                    mOutputAllocation.Copy2DRangeFrom(cutPointX, 0, mDimensions.Width / 2, mDimensions.Height,
                        mPrevAllocation, cutPointX, 0);
                }

                mHdrYuvToRGBScript.ForEach(mPrevAllocation);

                if (mMode == ModeHdr)
                {
                    mHdrBlendScript.ForEachDstOver(mPrevAllocation, mOutputAllocation);
                }
                else
                {
                    int cutPointX = (mFrameCounter & 1) == 1 ? 0 : mDimensions.Width / 2;
                    mOutputAllocation.Copy2DRangeFrom(cutPointX, 0, mDimensions.Width / 2, mDimensions.Height,
                        mPrevAllocation, cutPointX, 0);
                }
            }
            else
            {
                mHdrYuvToRGBScript.ForEach(mOutputAllocation);
            }
            mFrameCounter++;

            mOutputAllocation.IoSend();
        }
    }
}
