using Android.Content;
using Android.Util;
using Android.Views;
using Android.Views.Animations;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Java.Lang;
using static AndroidX.RecyclerView.Widget.RecyclerView;
using static AndroidX.RecyclerView.Widget.RecyclerView.SmoothScroller;

namespace CameraXExtensions
{
    //
    // Snaps the item to the center of the RecyclerView. Note that this SnapHelper ignores any
    // decorations applied to the child view. This is required since the first and last item are
    // centered by applying padding to the start or end of the view via an Item Decoration
    // @see OffsetCenterItemDecoration
    //
    class CenterItemSnapHelper : LinearSnapHelper
    {
        private const float MillisecondsPerInch = 100f;
        private const int MaxScrollOnFlingDurationMs = 1000;

        private Context context;
        private RecyclerView recyclerView;
        private Scroller scroller;
        private int maxScrollDistance = 0;

        public override void AttachToRecyclerView(RecyclerView recyclerView)
        {
            if (recyclerView != null)
            {
                context = recyclerView.Context;
                this.recyclerView = recyclerView;
                scroller = new Scroller(context, new DecelerateInterpolator())
;           }
            else
            {
                context = null;
                this.recyclerView = null;
                scroller = null;
            }
            base.AttachToRecyclerView(recyclerView);
        }

        public override View FindSnapView(LayoutManager layoutManager) =>
            FindMiddleView(layoutManager);

        protected override SmoothScroller CreateScroller(LayoutManager layoutManager)
        {
            if (layoutManager! is IScrollVectorProvider)
                return base.CreateScroller(layoutManager);
            if (context == null) return null;
            return new CenterSmoothScroller(this, layoutManager);
        }

        class CenterSmoothScroller : LinearSmoothScroller
        {
            CenterItemSnapHelper parent;
            LayoutManager layoutManager;
            public CenterSmoothScroller(CenterItemSnapHelper parent, LayoutManager layoutManager) : base(parent.context)
            {
                this.parent = parent;
                this.layoutManager = layoutManager;
            }

            protected override void OnTargetFound(View targetView, State state, Action action)
            {
                var snapDistance = parent.CalculateDistanceToFinalSnap(layoutManager, targetView);
                var dx = snapDistance[0];
                var dy = snapDistance[1];
                var dt = CalculateTimeForDeceleration(Math.Abs(dx));
                var time = Math.Max(1, Math.Min(MaxScrollOnFlingDurationMs, dt));
                action.Update(dx, dy, time, base.MDecelerateInterpolator);
            }

            protected override float CalculateSpeedPerPixel(DisplayMetrics displayMetrics)
            {
                return MillisecondsPerInch / (float)displayMetrics.DensityDpi;
            }
        }

        public override int[] CalculateDistanceToFinalSnap(
            LayoutManager layoutManager,
            View targetView)
        {
            var output = new int[2];
            output[0] = DistanceToMiddleView(layoutManager, targetView);
            return output;
        }

        public override int[] CalculateScrollDistance(int velocityX, int velocityY)
        {
            var output = new int[2];
            var layoutManager =
                recyclerView?.GetLayoutManager() as LinearLayoutManager;
            if (layoutManager == null) return output;

            if (maxScrollDistance == 0) {
                maxScrollDistance = (layoutManager.Width) / 2;
            }

            scroller?.Fling(0, 0, velocityX, velocityY, -maxScrollDistance, maxScrollDistance, 0, 0);
            output[0] = (int)scroller?.FinalX;
            output[1] = (int)scroller?.FinalY;
            return output;
        }

        private int DistanceToMiddleView(LayoutManager layoutManager, View targetView)
        {
            var middle = layoutManager.Width / 2;
            var targetMiddle = targetView.Left + targetView.Width / 2;
                return targetMiddle - middle;
        }

        private View FindMiddleView(LayoutManager layoutManager)
        {
            if (layoutManager == null) return null;

            var childCount = layoutManager.ChildCount;
            if (childCount == 0) return null;

            var absClosest = Integer.MaxValue;
            View closestView = null;
            var middle = layoutManager.Width / 2;

            for (int i = 0; i < childCount; i++)
            {
                var child = layoutManager.GetChildAt(i);
                if (child == null) continue;
                var absDistanceToMiddle = Math.Abs((child.Left + child.Width / 2) - middle);
                if (absDistanceToMiddle < absClosest)
                {
                    absClosest = absDistanceToMiddle;
                    closestView = child;
                }
            }
            return closestView;
        }
    }
}
