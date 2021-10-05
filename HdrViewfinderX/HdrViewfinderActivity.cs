using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Renderscripts;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Camera.Camera2.InterOp;
using AndroidX.Camera.Core;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.App;
using AndroidX.Core.Content;
using AndroidX.Core.Util;
using AndroidX.Lifecycle;
using Google.Android.Material.Snackbar;
using Google.Common.Util.Concurrent;
using Java.Lang;
using Java.Util.Concurrent;

namespace HdrViewfinder
{
    // A small demo of advanced camera functionality with the Android camera2 API.

    // <p>This demo implements a real-time high-dynamic-range camera viewfinder,
    // by alternating the sensor's exposure time between two exposure values on even and odd
    // frames, and then compositing together the latest two frames whenever a new frame is
    // captured.</p>

    // <p>The demo has three modes: Regular auto-exposure viewfinder, split-screen manual exposure,
    // and the fused HDR viewfinder.  The latter two use manual exposure controlled by the user,
    // by swiping up/down on the right and left halves of the viewfinder.  The left half controls
    // the exposure time of even frames, and the right half controls the exposure time of odd frames.
    // </p>

    // <p>In split-screen mode, the even frames are shown on the left and the odd frames on the right,
    // so the user can see two different exposures of the scene simultaneously.  In fused HDR mode,
    // the even/odd frames are merged together into a single image.  By selecting different exposure
    // values for the even/odd frames, the fused image has a higher dynamic range than the regular
    // viewfinder.</p>

    // <p>The HDR fusion and the split-screen viewfinder processing is done with RenderScript; as is the
    // necessary YUV->RGB conversion. The camera subsystem outputs YUV images naturally, while the GPU
    // and display subsystems generally only accept RGB data.  Therefore, after the images are
    // fused/composited, a standard YUV->RGB color transform is applied before the the data is written
    // to the output Allocation. The HDR fusion algorithm is very simple, and tends to result in
    // lower-contrast scenes, but has very few artifacts and can run very fast.</p>

    // <p>Data is passed between the subsystems (camera, RenderScript, and display) using the
    // Android {@link android.view.Surface} class, which allows for zero-copy transport of large
    // buffers between processes and subsystems.</p>

