using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Camera.View;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.Util;
using Java.Lang;
using Java.Util.Concurrent;
using Xamarin.Google.MLKit.Vision.Barcode.Common;
using Xamarin.Google.MLKit.Vision.BarCode;
using Xamarin.Google.MLKit.Vision.Interfaces;

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
            Barcode[] barcodeResults = result?.GetValue(barcodeScanner);
            if ((barcodeResults == null) ||
                (barcodeResults.Length == 0) ||
                (barcodeResults.First() == null))
            {
                previewView.Overlay.Clear();
                previewView.SetOnTouchListener(null); //no-op
                return;
            }

            var qrCodeViewModel = new QrCodeViewModel(barcodeResults[0]);
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
                    CameraController.CoordinateSystemViewReferenced,
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
