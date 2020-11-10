using System;
using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.OS;
using Android.Runtime;
using Android.Support.V7.App;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Xamarin.TensorFlow.Lite;
using Xamarin.TensorFlow.Lite.Nnapi;
using Camera.Utils;

namespace CameraXTfLite
{
    // Activity that displays the camera and performs object detection on the incoming frames
    [Activity(Name = "com.android.example.camerax.tflite.CameraActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class CameraActivity : AppCompatActivity,
        View.IOnClickListener,
        ImageAnalysis.IAnalyzer
    {
        private ConstraintLayout container;
        private Bitmap bitmapBuffer;

        private Java.Util.Concurrent.IExecutorService executor =
            Java.Util.Concurrent.Executors.NewSingleThreadExecutor();
        private string[] permissions = { Manifest.Permission.Camera };
        private int permissionsRequestCode = new Random().Next(0, 10000);

        private int lensFacing = CameraSelector.LensFacingBack;
        private bool isFrontFacing() { return lensFacing == CameraSelector.LensFacingFront; }

        private bool pauseAnalysis = false;
        private int imageRotationDegrees = 0;
        private Java.Nio.ByteBuffer tfImageBuffer;

        private Interpreter tflite;
        private ObjectDetectionHelper detector;
        private Size tfInputSize;
        private int[] argb8888;
        private byte[] rgb888;

        private YuvToRgbConverter converter;
        private int frameCounter;
        private long lastFpsTimestamp;
        private PreviewView viewFinder;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_camera);
            container = FindViewById(Resource.Id.camera_container) as ConstraintLayout;

            FindViewById(Resource.Id.camera_capture_button).SetOnClickListener(this);

            // load mapped file
            var fd = Assets.OpenFd(ModelPath);
            var channel = new Java.IO.FileInputStream(fd.FileDescriptor).Channel;
            var model = channel.Map(Java.Nio.Channels.FileChannel.MapMode.ReadOnly,
                fd.StartOffset, fd.Length);
            tflite = new Interpreter(model,
                new Interpreter.Options().AddDelegate(new NnApiDelegate()));
            fd.Close();

            // load labels
            var stream = Assets.Open(LabelsPath);
            var reader = new Java.IO.BufferedReader(
                new Java.IO.InputStreamReader(stream));
            List<string> labels = new List<string>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.Trim().Length > 0)
                {
                    labels.Add(line);
                }
            }
            detector = new ObjectDetectionHelper(tflite, labels);
            stream.Close();

            // find input size
            var inputIndex = 0;
            var inputShape = tflite.GetInputTensor(inputIndex).Shape();
            tfInputSize = new Size(inputShape[2], inputShape[1]); // Order of axis is: {1, height, width, 3}
            argb8888 = new int[tfInputSize.Width * tfInputSize.Height];
            rgb888 = new byte[tfInputSize.Width * tfInputSize.Height * 3];

