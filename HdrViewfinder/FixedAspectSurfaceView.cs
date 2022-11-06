using Android.Content;
using Android.Content.Res;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Java.Lang;

namespace HdrViewfinder
{
    //
    // A SurfaceView that maintains its aspect ratio to be a desired target value.
    //
    // <p>Depending on the layout, the FixedAspectSurfaceView may not be able to maintain the
    // requested aspect ratio. This can happen if both the width and the height are exactly
    // determined by the layout.  To avoid this, ensure that either the height or the width is
    // adjustable by the view; for example, by setting the layout parameters to be WRAP_CONTENT for
    // the dimension that is best adjusted to maintain the aspect ratio.</p>
    //
    [Register("com.example.android.hdrviewfinder.FixedAspectSurfaceView")]
    public class FixedAspectSurfaceView : SurfaceView
    {
        //
        // Desired width/height ratio
        //
        private float mAspectRatio;

        private GestureDetector mGestureDetector;

        public FixedAspectSurfaceView(Context context, IAttributeSet attrs) : base(context, attrs)
        {
            // Get initial aspect ratio from custom attributes
            TypedArray a =
                context.Theme.ObtainStyledAttributes(attrs,
                    Resource.Styleable.FixedAspectSurfaceView, 0, 0);
            SetAspectRatio(a.GetFloat(
                Resource.Styleable.FixedAspectSurfaceView_aspectRatio, 1f));
            a.Recycle();
        }

        //
        // Set the desired aspect ratio for this view.
        //
        // @param aspect the desired width/height ratio in the current UI orientation. Must be a
        //               positive value.
        //
        public void SetAspectRatio(float aspect)
        {
            if (aspect <= 0)
            {
                throw new IllegalArgumentException("Aspect ratio must be positive");
            }
            mAspectRatio = aspect;
            RequestLayout();
        }

        //
        // Set a gesture listener to listen for touch events
        //
        public void SetGestureListener(Context context, GestureDetector.IOnGestureListener listener)
        {
            if (listener == null)
            {
                mGestureDetector = null;
            }
            else
            {
                mGestureDetector = new GestureDetector(context, listener);
            }
        }

        protected override void OnMeasure(int widthMeasureSpec, int heightMeasureSpec)
        {
            var widthMode = MeasureSpec.GetMode(widthMeasureSpec);
            var heightMode = MeasureSpec.GetMode(heightMeasureSpec);
            int width = MeasureSpec.GetSize(widthMeasureSpec);
            int height = MeasureSpec.GetSize(heightMeasureSpec);

            // General goal: Adjust dimensions to maintain the requested aspect ratio as much
            // as possible. Depending on the measure specs handed down, this may not be possible

            // Only set one of these to true
            bool scaleWidth = false;
            bool scaleHeight = false;

            // Sort out which dimension to scale, if either can be. There are 9 combinations of
            // possible measure specs; a few cases below handle multiple combinations
            //noinspection StatementWithEmptyBody
            if (widthMode == MeasureSpecMode.Exactly && heightMode == MeasureSpecMode.Exactly)
            {
                // Can't adjust sizes at all, do nothing
            }
            else if (widthMode == MeasureSpecMode.Exactly)
            {
                // Width is fixed, heightMode either AT_MOST or UNSPECIFIED, so adjust height
                scaleHeight = true;
            }
            else if (heightMode == MeasureSpecMode.Exactly)
            {
                // Height is fixed, widthMode either AT_MOST or UNSPECIFIED, so adjust width
                scaleWidth = true;
            }
            else if (widthMode == MeasureSpecMode.AtMost && heightMode == MeasureSpecMode.AtMost)
            {
                // Need to fit into box <= [width, height] in size.
                // Maximize the View's area while maintaining aspect ratio
                // This means keeping one dimension as large as possible and shrinking the other
                float boxAspectRatio = width / (float)height;
                if (boxAspectRatio > mAspectRatio)
                {
                    // Box is wider than requested aspect; pillarbox
                    scaleWidth = true;
                }
                else
                {
                    // Box is narrower than requested aspect; letterbox
                    scaleHeight = true;
                }
            }
            else if (widthMode == MeasureSpecMode.AtMost)
            {
                // Maximize width, heightSpec is UNSPECIFIED
                scaleHeight = true;
            }
            else if (heightMode == MeasureSpecMode.AtMost)
            {
                // Maximize height, widthSpec is UNSPECIFIED
                scaleWidth = true;
            }
            else
            {
                // Both MeasureSpecs are UNSPECIFIED. This is probably a pathological layout,
                // with width == height == 0
                // but arbitrarily scale height anyway
                scaleHeight = true;
            }

            // Do the scaling
            if (scaleWidth)
            {
                width = (int)(height * mAspectRatio);
            }
            else if (scaleHeight)
            {
                height = (int)(width / mAspectRatio);
            }

            // Override width/height if needed for EXACTLY and AT_MOST specs
            width = View.ResolveSizeAndState(width, widthMeasureSpec, 0);
            height = View.ResolveSizeAndState(height, heightMeasureSpec, 0);

            // Finally set the calculated dimensions
            SetMeasuredDimension(width, height);
        }

        public override bool OnTouchEvent(MotionEvent motionEvent)
        {
            return mGestureDetector != null && mGestureDetector.OnTouchEvent(motionEvent);
        }
    }
}
