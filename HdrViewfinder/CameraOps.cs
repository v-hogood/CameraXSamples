using Android.Hardware.Camera2;
using Android.OS;
using Android.Util;
using Android.Views;
using Java.Lang;

namespace HdrViewfinder
{
    //
    // Simple interface for operating the camera, with major camera operations
    // all performed on a background handler thread.
    //
    public class CameraOps
    {
        private const string Tag = "CameraOps";

        public const long CameraCloseTimeout = 2000; // ms

        private CameraManager mCameraManager;
        private CameraDevice mCameraDevice;
        private CameraCaptureSession mCameraSession;
        private List<Surface> mSurfaces;

        private ConditionVariable mCloseWaiter = new ConditionVariable();

        private HandlerThread mCameraThread;
        private Handler mCameraHandler;

        private ErrorDisplayer mErrorDisplayer;

        private CameraReadyListener mReadyListener;
        private Handler mReadyHandler;

        //
        // Create a new camera ops thread.
        //
        // @param errorDisplayer listener for displaying error messages
        // @param readyListener  listener for notifying when camera is ready for requests
        // @param readyHandler   the handler for calling readyListener methods on
        //
        public CameraOps(CameraManager manager, ErrorDisplayer errorDisplayer,
                         CameraReadyListener readyListener, Handler readyHandler)
        {
            mCameraThread = new HandlerThread("CameraOpsThread");
            mCameraThread.Start();

            if (manager == null || errorDisplayer == null ||
                readyListener == null || readyHandler == null)
            {
                throw new IllegalArgumentException("Need valid displayer, listener, handler");
            }

            mCameraManager = manager;
            mErrorDisplayer = errorDisplayer;
            mReadyListener = readyListener;
            mReadyHandler = readyHandler;

            mCloseCameraRunnable = new Runnable(() =>
            {
                if (mCameraDevice != null)
                {
                    mCameraDevice.Close();
                }
                mCameraDevice = null;
                mCameraSession = null;
                mSurfaces = null;
            });

            mCameraSessionListener = new CameraSessionListener(this);

            mCameraDeviceListener = new CameraDeviceListener(this);
        }

        //
        // Open the first back-facing camera listed by the camera manager.
        // Displays a dialog if it cannot open a camera.
        //
        public void OpenCamera(string cameraId)
        {
            mCameraHandler = new Handler(mCameraThread.Looper);

            mCameraHandler.Post(() =>
            {
                if (mCameraDevice != null)
                {
                    throw new IllegalStateException("Camera already open");
                }
                try
                {
                    mCameraManager.OpenCamera(cameraId, mCameraDeviceListener, mCameraHandler);
                }
                catch (CameraAccessException e)
                {
                    string errorMessage = mErrorDisplayer.GetErrorString(e);
                    mErrorDisplayer.ShowErrorDialog(errorMessage);
                }
            });
        }

        //
        // Close the camera and wait for the close callback to be called in the camera thread.
        // Times out after @{value CAMERA_CLOSE_TIMEOUT} ms.
        //
        public void CloseCameraAndWait()
        {
            mCloseWaiter.Close();
            mCameraHandler.Post(mCloseCameraRunnable);
            bool closed = mCloseWaiter.Block(CameraCloseTimeout);
            if (!closed)
            {
                Log.Error(Tag, "Timeout closing camera");
            }
        }

        private Runnable mCloseCameraRunnable;

        //
        // Set the output Surfaces, and finish configuration if otherwise ready.
        //
        public void SetSurfaces(List<Surface> surfaces)
        {
            mCameraHandler.Post(new Runnable(() =>
            {
                mSurfaces = surfaces;
                StartCameraSession();
            }));
        }

        //
        // Get a request builder for the current camera.
        //
        public CaptureRequest.Builder CreateCaptureRequest(CameraTemplate template)
        {
            CameraDevice device = mCameraDevice;
            if (device == null)
            {
                throw new IllegalStateException("Can't get requests when no camera is open");
            }
            return device.CreateCaptureRequest(template);
        }