    [Activity(Name = "com.example.android.hdrviewfinder.HdrViewfinderActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class HdrViewfinderActivity : AppCompatActivity,
        View.IOnClickListener, View.IOnTouchListener, TextureView.ISurfaceTextureListener, Preview.ISurfaceProvider, IConsumer
    {
        private const string Tag = "HdrViewfinderDemo";

        private const string FragmentDialog = "dialog";

        private const int RequestPermissionsRequestCode = 34;

        //
        // View for the camera preview.
        //
        private PreviewView mPreviewView;
        private TextureView mTextureView;

        //
        // Root view of this activity.
        //
        private View rootView;

        //
        // This shows the current mode of the app.
        //
        private TextView mModeText;

        // These show lengths of exposure for even frames, exposure for odd frames, and auto exposure.
        private TextView mEvenExposureText, mOddExposureText, mAutoExposureText;

        private IExecutorService mExecutor;

        private ICameraInfo mCameraInfo;
        private ICameraControl mCameraControl;

        private LensFacing mLensFacing = LensFacing.Back;
        private Size mPreviewSize;
        private bool mSurfaceTextureUpdated;

        RenderScript mRS;
        ViewfinderProcessor mProcessor;
        ProcessCameraProvider mCameraProvider;

        private int mRenderMode = ViewfinderProcessor.ModeNormal;

        private int mOddExposure = 0;
        private int mEvenExposure = 0;
        private int mAutoExposure = 0;

        private GestureDetector mGestureDetector;

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.main);

            Window.AddFlags(WindowManagerFlags.Fullscreen);

            mViewListener = new ViewListener(this);

            mCaptureCallback = new CaptureCallback(this);

            rootView = FindViewById(Resource.Id.panels);

            mPreviewView = (PreviewView)FindViewById(Resource.Id.preview);
            mPreviewView.SetImplementationMode(PreviewView.ImplementationMode.Compatible);
            mPreviewView.SetScaleType(PreviewView.ScaleType.FitCenter);
            mPreviewView.SetOnTouchListener(this);
            mGestureDetector = new GestureDetector(this, mViewListener);

            ImageButton infoButton = (ImageButton)FindViewById(Resource.Id.info_button);
            infoButton.SetOnClickListener(this);

            Button helpButton = (Button)FindViewById(Resource.Id.help_button);
            helpButton.SetOnClickListener(this);

            mModeText = (TextView)FindViewById(Resource.Id.mode_label);
            mEvenExposureText = (TextView)FindViewById(Resource.Id.even_exposure);
            mOddExposureText = (TextView)FindViewById(Resource.Id.odd_exposure);
            mAutoExposureText = (TextView)FindViewById(Resource.Id.auto_exposure);

            mExecutor = Executors.NewSingleThreadExecutor();

            mRS = RenderScript.Create(this);
        }

        protected override void OnResume()
        {
            base.OnResume();

            // When permissions are revoked the app is restarted so onCreate is sufficient to check for
            // permissions core to the Activity's functionality.
            if (!CheckCameraPermissions())
            {
                RequestCameraPermissions();
            }
            else
            {
                FindAndOpenCamera();
            }
        }

        protected override void OnPause()
        {
            base.OnPause();

            // Wait until camera is closed to ensure the next application can open it
            if (mCameraProvider != null)
            {
                mCameraProvider.UnbindAll();
                mCameraProvider = null;
            }
        }

        private GestureDetector.IOnGestureListener mViewListener;

        public class ViewListener : GestureDetector.SimpleOnGestureListener
        {
            private HdrViewfinderActivity mParent;

            public ViewListener(HdrViewfinderActivity parent)
            {
                mParent = parent;
            }

            public override bool OnDown(MotionEvent e)
            {
                return true;
            }

            public override bool OnSingleTapUp(MotionEvent e)
            {
                mParent.SwitchRenderMode(1);
                return true;
            }

            public override bool OnScroll(MotionEvent e1, MotionEvent e2, float distanceX, float distanceY)
            {
                float xPosition = e1.GetAxisValue(Axis.X);
                float yPosition = e1.GetAxisValue(Axis.Y);
                float width = mParent.mPreviewView.Width;
                float height = mParent.mPreviewView.Height;

                float xPosNorm = xPosition / width;
                float yPosNorm = yPosition / height;

                bool isPortrait =
                    mParent.mPreviewView.Display.Rotation == SurfaceOrientation.Rotation0 ||
                    mParent.mPreviewView.Display.Rotation == SurfaceOrientation.Rotation180;
                int step = isPortrait ?
                    (distanceX < -Math.Abs(distanceY) ? 1 : distanceX > Math.Abs(distanceY) ? -1 : 0) :
                    (distanceY < -Math.Abs(distanceX) ? -1 : distanceY > Math.Abs(distanceX) ? 1 : 0);

                // Even on left, odd on right
                Range exposureRange = mParent.mCameraInfo.ExposureState.ExposureCompensationRange;
                if (mParent.mRenderMode == ViewfinderProcessor.ModeNormal)
                {
                    mParent.mAutoExposure += step;
                    mParent.mAutoExposure = Math.Max((exposureRange.Lower as Integer).IntValue(),
                        Math.Min((exposureRange.Upper as Integer).IntValue(), mParent.mAutoExposure));
                }
                else if (isPortrait && yPosNorm > 0.5 || !isPortrait && xPosNorm > 0.5)
                {
                    mParent.mOddExposure += step;
                    mParent.mOddExposure = Math.Max(mParent.mEvenExposure,
                        Math.Min((exposureRange.Upper as Integer).IntValue(), mParent.mOddExposure));

                }
                else
                {
                    mParent.mEvenExposure += step;
                    mParent.mEvenExposure = Math.Min(mParent.mOddExposure,
                        Math.Max((exposureRange.Lower as Integer).IntValue(), mParent.mEvenExposure));
                }

                return true;
            }
        }

        public bool OnTouch(View view, MotionEvent motionEvent)
        {
            return mGestureDetector != null && mGestureDetector.OnTouchEvent(motionEvent);
        }

        //
        // Show help dialogs.
        //
        public void OnClick(View v)
        {
            MessageDialogFragment.newInstance(v.Id == Resource.Id.help_button ?
                Resource.String.help_text : Resource.String.intro_message)
                .Show(SupportFragmentManager, FragmentDialog);
        }

        //
        // Return the current state of the camera permissions.
        //
        private bool CheckCameraPermissions()
        {
            var permissionState = ActivityCompat.CheckSelfPermission(this, Manifest.Permission.Camera);

            // Check if the Camera permission is already available.
            if (permissionState != Permission.Granted)
            {
                // Camera permission has not been granted.
                Log.Info(Tag, "CAMERA permission has NOT been granted.");
                return false;
            }
            else
            {
                // Camera permissions are available.
                Log.Info(Tag, "CAMERA permission has already been granted.");
                return true;
            }
        }

        private void RequestCameraPermissions()
        {
            // Provide an additional rationale to the user. This would happen if the user denied the
            // request previously, but didn't check the "Don't ask again" checkbox.
            if (ActivityCompat.ShouldShowRequestPermissionRationale(this, Manifest.Permission.Camera))
            {
                Log.Info(Tag, "Displaying camera permission rationale to provide additional context.");
                Snackbar.Make(rootView, Resource.String.camera_permission_rationale, Snackbar
                        .LengthIndefinite)
                        .SetAction(Resource.String.ok, (View view) =>
                        {
                            // Request Camera permission
                            ActivityCompat.RequestPermissions(this,
                                new string[] { Manifest.Permission.Camera },
                                RequestPermissionsRequestCode);
                        })
                        .Show();
            }
            else
            {
                Log.Info(Tag, "Requesting camera permission");
                // Request Camera permission. It's possible this can be auto answered if device policy
                // sets the permission in a given state or the user denied the permission
                // previously and checked "Never ask again".
                ActivityCompat.RequestPermissions(this,
                    new string[]{Manifest.Permission.Camera},
                    RequestPermissionsRequestCode);
            }
        }

        //
        // Callback received when a permissions request has been completed.
        //
        public override void OnRequestPermissionsResult(int requestCode, string[] permissions,
            [GeneratedEnum] Android.Content.PM.Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);
            Xamarin.Essentials.Platform.OnRequestPermissionsResult(requestCode, permissions, grantResults);

            Log.Info(Tag, "OnRequestPermissionResult");
            if (requestCode == RequestPermissionsRequestCode)
            {
                if (grantResults.Length <= 0)
                {
                    // If user interaction was interrupted, the permission request is cancelled and you
                    // receive empty arrays.
                    Log.Info(Tag, "User interaction was cancelled.");
                }
                else if (grantResults[0] == Permission.Granted)
                {
                    // Permission was granted.
                    FindAndOpenCamera();
                }
                else
                {
                    // Permission denied.

                    // In this Activity we've chosen to notify the user that they
                    // have rejected a core permission for the app since it makes the Activity useless.
                    // We're communicating this message in a Snackbar since this is a sample app, but
                    // core permissions would typically be best requested during a welcome-screen flow.

                    // Additionally, it is important to remember that a permission might have been
                    // rejected without asking the user for permission (device policy or "Never ask
                    // again" prompts). Therefore, a user interface affordance is typically implemented
                    // when permissions are denied. Otherwise, your app could appear unresponsive to
                    // touches or interactions which have required permissions.
                    Snackbar.Make(rootView, Resource.String.camera_permission_denied_explanation, Snackbar
                            .LengthIndefinite)
                            .SetAction(Resource.String.settings, (View view) =>
                            {
                                // Build intent that displays the App settings screen.
                                Intent intent = new Intent();
                                intent.SetAction(Settings.ActionApplicationDetailsSettings);
                                Uri uri = Uri.FromParts("package", Application.Context.PackageName, null);
                                intent.SetData(uri);
                                intent.SetFlags(ActivityFlags.NewTask);
                                StartActivity(intent);
                            })
                            .Show();
                }
            }
        }

