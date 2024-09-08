using Android;
using Android.Content.PM;
using AndroidX.Activity;
using AndroidX.Activity.Result;
using AndroidX.Activity.Result.Contract;
using AndroidX.AppCompat.App;
using AndroidX.Camera.Extensions;
using AndroidX.Core.App;
using AndroidX.Lifecycle;
using Java.Lang;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static AndroidX.Lifecycle.RepeatOnLifecycleKt;
using static CameraXExtensions.BuildersKtx;
using static Xamarin.KotlinX.Coroutines.Flow.FlowKt;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using Boolean = Java.Lang.Boolean;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

//
// Displays the camera preview with camera controls and available extensions. Tapping on the shutter
// button will capture a photo and display the photo.
//
namespace CameraXExtensions
{
    [Activity(Name = "com.android.example.cameraxextensions.MainActivity", Label = "@string/app_name", Theme = "@style/Theme.CameraXExtensions", MainLauncher = true)]
    public class MainActivity : AppCompatActivity,
        IActivityResultCallback,
        IContinuation,
        IFunction2,
        IFunction3
    {
        public ICoroutineContext Context => GetLifecycleScope(this).CoroutineContext;

        public void ResumeWith(Object result) { }

        private IDictionary<int, int> extensionName = new Dictionary<int, int>
        {
            [ExtensionMode.Auto] = Resource.String.camera_mode_auto,
            [ExtensionMode.Night] = Resource.String.camera_mode_night,
            [ExtensionMode.Hdr] = Resource.String.camera_mode_hdr,
            [ExtensionMode.FaceRetouch] = Resource.String.camera_mode_face_retouch,
            [ExtensionMode.Bokeh] = Resource.String.camera_mode_bokeh,
            [ExtensionMode.None] = Resource.String.camera_mode_none,
        };

        // tracks the current view state
        private IMutableStateFlow captureScreenViewState = MutableStateFlow(new CaptureScreenViewState());

        // handles back press if the current screen is the photo post capture screen
        class PostCaptureBackPressedCallback : OnBackPressedCallback
        {
            public PostCaptureBackPressedCallback(MainActivity parent) : base(false)
            { this.parent = parent; }
            MainActivity parent;

            public override void HandleOnBackPressed()
            {
                GetLifecycleScope(parent).Launch(() =>
                    parent.ClosePhotoPreview());
            }
        }
        private PostCaptureBackPressedCallback postCaptureBackPressedCallback;

        private CameraExtensionsScreen cameraExtensionsScreen;

        // view model for operating on the camera and capturing a photo
        private CameraExtensionsViewModel cameraExtensionsViewModel;

        // monitors changes in camera permission state
        private IMutableStateFlow permissionState = MutableStateFlow(new PermissionState());

        ActivityResultLauncher requestPermissionsLauncher;

        private Uri captureUri = null;
        private bool progressComplete = false;

        private void ShowCapture()
        {
            if (captureUri == null || !progressComplete) return;

            cameraExtensionsViewModel.StopPreview();
            captureScreenViewState.Emit(
                (captureScreenViewState.Value as CaptureScreenViewState)
                .UpdatePostCaptureScreen(() =>
                {
                    if (captureUri != null)
                    {
                        return new PostCaptureScreenViewState.VisibleViewState()
                        { uri = captureUri };
                    }
                    else
                    {
                        return new PostCaptureScreenViewState.HiddenViewState();
                    }
                })
                .UpdateCameraScreen((it) =>
                    it.HideCameraControls()
                      .HideProcessProgressViewState()
                ), this);

            captureUri = null;
            progressComplete = false;
        }

        public void OnActivityResult(Object result)
        {
            var isGranted = result as Boolean;
            if (isGranted.BooleanValue())
            {
                GetLifecycleScope(this).Launch(() =>
                    permissionState.Emit(new PermissionState.Granted(), this));
            }
            else
            {
                GetLifecycleScope(this).Launch(() =>
                    permissionState.Emit(new PermissionState.Denied(true), this));
            }
        }

        protected override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);
            SetContentView(Resource.Layout.activity_main);

            cameraExtensionsViewModel = new ViewModelProvider(
                this,
                new CameraExtensionsViewModelFactory(
                    Application,
                    ImageCaptureRepository.Create(ApplicationContext)
                )
            ).Get(Class.FromType(typeof(CameraExtensionsViewModel))) as CameraExtensionsViewModel;

            // capture screen abstracts the UI logic and exposes simple functions on how to interact
            // with the UI layer.
            cameraExtensionsScreen = new CameraExtensionsScreen(FindViewById(Resource.Id.root));

            postCaptureBackPressedCallback = new PostCaptureBackPressedCallback(this);

            // consume and dispatch the current view state to update the camera extensions screen
            GetLifecycleScope(this).Launch(() => CollectLatest(captureScreenViewState, this, this));

            OnBackPressedDispatcher.AddCallback(this, postCaptureBackPressedCallback);

            // initialize the permission state flow with the current camera permission status
            permissionState = MutableStateFlow(GetCurrentPermissionState());

            requestPermissionsLauncher =
                RegisterForActivityResult(new ActivityResultContracts.RequestPermission(), this);

            GetLifecycleScope(this).Launch(() =>
                RepeatOnLifecycle(this, Lifecycle.State.Resumed,
                    new Function2(() =>
                        // check the current permission state every time upon the activity is resumed
                        permissionState.Emit(GetCurrentPermissionState(), this)), this));

            // Consumes actions emitted by the UI and performs the appropriate operation associated with
            // the view model or permission flow.
            // Note that this flow is a shared flow and will not emit the last action unlike the state
            // flows exposed by the view model for consuming UI state.
            GetLifecycleScope(this).Launch(() =>
                CollectLatest(cameraExtensionsScreen.Action, this, this));

