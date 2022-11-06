using Android.Graphics;
using Android.Views;
using AndroidX.Core.View;
using AndroidX.RecyclerView.Widget;

namespace CameraXExtensions
{
    //
    // An ItemDecoration used to center the first and last items within the RecyclerView.
    //
    public class OffsetCenterItemDecoration : RecyclerView.ItemDecoration
    {
        public override void GetItemOffsets(
            Rect outRect,
            View view,
            RecyclerView parent,
            RecyclerView.State state
        )
        {
            var layoutManager = parent.GetLayoutManager() as LinearLayoutManager;
            if (layoutManager == null) return;
            var position = layoutManager.GetPosition(view);
            if (position == 0 || position == layoutManager.ItemCount - 1)
            {
                MeasureChild(parent, view);
                var width = view.MeasuredWidth;
                if (position == 0)
                {
                    outRect.Left = (parent.Width - width) / 2;
                    outRect.Right = 0;
                }
                else if (position == layoutManager.ItemCount - 1)
                {
                    outRect.Left = 0;
                    outRect.Right = (parent.Width - width) / 2;
                }
                else
                {
                    outRect.Left = 0;
                    outRect.Right = 0;
                }
            }
        }

        //
        // Forces a measure if the view hasn't been measured yet.
        //
        private void MeasureChild(RecyclerView parent, View child)
        {
            if (ViewCompat.IsLaidOut(child)) return;
            var layoutManager = parent.GetLayoutManager() as LinearLayoutManager;
            if (layoutManager == null) return;
            var lp = child.LayoutParameters;

            var widthSpec = RecyclerView.LayoutManager.GetChildMeasureSpec(
                layoutManager.Width, layoutManager.WidthMode,
                layoutManager.PaddingLeft + layoutManager.PaddingRight, lp.Width,
                layoutManager.CanScrollHorizontally()
            );
            var heightSpec = RecyclerView.LayoutManager.GetChildMeasureSpec(
                layoutManager.Height, layoutManager.HeightMode,
                layoutManager.PaddingTop + layoutManager.PaddingBottom, lp.Height,
                layoutManager.CanScrollVertically()
            );
            child.Measure(widthSpec, heightSpec);
        }
    }
}
