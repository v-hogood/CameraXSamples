using Android.Content;
using Android.Graphics;
using Android.Net;
using Android.Views;
using Java.Lang;
using Xamarin.Google.MLKit.Vision.Barcode.Common;

namespace CameraXMlKit
{
    //
    // A ViewModel for encapsulating the data for a QR Code, including the encoded data, the bounding
    // box, and the touch behavior on the QR Code.
    //
    // As is, this class only handles displaying the QR Code data if it's a URL. Other data types
    // can be handled by adding more cases of Barcode.TYPE_URL in the init block.
    //
    public class QrCodeViewModel : Object,
        View.IOnTouchListener
    {
        public Rect BoundingRect;
        public string QrContent = "";
        public View.IOnTouchListener QrCodeTouchCallback = null; // no-op

        public QrCodeViewModel(Barcode barcode)
        {
            BoundingRect = barcode.BoundingBox;

            if (barcode.ValueType is Barcode.TypeUrl)
            {
                QrContent = barcode.Url.Url;
                QrCodeTouchCallback = this;
            }
            // Add other QR Code types here to handle other types of data,
            // like Wifi credentials.
            else
            {
                QrContent = "Unsupported data type: " + barcode.RawValue.ToString();
            }
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            if (e.Action == MotionEventActions.Down && BoundingRect.Contains((int)e.GetX(), (int)e.GetY()))
            {
                var openBrowserIntent = new Intent(Intent.ActionView);
                openBrowserIntent.SetData(Uri.Parse(QrContent));
                v.Context.StartActivity(openBrowserIntent);
            }
            return true; // return true from the callback to signify the event was handled
        }
    }
}
