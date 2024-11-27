using Android;
using Android.Content.PM;
using Android.Runtime;
using AndroidX.AppCompat.App;
using AndroidX.Camera.MLKit.Vision;
using AndroidX.Camera.View;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.Util;
using Java.Util;
using Java.Util.Concurrent;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.BarCode;
using Xamarin.Google.MLKit.Vision.Interfaces;
using Object = Java.Lang.Object;

namespace CameraXMlKit
{
    [Activity(Name = "com.android.example.camerax.mlkit.MainActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class MainActivity : AppCompatActivity,
        IConsumer
    {
        private PreviewView previewView;
        private IExecutorService cameraExecutor;
        private IBarcodeScanner barcodeScanner;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            // Request camera permissions
            if (AllPermissionsGranted())
            {
                StartCamera();
            }
            else
            {
                ActivityCompat.RequestPermissions(
                    this, RequiredPermissions, RequestCodePermissions
                );
            }

            cameraExecutor = Executors.NewSingleThreadExecutor();
        }

        public void Accept(Object t)
        {
            var result = t as MlKitAnalyzer.Result;
            var value = result?.GetValue(barcodeScanner);
            var barcodeResults = value.JavaCast<ArrayList>();
            if (barcodeResults == null ||
                barcodeResults.Size() == 0 ||
                barcodeResults.Get(0) == null)
            {
                previewView.Overlay.Clear();
                previewView.SetOnTouchListener(null); // no-op
                return;
            }

            var qrCodeViewModel = new QrCodeViewModel(barcodeResults.Get(0) as Barcode);
            var qrCodeDrawable = new QrCodeDrawable(qrCodeViewModel);

            previewView.SetOnTouchListener(qrCodeViewModel.QrCodeTouchCallback);
            previewView.Overlay.Clear();
            previewView.Overlay.Add(qrCodeDrawable);
        }

        private void StartCamera()
        {
            var cameraController = new LifecycleCameraController(BaseContext);
            previewView = FindViewById<PreviewView>(Resource.Id.viewFinder);

            var options = new BarcodeScannerOptions.Builder()
                .SetBarcodeFormats(Barcode.FormatQrCode)
                .Build();
            barcodeScanner = BarcodeScanning.GetClient(options);

            cameraController.SetImageAnalysisAnalyzer(
                ContextCompat.GetMainExecutor(this),
                new MlKitAnalyzer(
                    new List<IDetector> { barcodeScanner },
#pragma warning disable CS0618
                    CameraController.CoordinateSystemViewReferenced,
#pragma warning restore CS0618
                    ContextCompat.GetMainExecutor(this),
                    this
                )
            );

            cameraController.BindToLifecycle(this);
            previewView.Controller = cameraController;
        }

        private bool AllPermissionsGranted() =>
            RequiredPermissions.All(it =>
                ContextCompat.CheckSelfPermission(
                    BaseContext, it) == Permission.Granted);

        protected override void OnDestroy()
        {
            base.OnDestroy();
            cameraExecutor.Shutdown();
            barcodeScanner.Close();
        }

        private const string Tag = "CameraXMlKit";
        private const int RequestCodePermissions = 10;
        private static readonly string[] RequiredPermissions =
            { Manifest.Permission.Camera };

        public override void OnRequestPermissionsResult(
            int requestCode, string[] permissions, Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == RequestCodePermissions)
            {
                if (AllPermissionsGranted())
                {
                    StartCamera();
                }
                else
                {
                    Toast.MakeText(this,
                        "Permissions not granted by the user.",
                        ToastLength.Short).Show();
                    Finish();
                }
            }
        }
    }
}
