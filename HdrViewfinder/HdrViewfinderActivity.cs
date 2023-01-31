using System.Collections.Generic;
using Android;
using Android.App;
using Android.Content;
using Android.Content.PM;
using Android.Graphics;
using Android.Hardware.Camera2;
using Android.Hardware.Camera2.Params;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Renderscripts;
using Android.Runtime;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.Core.App;
using Google.Android.Material.Snackbar;
using Java.Lang;

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

    [Activity(Name = "com.android.example.hdrviewfinder.HdrViewfinderActivity", Label = "@string/app_name", Theme = "@style/AppTheme", MainLauncher = true)]
    public class HdrViewfinderActivity : AppCompatActivity,
        View.IOnClickListener, ISurfaceHolderCallback, CameraOps.ErrorDisplayer, CameraOps.CameraReadyListener
    {
        private const string Tag = "HdrViewfinderDemo";

        private const string FragmentDialog = "dialog";

        private const int RequestPermissionsRequestCode = 34;

        //
        // View for the camera preview.
        //
        private FixedAspectSurfaceView mPreviewView;

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

        private Handler mUiHandler;

        private CameraCharacteristics mCameraInfo;

        private Surface mPreviewSurface;
        private Surface mProcessingHdrSurface;
        private Surface mProcessingNormalSurface;
        CaptureRequest.Builder mHdrBuilder;
        List<CaptureRequest> mHdrRequests = new List<CaptureRequest>(2);

        CaptureRequest mPreviewRequest;

        RenderScript mRS;
        ViewfinderProcessor mProcessor;
        CameraManager mCameraManager;
        CameraOps mCameraOps;

        private int mRenderMode = ViewfinderProcessor.ModeNormal;

        // Durations in nanoseconds
        private const long MicroSecond = 1000;
        private const long MilliSecond = MicroSecond * 1000;
        private const long OneSecond = MilliSecond * 1000;

        private long mOddExposure = OneSecond / 33;
        private long mEvenExposure = OneSecond / 33;

        private String mOddExposureTag = new String();
        private String mEvenExposureTag = new String();
        private String mAutoExposureTag = new String();

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.main);

            mViewListener = new ViewListener(this);

            mCaptureCallback = new CaptureCallback(this);

            rootView = FindViewById(Resource.Id.panels);

            mPreviewView = (FixedAspectSurfaceView)FindViewById(Resource.Id.preview);
            mPreviewView.Holder.AddCallback(this);
            mPreviewView.SetGestureListener(this, mViewListener);

            Button helpButton = (Button)FindViewById(Resource.Id.help_button);
            helpButton.SetOnClickListener(this);

            mModeText = (TextView)FindViewById(Resource.Id.mode_label);
            mEvenExposureText = (TextView)FindViewById(Resource.Id.even_exposure);
            mOddExposureText = (TextView)FindViewById(Resource.Id.odd_exposure);
            mAutoExposureText = (TextView)FindViewById(Resource.Id.auto_exposure);

            mUiHandler = new Handler(Looper.MainLooper);

            mRS = RenderScript.Create(this);

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

        protected override void OnResume()
        {
            base.OnResume();
        }

        protected override void OnPause()
        {
            base.OnPause();

            // Wait until camera is closed to ensure the next application can open it
            if (mCameraOps != null)
            {
                mCameraOps.CloseCameraAndWait();
                mCameraOps = null;
            }
        }

        public override bool OnCreateOptionsMenu(IMenu menu)
        {
            MenuInflater.Inflate(Resource.Menu.main, menu);
            return base.OnCreateOptionsMenu(menu);
        }

        public override bool OnOptionsItemSelected(IMenuItem item)
        {
            switch(item.ItemId)
            {
                case Resource.Id.info:
                {
                    MessageDialogFragment.newInstance(Resource.String.intro_message)
                        .Show(SupportFragmentManager, FragmentDialog);
                    break;
                }
            }
            return base.OnOptionsItemSelected(item);
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
                if (mParent.mRenderMode == ViewfinderProcessor.ModeNormal) return false;

                float xPosition = e1.GetAxisValue(Axis.X);
                float width = mParent.mPreviewView.Width;
                float height = mParent.mPreviewView.Height;

                float xPosNorm = xPosition / width;
                float yDistNorm = distanceY / height;

                const float AccelerationFactor = 8;
                double scaleFactor = Math.Pow(2f, yDistNorm * AccelerationFactor);

                // Even on left, odd on right
                if (xPosNorm > 0.5)
                {
                    mParent.mOddExposure = (long)(mParent.mOddExposure * scaleFactor);
                }
                else
                {
                    mParent.mEvenExposure = (long)(mParent.mEvenExposure * scaleFactor);
                }

                mParent.SetHdrBurst();

                return true;
            }
        }

        //
        // Show help dialogs.
        //
        public void OnClick(View v)
        {
            MessageDialogFragment.newInstance(Resource.String.help_text)
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

        //
        // Attempt to initialize the camera.
        //
        private void InitializeCamera()
        {
            mCameraManager = (CameraManager)GetSystemService(CameraService);
            if (mCameraManager != null)
            {
                mCameraOps = new CameraOps(mCameraManager,
                    errorDisplayer: this,
                    readyListener: this,
                    readyHandler: mUiHandler);

                mHdrRequests.Add(null);
                mHdrRequests.Add(null);
            }
            else
            {
                Log.Error(Tag, "Couldn't initialize the camera");
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
            Permission[] grantResults)
        {
            base.OnRequestPermissionsResult(requestCode, permissions, grantResults);

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
            InitializeCamera();
            if (mCameraOps != null)
            {
                try
                {
                    // Find first back-facing camera that has necessary capability.
                    string[] cameraIds = mCameraManager.GetCameraIdList();
                    foreach (string id in cameraIds)
                    {
                        CameraCharacteristics info = mCameraManager.GetCameraCharacteristics(id);
                        Integer facing = info.Get(CameraCharacteristics.LensFacing) as Integer;
                        Integer level = info.Get(CameraCharacteristics.InfoSupportedHardwareLevel) as Integer;
                        bool hasFullLevel = level.IntValue() ==
                            (int) InfoSupportedHardwareLevel.Full;

                        int[] capabilities = info
                            .Get(CameraCharacteristics.RequestAvailableCapabilities).ToArray<int>();
                        Integer syncLatency = info.Get(CameraCharacteristics.SyncMaxLatency) as Integer;
                        bool hasManualControl = HasCapability(capabilities,
                            (int) RequestAvailableCapabilities.ManualSensor);
                        bool hasEnoughCapability = syncLatency.IntValue() ==
                            (int) SyncMaxLatency.PerFrameControl;

                        // All these are guaranteed by
                        // CameraCharacteristics.INFO_SUPPORTED_HARDWARE_LEVEL_FULL, but checking
                        // for only the things we care about expands range of devices we can run on.
                        // We want:
                        //  - Back-facing camera
                        //  - Manual sensor control
                        //  - Per-frame synchronization (so that exposure can be changed every frame)
                        if (facing.IntValue() == (int) LensFacing.Back &&
                            (hasFullLevel || hasEnoughCapability))
                        {
                            // Found suitable camera - get info, open, and set up outputs
                            mCameraInfo = info;
                            mCameraOps.OpenCamera(id);
                            ConfigureSurfaces();
                            foundCamera = true;
                            break;
                        }
                    }
                    if (!foundCamera)
                    {
                        errorMessage = GetString(Resource.String.camera_no_good);
                    }
                }
                catch (CameraAccessException e)
                {
                    errorMessage = GetErrorString(e);
                }
            }
            if (!foundCamera)
            {
                ShowErrorDialog(errorMessage);
            }
        }

        private bool HasCapability(int[] capabilities, int capability)
        {
            foreach (int c in capabilities)
            {
                if (c == capability) return true;
            }
            return false;
        }

        private void SwitchRenderMode(int direction)
        {
            if (mCameraOps != null)
            {
                mRenderMode = (mRenderMode + direction) % 3;

                mModeText.Text = Resources.GetStringArray(Resource.Array.mode_label_array)[mRenderMode];

                if (mProcessor != null)
                {
                    mProcessor.SetRenderMode(mRenderMode);
                }
                if (mRenderMode == ViewfinderProcessor.ModeNormal)
                {
                    mCameraOps.SetRepeatingRequest(mPreviewRequest,
                        mCaptureCallback, mUiHandler);
                }
                else
                {
                    SetHdrBurst();
                }
            }
        }

        //
        // Configure the surfaceview and RS processing.
        //
        private void ConfigureSurfaces()
        {
            // Find a good size for output - largest 16:9 aspect ratio that's less than 720p
            const int MaxWidth = 1280;
            const float TargetAspect = 16f / 9f;
            const float AspectTolerance = 0.1f;

            StreamConfigurationMap configs =
                mCameraInfo.Get(CameraCharacteristics.ScalerStreamConfigurationMap) as StreamConfigurationMap;
            if (configs == null)
            {
                throw new RuntimeException("Cannot get available picture/preview sizes.");
            }
            Size[] outputSizes = configs.GetOutputSizes((int) ImageFormatType.Yuv420888);

            Size outputSize = outputSizes[0];
            float outputAspect = (float)outputSize.Width / outputSize.Height;
            foreach (Size candidateSize in outputSizes)
            {
                if (candidateSize.Width > MaxWidth) continue;
                float candidateAspect = (float)candidateSize.Width / candidateSize.Height;
                bool goodCandidateAspect =
                    Math.Abs(candidateAspect - TargetAspect) < AspectTolerance;
                bool goodOutputAspect =
                    Math.Abs(outputAspect - TargetAspect) < AspectTolerance;
                if ((goodCandidateAspect && !goodOutputAspect) ||
                    candidateSize.Width > outputSize.Width)
                {
                    outputSize = candidateSize;
                    outputAspect = candidateAspect;
                }
            }
            Log.Info(Tag, "Resolution chosen: " + outputSize);

            // Configure processing
            mProcessor = new ViewfinderProcessor(mRS, outputSize);
            SetupProcessor();

            // Configure the output view - this will fire surfaceChanged
            mPreviewView.SetAspectRatio(outputAspect);
            mPreviewView.Holder.SetFixedSize(outputSize.Width, outputSize.Height);
        }

        //
        // Once camera is open and output surfaces are ready, configure the RS processing
        // and the camera device inputs/outputs.
        //
        private void SetupProcessor()
        {
            if (mProcessor == null || mPreviewSurface == null) return;

            mProcessor.SetOutputSurface(mPreviewSurface);
            mProcessingHdrSurface = mProcessor.GetInputHdrSurface();
            mProcessingNormalSurface = mProcessor.GetInputNormalSurface();

            List<Surface> cameraOutputSurfaces = new List<Surface>();
            cameraOutputSurfaces.Add(mProcessingHdrSurface);
            cameraOutputSurfaces.Add(mProcessingNormalSurface);

            mCameraOps.SetSurfaces(cameraOutputSurfaces);
        }

        //
        // Start running an HDR burst on a configured camera session
        //
        public void SetHdrBurst()
        {
            mHdrBuilder.Set(CaptureRequest.SensorSensitivity, 1600);
            mHdrBuilder.Set(CaptureRequest.SensorFrameDuration, OneSecond / 30);

            mHdrBuilder.Set(CaptureRequest.SensorExposureTime, mEvenExposure);
            mHdrBuilder.SetTag(mEvenExposureTag);
            mHdrRequests[0] = mHdrBuilder.Build();

            mHdrBuilder.Set(CaptureRequest.SensorExposureTime, mOddExposure);
            mHdrBuilder.SetTag(mOddExposureTag);
            mHdrRequests[1] = mHdrBuilder.Build();

            mCameraOps.SetRepeatingBurst(mHdrRequests, mCaptureCallback, mUiHandler);
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

            public override void OnCaptureCompleted(CameraCaptureSession session,
                                                    CaptureRequest request,
                                                    TotalCaptureResult result)
            {
                // Only update UI every so many frames
                // Use an odd number here to ensure both even and odd exposures get an occasional update
                long frameNumber = result.FrameNumber;
                if (frameNumber % 3 != 0) return;

                Long exposureTime = result.Get(CaptureResult.SensorExposureTime) as Long;
                if (exposureTime == null)
                {
                    throw new RuntimeException("Cannot get exposure time.");
                }

                // Format exposure time nicely
                string exposureText;
                if (exposureTime.LongValue() > OneSecond)
                {
                    exposureText = (exposureTime.DoubleValue() / 1e9).ToString("0.00") + " s";
                }
                else if (exposureTime.LongValue() > MilliSecond)
                {
                    exposureText = (exposureTime.DoubleValue() / 1e6).ToString("0.00") + " ms";
                }
                else if (exposureTime.LongValue() > MicroSecond)
                {
                    exposureText = (exposureTime.DoubleValue() / 1e3).ToString("0.00") + " us";
                }
                else
                {
                    exposureText = (exposureTime.LongValue().ToString() + " ns");
                }

                Object tag = request.Tag;
                Log.Info(Tag, "Exposure: " + exposureText);

                if (tag == mParent.mEvenExposureTag)
                {
                    mParent.mEvenExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = true;
                    mParent.mOddExposureText.Enabled = true;
                    mParent.mAutoExposureText.Enabled = false;
                }
                else if (tag == mParent.mOddExposureTag)
                {
                    mParent.mOddExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = true;
                    mParent.mOddExposureText.Enabled = true;
                    mParent.mAutoExposureText.Enabled = false;
                }
                else
                {
                    mParent.mAutoExposureText.Text = exposureText;

                    mParent.mEvenExposureText.Enabled = false;
                    mParent.mOddExposureText.Enabled = false;
                    mParent.mAutoExposureText.Enabled = true;
                }
            }
        }

        //
        // Callbacks for the FixedAspectSurfaceView
        //

        public void SurfaceChanged(ISurfaceHolder holder, Format format, int width, int height)
        {
            mPreviewSurface = holder.Surface;

            SetupProcessor();
        }

        public void SurfaceCreated(ISurfaceHolder holder)
        {
            // ignored
        }

        public void SurfaceDestroyed(ISurfaceHolder holder)
        {
            mPreviewSurface = null;
        }

        //
        // Callbacks for CameraOps
        //
        public void OnCameraReady()
        {
            // Ready to send requests in, so set them up
            try
            {
                CaptureRequest.Builder previewBuilder =
                    mCameraOps.CreateCaptureRequest(CameraTemplate.Preview);
                previewBuilder.AddTarget(mProcessingNormalSurface);
                previewBuilder.SetTag(mAutoExposureTag);
                mPreviewRequest = previewBuilder.Build();

                mHdrBuilder =
                    mCameraOps.CreateCaptureRequest(CameraTemplate.Preview);
                mHdrBuilder.Set(CaptureRequest.ControlAeMode,
                    (int) ControlAEMode.Off);
                mHdrBuilder.AddTarget(mProcessingHdrSurface);

                SwitchRenderMode(0);
            }
            catch (CameraAccessException e)
            {
                string errorMessage = GetErrorString(e);
                ShowErrorDialog(errorMessage);
            }
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