        private void FindAndOpenCamera()
        {
            bool cameraPermissions = CheckCameraPermissions();
            if (!cameraPermissions)
            {
                return;
            }
            string errorMessage = "Unknown error";
            bool foundCamera = false;
            IListenableFuture cameraProviderFuture = ProcessCameraProvider.GetInstance(this);
            cameraProviderFuture.AddListener(new Runnable(() =>
            {
                // Camera provider is now guaranteed to be available
                mCameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;
                try
                {
                    // Find first back-facing camera that has necessary capability.
                    CameraSelector cameraSelector = new CameraSelector.Builder().RequireLensFacing((int)mLensFacing).Build();

                    // Find a good size for output - largest 4:3 aspect ratio that's less than 720p
                    Preview.Builder previewBuilder = new Preview.Builder()
                        .SetTargetAspectRatio(AspectRatio.Ratio43);
                    Camera2Interop.Extender previewExtender = new Camera2Interop.Extender(previewBuilder);
                    previewExtender.SetSessionCaptureCallback(mCaptureCallback);
                    Preview preview = previewBuilder.Build();

                    // Must unbind the use-cases before rebinding them
                    mCameraProvider.UnbindAll();
                    ICamera camera = mCameraProvider.BindToLifecycle(
                        this as ILifecycleOwner, cameraSelector, preview);
                      
                    if (camera != null)
                    {
                        // Found suitable camera - get info, open, and set up outputs
                        mCameraInfo = camera.CameraInfo;
                        mCameraControl = camera.CameraControl;
                        preview.SetSurfaceProvider(this);
                        mSurfaceTextureUpdated = false;
                        foundCamera = true;
                    }
                    if (!foundCamera)
                    {
                        errorMessage = GetString(Resource.String.camera_no_good);
                    }

                    SwitchRenderMode(0);
                }
                catch (CameraAccessException e)
                {
                    errorMessage = GetErrorString(e);
                }
                if (!foundCamera)
                {
                    ShowErrorDialog(errorMessage);
                }
            }), ContextCompat.GetMainExecutor(this));
        }

