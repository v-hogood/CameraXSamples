using Android.Graphics;
using Android.OS;
using Android.Renderscripts;
using Android.Util;
using Android.Views;
using Com.Example.Android.Hdrviewfinder;
using Java.Lang;

namespace HdrViewfinder
{
    //
    // Renderscript-based merger for an HDR viewfinder
    //
    public class ViewfinderProcessor
    {
        private Allocation mInputHdrAllocation;
        private Allocation mInputNormalAllocation;
        private Allocation mPrevAllocation;
        private Allocation mOutputAllocation;

        private Handler mProcessingHandler;
        private ScriptC_hdr_merge mHdrMergeScript;

        public ProcessingTask mHdrTask;
        public ProcessingTask mNormalTask;

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
            mInputNormalAllocation = Allocation.CreateTyped(rs, yuvTypeBuilder.Create(),
                AllocationUsage.IoInput | AllocationUsage.Script);

            Type.Builder rgbTypeBuilder = new Type.Builder(rs, Element.RGBA_8888(rs));
            rgbTypeBuilder.SetX(dimensions.Width);
            rgbTypeBuilder.SetY(dimensions.Height);
            mPrevAllocation = Allocation.CreateTyped(rs, rgbTypeBuilder.Create(),
                AllocationUsage.Script);
            mOutputAllocation = Allocation.CreateTyped(rs, rgbTypeBuilder.Create(),
                AllocationUsage.IoOutput | AllocationUsage.Script);

            HandlerThread processingThread = new HandlerThread("ViewfinderProcessor");
            processingThread.Start();
            mProcessingHandler = new Handler(processingThread.Looper);

            mHdrMergeScript = new ScriptC_hdr_merge(rs);

            mHdrMergeScript.Set_gPrevFrame(mPrevAllocation);

            mHdrTask = new ProcessingTask(this, mInputHdrAllocation, dimensions.Width / 2, true);
            mNormalTask = new ProcessingTask(this, mInputNormalAllocation, 0, false);

            SetRenderMode(ModeNormal);
        }

        public Surface GetInputHdrSurface()
        {
            return mInputHdrAllocation.Surface;
        }

        public Surface GetInputNormalSurface()
        {
            return mInputNormalAllocation.Surface;
        }

        public void setOutputSurface(Surface output)
        {
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
        public class ProcessingTask : Object, IRunnable, Allocation.IOnBufferAvailableListener
        {
            private int mPendingFrames = 0;
            private int mFrameCounter = 0;
            private int mCutPointX;
            private bool mCheckMerge;

            private Allocation mInputAllocation;

            private ViewfinderProcessor mParent;

            public ProcessingTask(ViewfinderProcessor parent, Allocation input, int cutPointX, bool checkMerge)
            {
                mParent = parent;
                mInputAllocation = input;
                mInputAllocation.SetOnBufferAvailableListener(this);
                mCutPointX = cutPointX;
                mCheckMerge = checkMerge;
            }

            public void OnBufferAvailable(Allocation a)
            {
                lock(this)
                {
                    mPendingFrames++;
                    mParent.mProcessingHandler.Post(this);
                }
            }

            public void Run()
            {
                // Find out how many frames have arrived
                int pendingFrames;
                lock(this)
                {
                    pendingFrames = mPendingFrames;
                    mPendingFrames = 0;

                    // Discard extra messages in case processing is slower than frame rate
                    mParent.mProcessingHandler.RemoveCallbacks(this);
                }

                // Get to newest input
                for (int i = 0; i < pendingFrames; i++)
                {
                    mInputAllocation.IoReceive();
                }

                mParent.mHdrMergeScript.Set_gFrameCounter(mFrameCounter++);
                mParent.mHdrMergeScript.Set_gCurrentFrame(mInputAllocation);
                mParent.mHdrMergeScript.Set_gCutPointX(mCutPointX);
                if (mCheckMerge && mParent.mMode == ModeHdr)
                {
                    mParent.mHdrMergeScript.Set_gDoMerge(1);
                }
                else
                {
                    mParent.mHdrMergeScript.Set_gDoMerge(0);
                }

                // Run processing pass
                mParent.mHdrMergeScript.ForEach_mergeHdrFrames(mParent.mPrevAllocation, mParent.mOutputAllocation);
                mParent.mOutputAllocation.IoSend();
            }
        }
    }
}
