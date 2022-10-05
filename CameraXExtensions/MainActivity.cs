﻿using System.Collections.Generic;
using System.Linq;
using Android;
using Android.App;
using Android.Content.PM;
using Android.OS;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.App;
using AndroidX.Camera.Extensions;
using AndroidX.Core.App;
using AndroidX.Lifecycle;
using Java.Lang;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;

//
// Displays the camera preview with camera controls and available extensions. Tapping on the shutter
// button will capture a photo and display the photo.
//
namespace CameraXExtensions
{
    [Activity(Name = "com.android.examples.cameraxextensions.MainActivity", Label = "@string/app_name", Theme = "@style/Theme.CameraXExtensions", MainLauncher = true)]
    public class MainActivity : AppCompatActivity,
        IActivityResultCallback,
        IContinuation,
        IFunction2,
        IFunction3
    {
        private IDictionary<int, int> extensionName = new Dictionary<int, int>
        {
            [ExtensionMode.Auto] = Resource.String.camera_mode_auto,
            [ExtensionMode.Night] = Resource.String.camera_mode_night,
            [ExtensionMode.Hdr] = Resource.String.camera_mode_hdr,
            [ExtensionMode.FaceRetouch] = Resource.String.camera_mode_face_retouch,
            [ExtensionMode.Bokeh] = Resource.String.camera_mode_bokeh,
            [ExtensionMode.None] = Resource.String.camera_mode_none,
        };

        // view model for operating on the camera and capturing a photo
        private CameraExtensionsViewModel cameraExtensionsViewModel;

        // monitors changes in camera permission state
        private IMutableStateFlow permissionState = StateFlowKt.MutableStateFlow(new PermissionState());

        public void OnActivityResult(Object result)
        {
            var isGranted = result as Boolean;
            if (isGranted.BooleanValue())
            {
                LifecycleOwnerKt.GetLifecycleScope(this).Launch(
                    new Function2(() =>
                        permissionState.Emit(new PermissionState.Granted(), this)));
            }
            else
            {
                LifecycleOwnerKt.GetLifecycleScope(this).Launch(
                    new Function2(() =>
                        permissionState.Emit(new PermissionState.Denied(true), this)));
            }
        }

        public ICoroutineContext Context => LifecycleOwnerKt.GetLifecycleScope(this).CoroutineContext;

        public void ResumeWith(Object result) { }

        CameraExtensionsScreen cameraExtensionsScreen;

        ActivityResultLauncher requestPermissionsLauncher;

        public class Function2 : Object, IFunction2
        {
            System.Action action;
            public Function2(System.Action action) => this.action = action;
            public Object Invoke(Object p0, Object p1)
            {
                action();
                return null;
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            Xamarin.Essentials.Platform.Init(this, savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            cameraExtensionsViewModel = new CameraExtensionsViewModel(Application);

            // capture screen abstracts the UI logic and exposes simple functions on how to interact
            // with the UI layer.
            cameraExtensionsScreen = new CameraExtensionsScreen(FindViewById(Resource.Id.root));

            // initialize the permission state flow with the current camera permission status
            permissionState = StateFlowKt.MutableStateFlow(GetCurrentPermissionState());

            requestPermissionsLauncher =
                RegisterForActivityResult(new ActivityResultContracts.RequestPermission(), this);

            LifecycleOwnerKt.GetLifecycleScope(this).Launch(
                new Function2(() =>
                    RepeatOnLifecycleKt.RepeatOnLifecycle(this, Lifecycle.State.Resumed,
                        new Function2(() =>
                            // check the current permission state every time upon the activity is resumed
                            permissionState.Emit(GetCurrentPermissionState(), this)), this)));

            // Consumes actions emitted by the UI and performs the appropriate operation associated with
            // the view model or permission flow.
            // Note that this flow is a shared flow and will not emit the last action unlike the state
            // flows exposed by the view model for consuming UI state.
            LifecycleOwnerKt.GetLifecycleScope(this).Launch(
                new Function2(() =>
                    FlowKt.CollectLatest(cameraExtensionsScreen.Action, this, this)));

            // Consume state emitted by the view model to render the Photo Capture state.
            // Upon collecting this state, the last emitted state will be immediately received.
            LifecycleOwnerKt.GetLifecycleScope(this).Launch(
               new Function2(() =>
                   FlowKt.CollectLatest(cameraExtensionsViewModel.CaptureUiState, this, this)));

            // Because camera state is dependent on the camera permission status, we combine both camera
            // UI state and permission state such that each emission accurately captures the current
            // permission status and camera UI state.
            // The camera permission is always checked to see if it's granted. If it isn't then stop
            // interacting with the camera and display the permission request screen. The user can tap
            // on "Turn On" to request permissions.
            LifecycleOwnerKt.GetLifecycleScope(this).Launch(
                new Function2(() =>
                    FlowKt.CollectLatest(
                        FlowKt.Combine(permissionState, cameraExtensionsViewModel.CameraUiState, this),
                        this, this)));
        }

        public Object Invoke(Object p0, Object p1)
        {
            if (p0 is CameraUiAction)
            {
                var action = p0 as CameraUiAction;
                if (action is CameraUiAction.SelectCameraExtension)
                {
                    cameraExtensionsViewModel.SetExtensionMode(((CameraUiAction.SelectCameraExtension)action).Extension);
                }
                else if (action is CameraUiAction.ShutterButtonClick)
                {
                    cameraExtensionsViewModel.CapturePhoto();
                }
                else if (action is CameraUiAction.SwitchCameraClick)
                {
                    cameraExtensionsViewModel.SwitchCamera();
                }
                else if (action is CameraUiAction.ClosePhotoPreviewClick)
                {
                    cameraExtensionsScreen.HidePhoto();
                    cameraExtensionsScreen.ShowCameraControls();
                    cameraExtensionsViewModel.StartPreview(
                        this, cameraExtensionsScreen.PreviewView
                    );
                }
                else if (action is CameraUiAction.RequestPermissionClick)
                {
                    requestPermissionsLauncher.Launch(
                        Manifest.Permission.Camera
                    );
                }
            }
            else if (p0 is CaptureState)
            {
                var state = p0 as CaptureState;
                if (state is CaptureState.CaptureNotReady)
                {
                    cameraExtensionsScreen.HidePhoto();
                    cameraExtensionsScreen.EnableCameraShutter(true);
                    cameraExtensionsScreen.EnableSwitchLens(true);
                }
                else if (state is CaptureState.CaptureReady)
                {
                    cameraExtensionsScreen.EnableCameraShutter(true);
                    cameraExtensionsScreen.EnableSwitchLens(true);
                }
                else if (state is CaptureState.CaptureStarted)
                {
                    cameraExtensionsScreen.EnableCameraShutter(false);
                    cameraExtensionsScreen.EnableSwitchLens(false);
                }
                else if (state is CaptureState.CaptureFinished)
                {
                    cameraExtensionsViewModel.StopPreview();
                    cameraExtensionsScreen.ShowPhoto(((CaptureState.CaptureFinished)state).OutputResults.SavedUri);
                    cameraExtensionsScreen.HideCameraControls();
                }
                else if (state is CaptureState.CaptureFailed)
                {
                    cameraExtensionsScreen.ShowCaptureError("Couldn't take photo");
                    cameraExtensionsViewModel.StartPreview(
                        this, cameraExtensionsScreen.PreviewView
                    );
                    cameraExtensionsScreen.EnableCameraShutter(true);
                    cameraExtensionsScreen.EnableSwitchLens(true);
                }
            }
            else if (p0 is Kotlin.Pair)
            {
                var pair = p0 as Kotlin.Pair;
                var permissionState = pair.Component1() as PermissionState;
                var cameraUiState = pair.Component2() as CameraUiState;
                if (permissionState is PermissionState.Granted)
                {
                    cameraExtensionsScreen.HidePermissionsRequest();
                }
                else if (permissionState is PermissionState.Denied)
                {
                    if (cameraUiState.CameraState != CameraState.PreviewStopped)
                    {
                        cameraExtensionsScreen.ShowPermissionsRequest(((PermissionState.Denied)permissionState).ShouldShowRationale);
                        return null;
                    }
                }

                if (cameraUiState.CameraState == CameraState.NotReady)
                {
                    cameraExtensionsScreen.HidePhoto();
                    cameraExtensionsScreen.ShowCameraControls();
                    cameraExtensionsScreen.EnableCameraShutter(false);
                    cameraExtensionsScreen.EnableSwitchLens(false);
                    cameraExtensionsViewModel.InitializeCamera(this);
                }
                else if (cameraUiState.CameraState == CameraState.Ready)
                {
                    cameraExtensionsScreen.PreviewView.DoOnLaidOut(() =>
                    {
                        cameraExtensionsViewModel.StartPreview(
                            this,
                            cameraExtensionsScreen.PreviewView
                        );
                    });
                    cameraExtensionsScreen.SetAvailableExtensions(cameraUiState.AvailableExtensions.Select((it) =>
                    {
                        return new CameraExtensionItem()
                        {
                            ExtensionMode = it,
                            Name = GetString(extensionName[it]),
                            Selected = cameraUiState.ExtensionMode == it
                        };
                    }).ToList());
                    cameraExtensionsScreen.ShowCameraControls();
                }
                else if (cameraUiState.CameraState == CameraState.PreviewStopped)
                {
                }
            }
            return null;
        }

        public Object Invoke(Object p0, Object p1, Object p2)
        {
            return new Kotlin.Pair(p0, p1);
        }

        private PermissionState GetCurrentPermissionState()
        {
            var status = ActivityCompat.CheckSelfPermission(this, Manifest.Permission.Camera);
            return status == Permission.Granted ?
                new PermissionState.Granted() :
                new PermissionState.Denied(
                    ActivityCompat.ShouldShowRequestPermissionRationale(
                        this,
                        Manifest.Permission.Camera
                    )
                );
        }
    }
}