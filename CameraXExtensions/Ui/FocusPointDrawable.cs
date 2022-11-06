using System;
using Android.Graphics;
using Android.Graphics.Drawables;

namespace CameraXExtensions
{
    public class FocusPointDrawable : Drawable
    {
        public FocusPointDrawable()
        {
            paint.SetStyle(Paint.Style.Stroke);
        }
        private Paint paint = new Paint()
        {
            AntiAlias = true,
            Color = Color.White
        };

        private float radius = 0f;
        private float centerX = 0f;
        private float centerY = 0f;

        public bool SetStrokeWidth(float strokeWidth)
        {
            if (paint.StrokeWidth == strokeWidth)
            {
                return false;
            }
            else
            {
                paint.StrokeWidth = strokeWidth;
                return true;
            }
        }

        protected override void OnBoundsChange(Rect bounds)
        {
            var width = bounds.Width();
            var height = bounds.Height();
            radius = Math.Min(width, height) / 2f - paint.StrokeWidth / 2f;
            centerX = width / 2f;
            centerY = height / 2f;
        }

        public override void Draw(Canvas canvas)
        {
            if (radius == 0f) return;

            canvas.DrawCircle(centerX, centerY, radius, paint);
        }

        public override void SetAlpha(int alpha)
        {
            paint.Alpha = alpha;
        }

        public override void SetColorFilter(ColorFilter colorFilter)
        {
            paint.SetColorFilter(colorFilter);
        }

        public override int Opacity => (int)Format.Translucent;
    }
}