        //
        // Set a repeating request.
        //
        public void SetRepeatingRequest(CaptureRequest request,
                                        CameraCaptureSession.CaptureCallback listener,
                                        Handler handler)
        {
            mCameraHandler.Post(new Runnable(() =>
            {
                try
                {
                    mCameraSession.SetRepeatingRequest(request, listener, handler);
                }
                catch (CameraAccessException e)
                {
                    string errorMessage = mErrorDisplayer.GetErrorString(e);
                    mErrorDisplayer.ShowErrorDialog(errorMessage);
                }
            }));
        }

        //
        // Set a repeating burst.
        //
        public void SetRepeatingBurst(List<CaptureRequest> requests,
                                      CameraCaptureSession.CaptureCallback listener,
                                      Handler handler)
        {
            mCameraHandler.Post(new Runnable(() =>
            {
                try
                {
                    mCameraSession.SetRepeatingBurst(requests, listener, handler);
                }
                catch (CameraAccessException e)
                {
                    string errorMessage = mErrorDisplayer.GetErrorString(e);
                    mErrorDisplayer.ShowErrorDialog(errorMessage);
                }
            }));
        }

        //
        // Configure the camera session.
        //
        private void StartCameraSession()
        {
            // Wait until both the camera device is open and the SurfaceView is ready
            if (mCameraDevice == null || mSurfaces == null) return;

            try
            {
#pragma warning disable CA1422
                mCameraDevice.CreateCaptureSession(
                    mSurfaces, mCameraSessionListener, mCameraHandler);
#pragma warning restore CA1422
            }
            catch (CameraAccessException e)
            {
                string errorMessage = mErrorDisplayer.GetErrorString(e);
                mErrorDisplayer.ShowErrorDialog(errorMessage);
                mCameraDevice.Close();
                mCameraDevice = null;
            }
        }

        //
        // Main listener for camera session events
        // Invoked on mCameraThread
        //
        private CameraSessionListener mCameraSessionListener;

        public class CameraSessionListener : CameraCaptureSession.StateCallback
        {
            CameraOps mParent;

            public CameraSessionListener(CameraOps parent)
            {
                mParent = parent;
            }

            public override void OnConfigured(CameraCaptureSession session)
            {
                mParent.mCameraSession = session;
                mParent.mReadyHandler.Post(new Runnable(() =>
                {
                    // This can happen when the screen is turned off and turned back on.
                    if (null == mParent.mCameraDevice)
                    {
                        return;
                    }

                    mParent.mReadyListener.OnCameraReady();
                }));
            }

            public override void OnConfigureFailed(CameraCaptureSession session)
            {
                mParent.mErrorDisplayer.ShowErrorDialog("Unable to configure the capture session");
                mParent.mCameraDevice.Close();
                mParent.mCameraDevice = null;
            }
        };

        //
        // Main listener for camera device events.
        // Invoked on mCameraThread
        //
        private CameraDevice.StateCallback mCameraDeviceListener;

        public class CameraDeviceListener : CameraDevice.StateCallback
        {
            CameraOps mParent;

            public CameraDeviceListener(CameraOps parent)
            {
                mParent = parent;
            }

            public override void OnOpened(CameraDevice camera)
            {
                mParent.mCameraDevice = camera;
                mParent.StartCameraSession();
            }

            public override void OnClosed(CameraDevice camera)
            {
                mParent.mCloseWaiter.Open();
            }

            public override void OnDisconnected(CameraDevice camera)
            {
                mParent.mErrorDisplayer.ShowErrorDialog("The camera device has been disconnected.");
                camera.Close();
                mParent.mCameraDevice = null;
            }

            public override void OnError(CameraDevice camera, CameraError error)
            {
                mParent.mErrorDisplayer.ShowErrorDialog("The camera encountered an error:" + error);
                camera.Close();
                mParent.mCameraDevice = null;
            }
        }

        //
        // Simple listener for main code to know the camera is ready for requests, or failed to
        // start.
        //
        public interface CameraReadyListener
        {
            void OnCameraReady();
        }

        //
        // Simple listener for displaying error messages
        //
        public interface ErrorDisplayer
        {
            void ShowErrorDialog(string errorMessage);

            string GetErrorString(CameraAccessException e);
        }
    }
}