        private void SwitchRenderMode(int direction)
        {
            if (mCameraProvider != null)
            {
                mRenderMode = (mRenderMode + direction) % 3;

                mModeText.Text = Resources.GetStringArray(Resource.Array.mode_label_array)[mRenderMode];

                if (mProcessor != null)
                {
                    mProcessor.SetRenderMode(mRenderMode);
                }
            }
        }

        //
        // Listener for completed captures
        // Invoked on UI thread
        //
        private CaptureCallback mCaptureCallback;

        public class CaptureCallback : CameraCaptureSession.CaptureCallback
        {
            private HdrViewfinderActivity mParent;

            public CaptureCallback(HdrViewfinderActivity parent)
            {
                mParent = parent;
            }

            public override void OnCaptureStarted(CameraCaptureSession session,
                                                  CaptureRequest request,
                                                  long timeStamp, long frameNumber)
            {
                if (mParent.mRenderMode == ViewfinderProcessor.ModeNormal) return;

                if ((frameNumber & 1) == 0)
                {
                    mParent.mCameraControl.SetExposureCompensationIndex(mParent.mOddExposure);
                }
                else
                {
                    mParent.mCameraControl.SetExposureCompensationIndex(mParent.mEvenExposure);
                }
            }

            public override void OnCaptureCompleted(CameraCaptureSession session,
                                                    CaptureRequest request,
                                                    TotalCaptureResult result)
            {
                // Only update UI every so many frames
                // Use an odd number here to ensure both even and odd exposures get an occasional update
                long frameNumber = result.FrameNumber;
                if (frameNumber % 3 != 0) return;

                Integer exposureComp = result.Get(CaptureResult.ControlAeExposureCompensation) as Integer;
                if (exposureComp == null)
                {
                    throw new RuntimeException("Cannot get exposure compensation.");
                }

                // Format exposure time nicely
                Rational exposureStep = mParent.mCameraInfo.ExposureState.ExposureCompensationStep;
                string exposureText = (exposureComp.FloatValue() * exposureStep.FloatValue()).ToString("0.00");

                if (mParent.mRenderMode == ViewfinderProcessor.ModeNormal)
                {
                    mParent.mAutoExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = false;
                    mParent.mOddExposureText.Enabled = false;
                    mParent.mAutoExposureText.Enabled = true;

                    if (exposureComp.IntValue() != mParent.mAutoExposure)
                    {
                        mParent.mCameraControl.SetExposureCompensationIndex(mParent.mAutoExposure);
                    }
                }
                else if ((frameNumber & 1) == 0)
                {
                    mParent.mEvenExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = true;
                    mParent.mOddExposureText.Enabled = true;
                    mParent.mAutoExposureText.Enabled = false;
                }
                else
                {
                    mParent.mOddExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = true;
                    mParent.mOddExposureText.Enabled = true;
                    mParent.mAutoExposureText.Enabled = false;
                }
            }
        }