            // Consume state emitted by the view model to render the Photo Capture state.
            // Upon collecting this state, the last emitted state will be immediately received.
            GetLifecycleScope(this).Launch(() =>
                CollectLatest(cameraExtensionsViewModel.CaptureUiState, this, this));

            // Because camera state is dependent on the camera permission status, we combine both camera
            // UI state and permission state such that each emission accurately captures the current
            // permission status and camera UI state.
            // The camera permission is always checked to see if it's granted. If it isn't then stop
            // interacting with the camera and display the permission request screen. The user can tap
            // on "Turn On" to request permissions.
            GetLifecycleScope(this).Launch(() =>
                CollectLatest(
                    Combine(permissionState, cameraExtensionsViewModel.CameraUiState, this),
                    this, this));
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
                    ClosePhotoPreview();
                }
                else if (action is CameraUiAction.RequestPermissionClick)
                {
                    requestPermissionsLauncher.Launch(
                        Manifest.Permission.Camera
                    );
                }
                else if (action is CameraUiAction.ProcessProgressComplete)
                {
                    progressComplete = true;
                    ShowCapture();
                }
                else if (action is CameraUiAction.Focus)
                {
                    cameraExtensionsViewModel.Focus(
                        (action as CameraUiAction.Focus).meteringPoint);
                }
                else if (action is CameraUiAction.Scale)
                {
                    cameraExtensionsViewModel.Scale(
                        (action as CameraUiAction.Scale).scaleFactor);
                }
            }
            else if (p0 is CaptureState)
            {
                var state = p0 as CaptureState;
                if (state is CaptureState.CaptureNotReady)
                {
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdatePostCaptureScreen(() =>
                            new PostCaptureScreenViewState.HiddenViewState())
                        .UpdateCameraScreen((it) =>
                            it.EnableCameraShutter(true)
                                .EnableSwitchLens(true)
                                .HidePostview()
                        ), this);
                }
                else if (state is CaptureState.CaptureReady)
                {
                    captureUri = null;
                    progressComplete = false;
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((it) =>
                            it.EnableCameraShutter(true)
                                .EnableSwitchLens(true)
                        ), this);
                }
                else if (state is CaptureState.CaptureStarted)
                {
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((it) =>
                            it.EnableCameraShutter(false)
                                .EnableSwitchLens(false)
                        ), this);
                }
                else if (state is CaptureState.CaptureFinished)
                {
                    captureUri = (state as CaptureState.CaptureFinished).OutputResults.SavedUri;
                    if (!(state as CaptureState.CaptureFinished).IsProcessingSupported)
                    {
                        progressComplete = true;
                    }
                    ShowCapture();
                }
                else if (state is CaptureState.CaptureFailed)
                {
                    cameraExtensionsScreen.ShowCaptureError("Couldn't take photo");
                    cameraExtensionsViewModel.StartPreview(
                        this, cameraExtensionsScreen.PreviewView
                    );
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((it) =>
                            it.ShowCameraControls()
                                .EnableCameraShutter(true)
                                .EnableSwitchLens(true)
                                .HideProcessProgressViewState()
                                .HidePostview()
                       ), this);
                }
                else if (state is CaptureState.CapturePostview)
                {
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((it) =>
                            it.ShowPostview((state as CaptureState.CapturePostview).Bitmap)
                        ), this);
                }
                else if (state is CaptureState.CaptureProcessProgress)
                {
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((it) =>
                            it.ShowProcessProgressViewState((state as CaptureState.CaptureProcessProgress).Progress)
                        ), this);
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
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdatePostCaptureScreen(() =>
                            new PostCaptureScreenViewState.HiddenViewState())
                        .UpdateCameraScreen((it) =>
                            it.ShowCameraControls()
                                .HidePostview()
                                .EnableCameraShutter(false)
                                .EnableSwitchLens(false)
                        ), this);
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
                    captureScreenViewState.Emit(
                        (captureScreenViewState.Value as CaptureScreenViewState)
                        .UpdateCameraScreen((s) =>
                            s.ShowCameraControls()
                            .SetAvailableExtensions(cameraUiState.AvailableExtensions.Select((it) =>
                            {
                                return new CameraExtensionItem()
                                {
                                    ExtensionMode = it,
                                    Name = GetString(extensionName[it]),
                                    Selected = cameraUiState.ExtensionMode == it
                                };
                            }).ToList())
                        ), this);
                }
                else if (cameraUiState.CameraState == CameraState.PreviewStopped)
                {
                }
            }
            else if (p0 is CaptureScreenViewState)
            {
                var state = p0 as CaptureScreenViewState;
                cameraExtensionsScreen.SetCaptureScreenViewState(state);
                postCaptureBackPressedCallback.Enabled =
                    state.postCaptureScreenViewState is PostCaptureScreenViewState.VisibleViewState;
            }
            return null;
        }

        public Object Invoke(Object p0, Object p1, Object p2)
        {
            return new Kotlin.Pair(p0, p1);
        }

        private void ClosePhotoPreview()
        {
            captureScreenViewState.Emit(
                (captureScreenViewState.Value as CaptureScreenViewState)
                .UpdateCameraScreen((state) =>
                    state.ShowCameraControls()
                         .HidePostview())
                .UpdatePostCaptureScreen(() =>
                    new PostCaptureScreenViewState.HiddenViewState()),
                this
            );
            cameraExtensionsViewModel.StartPreview(
                this, cameraExtensionsScreen.PreviewView
            );
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