            tfImageBuffer = Java.Nio.ByteBuffer.AllocateDirect(tfInputSize.Width * tfInputSize.Height * 3);
            tfImageBuffer.Order(Java.Nio.ByteOrder.NativeOrder());
        }

        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.camera_capture_button)
            {
                // Disable all camera controls
                v.Enabled = false;

                ImageView imagePredicted = FindViewById(Resource.Id.image_predicted) as ImageView;
                if (pauseAnalysis)
                {
                    // If image analysis is in paused state, resume it
                    pauseAnalysis = false;
                    imagePredicted.Visibility = ViewStates.Gone;

                }
                else
                {
                    // Otherwise, pause image analysis and freeze image
                    pauseAnalysis = true;
                    var matrix = new Matrix();
                    matrix.PostRotate((float) imageRotationDegrees);
                    if (isFrontFacing()) matrix.PostScale(-1f, 1f);
                    var uprightImage = Bitmap.CreateBitmap(
                        bitmapBuffer, 0, 0, bitmapBuffer.Width, bitmapBuffer.Height, matrix, true);
                    imagePredicted.SetImageBitmap(uprightImage);
                    imagePredicted.Visibility = ViewStates.Visible;
                }

                // Re-enable camera controls
                v.Enabled = true;
            }
        }

        // Declare and bind preview and analysis use cases
        private void BindCameraUseCases()
        {
            viewFinder = FindViewById(Resource.Id.view_finder) as PreviewView;
            viewFinder.Post(() =>
            {
                var cameraProviderFuture = ProcessCameraProvider.GetInstance(this);
                cameraProviderFuture.AddListener(new Java.Lang.Runnable(() =>
                {
                    // Camera provider is now guaranteed to be available
                    var cameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;

                    // Set up the view finder use case to display camera preview
                    var preview = new Preview.Builder()
                        .SetTargetAspectRatio(AspectRatio.Ratio43)
                        .SetTargetRotation((int)viewFinder.Display.Rotation)
                        .Build();

                    // Set up the image analysis use case which will process frames in real time
                    var imageAnalysis = new ImageAnalysis.Builder()
                        .SetTargetAspectRatio(AspectRatio.Ratio43)
                        .SetTargetRotation((int)viewFinder.Display.Rotation)
                        .SetBackpressureStrategy(ImageAnalysis.StrategyKeepOnlyLatest)
                        .Build();

                    frameCounter = 0;
                    lastFpsTimestamp = Java.Lang.JavaSystem.CurrentTimeMillis();
                    converter = new YuvToRgbConverter(this);

                    imageAnalysis.SetAnalyzer(executor, this);

                    // Create a new camera selector each time, enforcing lens facing
                    var cameraSelector = new CameraSelector.Builder().RequireLensFacing(lensFacing).Build();

                    // Apply declared configs to CameraX using the same lifecycle owner
                    cameraProvider.UnbindAll();
                    var camera = cameraProvider.BindToLifecycle(
                        (ILifecycleOwner) this, cameraSelector, preview, imageAnalysis);

                    // Use the camera object to link our preview use case with the view
                    preview.SetSurfaceProvider(viewFinder.CreateSurfaceProvider());

                }), ContextCompat.GetMainExecutor(this));
            });
        }

        public void Analyze(IImageProxy image)
        {
            if (bitmapBuffer == null)
            {
                // The image rotation and RGB image buffer are initialized only once
                // the analyzer has started running
                imageRotationDegrees = image.ImageInfo.RotationDegrees;
                bitmapBuffer = Bitmap.CreateBitmap(
                    image.Width, image.Height, Bitmap.Config.Argb8888);
            }

            // Early exit: image analysis is in paused state
            if (pauseAnalysis)
            {
                image.Close();
                return;
            }

            // Convert the image to RGB and place it in our shared buffer
            converter.YuvToRgb(image.Image, bitmapBuffer);
            image.Close();

            // Center crop the image to the largest square possible
            int x = 0;
            int y = 0;
            int w = bitmapBuffer.Width;
            int h = bitmapBuffer.Height;
            if (w > h)
            {
                x += (w - h) / 2;
                w -= (w - h);
            }
            else
            {
                y += (h - w) / 2;
                h -= (h - w); ;
            }

            // Resize using Bilinear or Nearest neighbour
            bool bilinear = true;
            Matrix matrix = new Matrix();
            matrix.PostScale((float) tfInputSize.Width / w,
                             (float) tfInputSize.Height / h);

            // Rotation counter-clockwise in 90 degree increments
            int numRotation = -imageRotationDegrees / 90;
            matrix.PostRotate(-90 * numRotation);
            Bitmap bitmapInput = Bitmap.CreateBitmap(bitmapBuffer, x, y, w, h, matrix, bilinear);

            // Process the image in Tensorflow
            w = bitmapInput.Width;
            h = bitmapInput.Height;
            bitmapInput.GetPixels(argb8888, 0, w, 0, 0, w, h);
            for (int j = 0, i = 0; i <argb8888.Length; i++)
            {
                rgb888[j++] = (byte) (argb8888[i] >> 16 & 0xff);
                rgb888[j++] = (byte) (argb8888[i] >>  8 & 0xff);
                rgb888[j++] = (byte) (argb8888[i]       & 0xff);
            }
            tfImageBuffer.Rewind();
            tfImageBuffer.Put(rgb888);
            tfImageBuffer.Rewind();

            // Perform the object detection for the current frame
            var predictions = detector.Predict(tfImageBuffer);

            // Report only the top prediction
            ReportPrediction(predictions.OrderBy(p => p.Score).Last());

            // Compute the FPS of the entire pipeline
            var frameCount = 10;
            if (++frameCounter % frameCount == 0)
            {
                frameCounter = 0;
                var now = Java.Lang.JavaSystem.CurrentTimeMillis();
                var delta = now - lastFpsTimestamp;
                var fps = 1000 * (float) frameCount / delta;
                Log.Debug(Tag, "FPS: " + fps.ToString("0.00"));
                lastFpsTimestamp = now;
            }
        }

        private void ReportPrediction(
            ObjectDetectionHelper.ObjectPrediction prediction)
        {
            viewFinder.Post(() =>
            {
                var boxPrediction = FindViewById(Resource.Id.box_prediction);
                var textPrediction = FindViewById(Resource.Id.text_prediction) as TextView;

                // Early exit: if prediction is not good enough, don't report it
                if (prediction == null || prediction.Score < AccuracyThreshold)
                {
                    boxPrediction.Visibility = ViewStates.Gone;
                    textPrediction.Visibility = ViewStates.Gone;
                    return;
                }

                // Location has to be mapped to our local coordinates
                var location = MapOutputCoordinates(prediction.Location);

                // Update the text and UI
                textPrediction.Text = prediction.Score.ToString("0.00") + prediction.Label;
                var layoutParams = boxPrediction.LayoutParameters as ViewGroup.MarginLayoutParams;
                layoutParams.TopMargin = (int) location.Top;
                layoutParams.LeftMargin = (int) location.Left;
                layoutParams.Width =
                    Math.Min(viewFinder.Width, (int) location.Right - (int) location.Left);
                layoutParams.Height =
                    Math.Min(viewFinder.Height, (int) location.Bottom - (int) location.Top);
                boxPrediction.LayoutParameters = layoutParams;

                // Make sure all UI elements are visible
                boxPrediction.Visibility = ViewStates.Visible;
                textPrediction.Visibility = ViewStates.Visible;
            });
        }

        // Helper function used to map the coordinates for objects coming out of
        // the model into the coordinates that the user sees on the screen.
        private RectF MapOutputCoordinates(RectF location)
        {
            // Step 1: map location to the preview coordinates
            var previewLocation = new RectF(
                location.Left * viewFinder.Width,
                location.Top * viewFinder.Height,
                location.Right * viewFinder.Width,
                location.Bottom * viewFinder.Height
            );

            // Step 2: compensate for camera sensor orientation and mirroring
            var isFrontFacing = lensFacing == CameraSelector.LensFacingFront;
            var correctedLocation = isFrontFacing ?
                new RectF(
                    viewFinder.Width - previewLocation.Right,
                    previewLocation.Top,
                    viewFinder.Width - previewLocation.Left,
                    previewLocation.Bottom) :
                previewLocation;

            // Step 3: compensate for 1:1 to 4:3 aspect ratio conversion + small margin
            var margin = 0.1f;
            var requestedRatio = 4f / 3f;
            var midX = (correctedLocation.Left + correctedLocation.Right) / 2f;
            var midY = (correctedLocation.Top + correctedLocation.Bottom) / 2f;
            return viewFinder.Width < viewFinder.Height ?
                new RectF(
                    midX - (1f + margin) * requestedRatio * correctedLocation.Width() / 2f,
                    midY - (1f - margin) * correctedLocation.Height() / 2f,
                    midX + (1f + margin) * requestedRatio * correctedLocation.Width() / 2f,
                    midY + (1f - margin) * correctedLocation.Height() / 2f) :
                new RectF(
                    midX - (1f - margin) * correctedLocation.Width() / 2f,
                    midY - (1f + margin) * requestedRatio * correctedLocation.Height() / 2f,
                    midX + (1f - margin) * correctedLocation.Width() / 2f,
                    midY + (1f + margin) * requestedRatio * correctedLocation.Height() / 2f);
        }

        protected override void OnResume()
        {
            base.OnResume();

            // Request permissions each time the app resumes, since they can be revoked at any time
            if (!HasPermissions(this))
            {
                ActivityCompat.RequestPermissions(
                    this, permissions, permissionsRequestCode);
            }
            else
            {
                BindCameraUseCases();
            }
        }

        public override void OnRequestPermissionsResult(int requestCode, string[] permissions, [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            if (requestCode == permissionsRequestCode && HasPermissions(this))
            {
                BindCameraUseCases();
            }
            else
            {
                Finish(); // If we don't have the required permissions, we can't run
            }
        }

        // Convenience method used to check if all permissions required by this app are granted
        public bool HasPermissions(Context context)
        {
            return permissions.All(x => ContextCompat.CheckSelfPermission(context, x) == Permission.Granted);
        }

        private const string Tag = "CameraXTfLite";

        private const float AccuracyThreshold = 0.5f;
        private const string ModelPath = "coco_ssd_mobilenet_v1_1.0_quant.tflite";
        private const string LabelsPath = "coco_ssd_mobilenet_v1_1.0_labels.txt";
    }
}
