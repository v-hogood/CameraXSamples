using Android.Graphics;
using Android.Graphics.Drawables;

namespace CameraXMlKit
{
    //
    // A Drawable that handles displaying a QR Code's data and a bounding box around the QR code.
    //
    class QrCodeDrawable : Drawable
    {
        public QrCodeDrawable(QrCodeViewModel qrCodeViewModel)
        {
            this.qrCodeViewModel = qrCodeViewModel;

            boundingRectPaint.SetStyle(Paint.Style.Stroke);
            contentRectPaint.SetStyle(Paint.Style.Fill);
            textWidth = (int)contentTextPaint.MeasureText(qrCodeViewModel.QrContent);
        }

        private Paint boundingRectPaint = new Paint()
        {
            Color = Color.Yellow,
            StrokeWidth = 5F,
            Alpha = 200
        };

        private Paint contentRectPaint = new Paint()
        {
            Color = Color.Yellow,
            Alpha = 255
        };

        private Paint contentTextPaint = new Paint()
        {
            Color = Color.DarkGray,
            Alpha = 255,
            TextSize = 36F
        };

        private QrCodeViewModel qrCodeViewModel;
        private int contentPadding = 25;
        private int textWidth;

        public override void Draw(Canvas canvas)
        {
            canvas.DrawRect(qrCodeViewModel.BoundingRect, boundingRectPaint);
            canvas.DrawRect(
                new Rect(
                    qrCodeViewModel.BoundingRect.Left,
                    qrCodeViewModel.BoundingRect.Bottom + contentPadding / 2,
                    qrCodeViewModel.BoundingRect.Left + textWidth + contentPadding * 2,
                    qrCodeViewModel.BoundingRect.Bottom + (int)contentTextPaint.TextSize + contentPadding),
                contentRectPaint
            );
            canvas.DrawText(
                qrCodeViewModel.QrContent,
                (qrCodeViewModel.BoundingRect.Left + contentPadding),
                (qrCodeViewModel.BoundingRect.Bottom + contentPadding * 2),
                contentTextPaint
            );
        }

        public override void SetAlpha(int alpha)
        {
            boundingRectPaint.Alpha = alpha;
            contentRectPaint.Alpha = alpha;
            contentTextPaint.Alpha = alpha;
        }

        public override void SetColorFilter(ColorFilter colorFilter)
        {
            boundingRectPaint.SetColorFilter(colorFilter);
            contentRectPaint.SetColorFilter(colorFilter);
            contentTextPaint.SetColorFilter(colorFilter);
        }

        public override int Opacity => (int)Format.Translucent;
    }
}