        //
        // Callbacks for ISurfaceProvider
        //
        public void OnSurfaceRequested(SurfaceRequest request)
        {
            if (mPreviewView.Width == 0 || mPreviewView.Height == 0) return;

            mPreviewSize = request.Resolution;
            Log.Info(Tag, "Resolution chosen: " + mPreviewSize);

            // Configure processing
            mProcessor = new ViewfinderProcessor(mRS, mPreviewSize);

            request.ProvideSurface(mProcessor.GetInputHdrSurface(), mExecutor, this);

            mPreviewView.SurfaceProvider.OnSurfaceRequested(request);

            mTextureView = mPreviewView.GetChildAt(0) as TextureView;
            mTextureView.SurfaceTextureListener = this;
        }

        public void Accept(Object resultObject)
        {
            SurfaceRequest.Result result = resultObject as SurfaceRequest.Result;
            Log.Info(Tag, "SurfaceRequest ResultCode: " + result.ResultCode);
        }

        //
        // Callbacks for ISurfaceTextureListener
        //
        public void OnSurfaceTextureAvailable(SurfaceTexture texture, int width, int height)
        {
            // We configure the size of default buffer to be the size of camera preview we want.
            texture.SetDefaultBufferSize(mPreviewSize.Height, mPreviewSize.Width);

            mProcessor.SetOutputSurface(new Surface(texture));
        }

        public void OnSurfaceTextureSizeChanged(SurfaceTexture texture, int width, int height)
        {
            mProcessor.SetOutputSurface(new Surface(texture));
        }

        public void OnSurfaceTextureUpdated(SurfaceTexture texture)
        {
            if (!mSurfaceTextureUpdated)
            {
                float centerX = mPreviewSize.Width / 2;
                float centerY = mPreviewSize.Height / 2;

                Matrix matrix = mTextureView.GetTransform(null);
                if (mLensFacing == LensFacing.Front)
                {
                    // SurfaceView/TextureView automatically mirrors the Surface for front camera, which
                    // needs to be compensated by mirroring the Surface around the upright direction of the
                    // output image.
                    if (mCameraInfo.SensorRotationDegrees == 90 ||
                        mCameraInfo.SensorRotationDegrees == 270)
                    {
                        // If the rotation is 90/270, the Surface should be flipped vertically.
                        //   +---+     90 +---+  270 +---+
                        //   | ^ | -->    | < |      | > |
                        //   +---+        +---+      +---+
                        matrix.PreScale(1F, -1F, centerX, centerY);
                    }
                    else
                    {
                        // If the rotation is 0/180, the Surface should be flipped horizontally.
                        //   +---+      0 +---+  180 +---+
                        //   | ^ | -->    | ^ |      | v |
                        //   +---+        +---+      +---+
                        matrix.PreScale(-1F, 1F, centerX, centerY);
                    }
                }
                if (mCameraInfo.SensorRotationDegrees == 90 ||
                    mCameraInfo.SensorRotationDegrees == 270)
                {
                    float aspect = centerX / centerY;
                    matrix.PostScale(1f / aspect, aspect, centerX, centerY);
                }
                matrix.PostRotate(mCameraInfo.SensorRotationDegrees, centerX, centerY);
                mTextureView.SetTransform(matrix);
                mSurfaceTextureUpdated = true;
            }
        }

        public bool OnSurfaceTextureDestroyed(SurfaceTexture texture)
        {
            mProcessor.SetOutputSurface(null);
            return true;
        }

        //
        // Utility methods
        //
        public void ShowErrorDialog(string errorMessage)
        {
            MessageDialogFragment.newInstance(errorMessage)
                .Show(SupportFragmentManager, FragmentDialog);
        }

        public string GetErrorString(CameraAccessException e)
        {
            string errorMessage;
            switch (e.Reason)
            {
                case CameraAccessErrorType.Disabled:
                    errorMessage = GetString(Resource.String.camera_disabled);
                    break;
                case CameraAccessErrorType.Disconnected:
                    errorMessage = GetString(Resource.String.camera_disconnected);
                    break;
                case CameraAccessErrorType.Error:
                    errorMessage = GetString(Resource.String.camera_error);
                    break;
                default:
                    errorMessage = GetString(Resource.String.camera_unknown) + ": " + e.Message;
                    break;
            }
            return errorMessage;
        }
    }
}
