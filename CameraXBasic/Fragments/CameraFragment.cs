using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.Annotation;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Hardware.Display;
using Android.Media;
using Android.Net;
using Android.OS;
using Android.Util;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.ConstraintLayout;
using AndroidX.Core.Content;
using AndroidX.Core.Net;
using AndroidX.Core.View;
using AndroidX.Fragment.App;
using AndroidX.Lifecycle;
using AndroidX.LocalBroadcastManager.Content;
using AndroidX.Navigation;
using CameraXBasic.Utils;
using Com.Bumptech.Glide;
using AndroidX.Camera;
using AndroidX.Camera.Core;
using AndroidX.Camera.View;
using AndroidX.Camera.Camera2;
using AndroidX.Camera.Lifecycle;
using Bumptech.Glide.Util;
using Java.Security;
using Bumptech.Glide.Load;
using Uri = Android.Net.Uri;
using Bumptech.Glide;
using Bumptech.Glide.Request;
using AndroidX.ConstraintLayout.Widget;
using System.Threading.Tasks;
using Android.Content;

namespace CameraXBasic.Fragments
{
    // Main fragment for this app. Implements all camera operations including:
    // - Viewfinder
    // - Photo taking
    // - Image analysis
    public class CameraFragment : Fragment
    {
        private const string Tag = "CameraXBasic";
        private const string Filename = "yyyy-MM-dd-HH-mm-ss-SSS";
        private const string PhotoExtension = ".jpg";
        private const double Ratio4To3Value = 4.0 / 3.0;
        private const double Ratio16To9Value = 16.0 / 9.0;

        private ConstraintLayout container;
        private PreviewView viewFinder;
        private Java.IO.File outputDirectory;
        private LocalBroadcastManager broadcastManager;

        private int displayId = -1;
        private int lensFacing = CameraSelector.LensFacingBack;
        private Preview preview;
        private ImageCapture imageCapture;
        private ImageAnalysis imageAnalyzer;
        private Camera camera;
        private ProcessCameraProvider cameraProvider;

        private DisplayManager displayManager => RequireContext().GetSystemService(Context.DisplayService);

        // Blocking camera operations are performed using this task
        private Task cameraExecutor;

        private readonly VolumeDownReceiver volumeDownReceiver = new volumeDownReceiver(this);
        private readonly DisplayListener displayListener = new DisplayListener(this);

        public override void OnResume()
        {
            base.OnResume();

            // Make sure that all permissions are still present, since the
            // user could have removed them while the app was in paused state.
            if (!PermissionsFragment.HasPermissions(RequireContext()))
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).Navigate(CameraFragmentDirections.ActionCameraToPermissions());
            }
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            inflater.Inflate(Resource.Layout.fragment_camera, container, false);
            return base.OnCreateView(inflater, container, savedInstanceState);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            container = view as ConstraintLayout;
            viewFinder = container.FindViewById<PreviewView>(Resource.Id.view_finder);

            // Initialize our background task

            broadcastManager = LocalBroadcastManager.GetInstance(view.Context);

            // Set up the intent filter that will receive events from our main activity
            var filter = new IntentFilter();
            filter.AddAction("key_event_action");
            broadcastManager.RegisterReceiver(volumeDownReceiver, filter);

            // Every time the orientation of device changes, update rotation for use cases
            displayManager.RegisterDisplayListener(displayListener, null);

            // Determine the output directory
            outputDirectory = MainActivity.GetOutputDirectory(requireContext())

        // Wait for the views to be properly laid out
            viewFinder.Post(() =>
            {
                // Keep track of the display in which this view is attached
                displayId = viewFinder.display.displayId

            // Build UI controls
                updateCameraUi()

            // Set up the camera and its use cases
                setUpCamera()
        });
        }

        public override void OnDestroyView()
        {
            base.OnDestroyView();

            // Shut down our background executor
            cameraExecutor.Shutdown();

            // Unregister the broadcast receivers and listeners
            broadcastManager.UnregisterReceiver(volumeDownReceiver);
            displayManager.UnregisterDisplayListener(displayListener);
        }

        private void SetGalleryThumbnail(Uri uri)
        {
            // Reference of the view that holds the gallery thumbnail
            ImageButton thumbnail = container.FindViewById<ImageButton>(Resource.Id.photo_view_button);

            // Run the operations in the view's thread
            thumbnail.Post(() =>
            {
                // Remove thumbnail padding
                thumbnail.SetPadding(Resources.GetDimension(Resource.Dimension.stroke_small));

                // Load thumbnail into circular button using Glide
                Glide.With(thumbnail)
                    .Load(uri)
                    .Apply(RequestOptions.CircleCropTransform())
                    .Into(thumbnail);
            });
        }

        // Volume down button receiver used to trigger shutter
        private class VolumeDownReceiver : BroadcastReceiver
        {
            private CameraFragment parent;

            public VolumeDownReceiver(CameraFragment fragment)
            {
                parent = fragment;
            }

            public override void OnReceive(Context context, Intent intent)
            {
                // When the volume down button is pressed, simulate a shutter button click
                if (intent.GetIntArrayExtra(KeyEvent.KeyCodeFromString("extra"), Keycode.Unknown) == Keycode.VolumeDown)
                {
                    var shutter = parent.container.FindViewById<ImageButton>(Resource.Id.camera_capture_button);
                    shutter.SimulateClick();
                }
            }
        }

        // We need a display listener for orientation changes that do not trigger a configuration
     // change, for example if we choose to override config change in manifest or for 180-degree
     // orientation changes.
        private class DisplayListener : DisplayManager.IDisplayListener
        {
            private CameraFragment parent;

            public DisplayListener(CameraFragment fragment)
            {
                parent = fragment;
            }

            public void OnDisplayAdded(int displayId)
            {
            }

            public void OnDisplayChanged(int displayId)
            {
                if (displayId == parent.displayId)
                {
                    Log.Debug(Tag, $"Rotation changed: {view.Display.Rotation}");
                    imageCapture.TargetRotation = view.Display.Rotation;
                    imageAnalyzer.TargetRotation = view.Display.rotation;
            }
            }

            public void OnDisplayRemoved(int displayId)
            {
            }
        }


        }
}
