//
// Simple app to demonstrate CameraX Video capturing with Recorder ( to local files ), with the
// following simple control follow:
//   - user starts capture.
//   - this app disables all UI selections.
//   - this app enables capture run-time UI (pause/resume/stop).
//   - user controls recording with run-time UI, eventually tap "stop" to end.
//   - this app informs CameraX recording to stop with recording.stop() (or recording.close()).
//   - CameraX notify this app that the recording is indeed stopped, with the Finalize event.
//   - this app starts VideoViewer fragment to view the captured result.
//
using Android.Content;
using Android.Content.Res;
using Android.Media;
using Android.Provider;
using Android.Util;
using Android.Views;
using AndroidX.Camera.Core;
using AndroidX.Camera.Core.ResolutionSelector;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.Video;
using AndroidX.Camera.View;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.Content;
using AndroidX.Core.Util;
using AndroidX.Lifecycle;
using AndroidX.Navigation;
using AndroidX.RecyclerView.Widget;
using Java.Lang;
using Java.Text;
using Java.Util;
using Java.Util.Concurrent;
using Kotlin.Coroutines;
using Kotlin.Jvm.Functions;
using Xamarin.KotlinX.Coroutines;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using Exception = Java.Lang.Exception;
using Fragment = AndroidX.Fragment.App.Fragment;
using Object = Java.Lang.Object;
using Orientation = Android.Content.Res.Orientation;
using VideoCapture = AndroidX.Camera.Video.VideoCapture;

namespace CameraXVideo
{
    [Android.App.Activity(Name = "com.android.example.cameraxvideo.fragments.CaptureFragment")]
    class CaptureFragment : Fragment,
        View.IOnClickListener,
        AndroidX.Lifecycle.IObserver,
        IConsumer
    {
        // UI without ViewBinding
        private PreviewView previewView;
        private ImageButton cameraButton;
        private ImageButton captureButton;
        private ImageButton stopButton;
        private CheckBox audioSelection;
        private RecyclerView qualitySelection;
        private TextView captureStatus;
        private MutableLiveData captureLiveStatus = new MutableLiveData();

        // Host's navigation controller
        private NavController navController;

        private List<CameraCapability> cameraCapabilities = new List<CameraCapability>();

        private VideoCapture videoCapture;
        private Recording currentRecording;
        private VideoRecordEvent recordingState;

        // Camera UI  states and inputs
        enum UiState
        {
            Idle,       // Not recording, all UI controls are active.
            Recording,  // Camera is recording, only display Pause/Resume & Stop button.
            Finalized,  // Recording just completes, disable all RECORDING UI controls.
            Recovery    // For future use.
        }
        private int cameraIndex = 0;
        private int qualityIndex = DefaultQualityIdx;
        private bool audioEnabled = false;

        private IExecutor mainThreadExecutor;
        private SemaphoreSlim enumerationDeferred = new SemaphoreSlim(1);

        // main cameraX capture functions
        //
        //   Always bind preview + video capture use case combinations in this sample
        //   (VideoCapture can work on its own). The function should always execute on
        //   the main thread.
        //
        private void BindCaptureUsecase()
        {
            var cameraProviderFuture = ProcessCameraProvider.GetInstance(RequireContext());
            cameraProviderFuture.AddListener(new Runnable(() =>
            {
                var cameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;

                var cameraSelector = GetCameraSelector(cameraIndex);

                // create the user required QualitySelector (video resolution): we know this is
                // supported, a valid qualitySelector will be created.
                var quality = cameraCapabilities[cameraIndex].Qualities[qualityIndex];
                var qualitySelector = QualitySelector.From(quality);

                var resolutionSelector = new ResolutionSelector.Builder().
                   SetAspectRatioStrategy(quality.GetAspectRatio() == AspectRatio.Ratio169 ?
                       AspectRatioStrategy.Ratio169FallbackAutoStrategy :
                       AspectRatioStrategy.Ratio43FallbackAutoStrategy)
                   .Build();

                var orientation = Resources.Configuration.Orientation;
                (previewView.LayoutParameters as ConstraintLayout.LayoutParams).
                    DimensionRatio = quality.GetAspectRatioString(
                        (orientation == Orientation.Portrait));

                var preview = new Preview.Builder()
                    .SetResolutionSelector(resolutionSelector)
                    .Build();

                preview.SetSurfaceProvider(previewView.SurfaceProvider);

                // build a recorder, which can:
                //   - record video/audio to MediaStore(only shown here), File, ParcelFileDescriptor
                //   - be used create recording(s) (the recording performs recording)
                var recorder = new Recorder.Builder()
                    .SetQualitySelector(qualitySelector)
                    .Build();
                videoCapture = VideoCapture.WithOutput(recorder);

                try
                {
                    cameraProvider.UnbindAll();
                    cameraProvider.BindToLifecycle(
                        ViewLifecycleOwner,
                        cameraSelector,
                        videoCapture,
                        preview
                    );
                }
                catch (Exception exc)
                {
                    // we are on main thread, let's reset the controls on the UI.
                    Log.Error(Tag, "Use case binding failed", exc);
                    ResetUIandState("BindToLifecycle failed: " + exc);
                }

                EnableUI(true);
            }), mainThreadExecutor);
        }

        //
        // Kick start the video recording
        //   - config Recorder to capture to MediaStoreOutput
        //   - register RecordEvent Listener
        //   - apply audio request from user
        //   - start recording!
        // After this function, user could start/pause/resume/stop recording and application listens
        // to VideoRecordEvent for the current recording status.
        //
        private void StartRecording()
        {
            // create MediaStoreOutputOptions for our recorder: resulting our recording!
            var name = "CameraX-recording-" +
                new SimpleDateFormat(FilenameFormat, Locale.Us)
                    .Format(JavaSystem.CurrentTimeMillis()) + ".mp4";
            var contentValues = new ContentValues();
            contentValues.Put(MediaStore.IMediaColumns.DisplayName, name);

            var mediaStoreOutput = new MediaStoreOutputOptions.Builder(
                RequireActivity().ContentResolver,
                MediaStore.Video.Media.ExternalContentUri)
                .SetContentValues(contentValues)
                .Build();

            // configure Recorder and Start recording to the mediaStoreOutput.
            var pendingRecording = (videoCapture.Output as Recorder)
               .PrepareRecording(RequireActivity(), mediaStoreOutput);
            if (audioEnabled) pendingRecording.WithAudioEnabled();
            currentRecording = pendingRecording.Start(mainThreadExecutor, this);

            Log.Info(Tag, "Recording started");
        }

        //
        // CaptureEvent listener.
        //
        public void Accept(Object t)
        {
            var videoRecordEvent = t as VideoRecordEvent;

            // cache the recording state
            if (!(videoRecordEvent is VideoRecordEvent.Status))
                recordingState = videoRecordEvent;

            UpdateUI(videoRecordEvent);

            if (videoRecordEvent is VideoRecordEvent.Finalize)
            {
                // display the captured video
                GetLifecycleScope(this).Launch(() =>
                {
                    var args = new Bundle();
                    args.PutString("uri", ((VideoRecordEvent.Finalize)videoRecordEvent).OutputResults.OutputUri.ToString());
                    navController.Navigate(
                        Resource.Id.action_capture_to_video_viewer, args);
                });
            }
        }

        //
        // Retrieve the asked camera's type(lens facing type). In this sample, only 2 types:
        //   idx is even number:  CameraSelector.LENS_FACING_BACK
        //          odd number:   CameraSelector.LENS_FACING_FRONT
        //
        private CameraSelector GetCameraSelector(int idx)
        {
            if (cameraCapabilities.Count == 0)
            {
                Log.Info(Tag, "Error: This device does not have any camera, bailing out");
                RequireActivity().Finish();
            }
            return (cameraCapabilities[idx % cameraCapabilities.Count].CamSelector);
        }

        struct CameraCapability { public CameraSelector CamSelector; public List<Quality> Qualities; };
        //
        // Query and cache this platform's camera capabilities, run only once.
        //
        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            navController = AndroidX.Navigation.Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container);

            mainThreadExecutor = ContextCompat.GetMainExecutor(RequireContext());

            enumerationDeferred.Wait();

            var cameraProviderFuture = ProcessCameraProvider.GetInstance(RequireContext());
            cameraProviderFuture.AddListener(new Runnable(() =>
            {
                // Camera provider is now guaranteed to be available
                var provider = cameraProviderFuture.Get() as ProcessCameraProvider;

                provider.UnbindAll();
                foreach (CameraSelector camSelector in new CameraSelector[] {
                    CameraSelector.DefaultBackCamera,
                    CameraSelector.DefaultFrontCamera })
                {
                    try
                    {
                        // just get the camera.cameraInfo to query capabilities
                        // we are not binding anything here.
                        if (provider.HasCamera(camSelector))
                        {
                            var camera = provider.BindToLifecycle(RequireActivity(), camSelector);
                            cameraCapabilities.Add(new CameraCapability()
                            {
                                CamSelector = camSelector,
                                Qualities = Recorder.GetVideoCapabilities(camera.CameraInfo)
                                    .GetSupportedQualities(new DynamicRange(DynamicRange.EncodingSdr, DynamicRange.BitDepth8Bit))
                                    .Where(quality => new CamcorderQuality[] { CamcorderQuality.Q2160p, CamcorderQuality.Q1080p, CamcorderQuality.Q720p, CamcorderQuality.Q480p }
                                        .Contains(quality.GetValue())).ToList()
                            });
                        }
                    }
                    catch (Exception exc)
                    {
                        Log.Error(Tag, "Camera Face " + camSelector + " is not supported", exc);
                    }
                }

                enumerationDeferred.Release();
            }), mainThreadExecutor);
        }

        //
        // One time initialize for CameraFragment (as a part of fragment layout's creation process).
        // This function performs the following:
        //   - initialize but disable all UI controls except the Quality selection.
        //   - set up the Quality selection recycler view.
        //   - bind use cases to a lifecycle camera, enable UI controls.
        //
        private void InitCameraFragment()
        {
            InitializeUI();

            enumerationDeferred.WaitAsync().ContinueWith((t) =>
            {
                GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
                {
                    InitializeQualitySectionsUI();

                    BindCaptureUsecase();

                    enumerationDeferred.Release();
                });
            });
        }

        //
        // Initialize UI. Preview and Capture actions are configured in this function.
        // Note that preview and capture are both initialized either by UI or CameraX callbacks
        // (except the very 1st time upon entering to this fragment in onCreateView()
        //
        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.camera_button)
            {
                cameraIndex = (cameraIndex + 1) % cameraCapabilities.Count;
                // camera device change is in effect instantly:
                //   - reset quality selection
                //   - restart preview
                qualityIndex = DefaultQualityIdx;
                InitializeQualitySectionsUI();
                EnableUI(false);
                GetLifecycleScope(ViewLifecycleOwner).Launch(() =>
                    BindCaptureUsecase());
            }
            else if (v.Id == Resource.Id.audio_selection)
            {
                audioEnabled = audioSelection.Checked;
            }
            else if (v.Id == Resource.Id.capture_button)
            {
                if (recordingState == null ||
                    recordingState is VideoRecordEvent.Finalize)
                {
                    EnableUI(false);  // Our eventListener will turn on the Recording UI.
                    StartRecording();
                }
                else
                {
                    if (recordingState is VideoRecordEvent.Start)
                    {
                        currentRecording?.Pause();
                        stopButton.Visibility = ViewStates.Visible;
                    }
                    else if (recordingState is VideoRecordEvent.Pause)
                    {
                        currentRecording?.Resume();
                    }
                    else if (recordingState is VideoRecordEvent.Resume)
                    {
                        currentRecording?.Pause();
                    }
                    else
                    {
                        throw new IllegalStateException("recordingState in unknown state");
                    }
                }
            }
            else if (v.Id == Resource.Id.stop_button)
            {
                // stopping: hide it after getting a click before we go to viewing fragment
                stopButton.Visibility = ViewStates.Invisible;
                if (currentRecording == null || recordingState is VideoRecordEvent.Finalize)
                {
                    return;
                }
                var recording = currentRecording;
                if (recording != null)
                {
                    recording.Stop();
                    currentRecording = null;
                }
                captureButton.SetImageResource(Resource.Drawable.ic_start);
            }
        }

        public void OnChanged(Object p0)
        {
            captureStatus.Post(() =>
            {
                captureStatus.Text = (string)p0;
            });
        }

        private void InitializeUI()
        {
            cameraButton.SetOnClickListener(this);

            cameraButton.Enabled = false;

            // audioEnabled by default is disabled.
            audioSelection.Checked = audioEnabled;
            audioSelection.SetOnClickListener(this);

            // React to user touching the capture button
            captureButton.SetOnClickListener(this);
            captureButton.Enabled = false;

            stopButton.SetOnClickListener(this);
            // ensure the stop button is initialized disabled & invisible
            stopButton.Visibility = ViewStates.Invisible;
            stopButton.Enabled = false;

            captureLiveStatus.Observe(ViewLifecycleOwner, this);
            captureLiveStatus.SetValue(GetString(Resource.String.Idle));
        }

        //
        // UpdateUI according to CameraX VideoRecordEvent type:
        //   - user starts capture.
        //   - this app disables all UI selections.
        //   - this app enables capture run-time UI (pause/resume/stop).
        //   - user controls recording with run-time UI, eventually tap "stop" to end.
        //   - this app informs CameraX recording to stop with recording.stop() (or recording.close()).
        //   - CameraX notify this app that the recording is indeed stopped, with the Finalize event.
        //   - this app starts VideoViewer fragment to view the captured result.
        //
        private void UpdateUI(VideoRecordEvent videoRecordEvent)
        {
            var state = videoRecordEvent is VideoRecordEvent.Status ?
                recordingState.GetNameString() : videoRecordEvent.GetNameString();

            if (videoRecordEvent is VideoRecordEvent.Status)
            {
                // placeholder: we update the UI with new status after this when() block,
                // nothing needs to do here.
            }
            else if (videoRecordEvent is VideoRecordEvent.Start)
            {
                ShowUi(UiState.Recording, videoRecordEvent.GetNameString());
            }
            else if (videoRecordEvent is VideoRecordEvent.Finalize)
            {
                ShowUi(UiState.Finalized, videoRecordEvent.GetNameString());
            }
            else if (videoRecordEvent is VideoRecordEvent.Pause)
            {
                captureButton.SetImageResource(Resource.Drawable.ic_resume);
            }
            else if (videoRecordEvent is VideoRecordEvent.Resume)
            {
                captureButton.SetImageResource(Resource.Drawable.ic_pause);
            }

            var stats = videoRecordEvent.RecordingStats;
            var size = stats.NumBytesRecorded / 1000;
            var time = TimeUnit.Nanoseconds.ToSeconds(stats.RecordedDurationNanos);
            var text = state + ": recorded " + size + " KB, in " + time + " seconds";
            if (videoRecordEvent is VideoRecordEvent.Finalize)
                text = text + "\nFile saved to: " + ((VideoRecordEvent.Finalize)videoRecordEvent).OutputResults.OutputUri.ToString();

            captureLiveStatus.SetValue(text);
            Log.Info(Tag, "recording event: " + text);
        }

        //
        // Enable/disable UI:
        //    User could select the capture parameters when recording is not in session
        //    Once recording is started, need to disable able UI to avoid conflict.
        //
        private void EnableUI(bool enable)
        {
            foreach (View it in new View[]
            {
                cameraButton,
                captureButton,
                stopButton,
                audioSelection,
                qualitySelection
            })
            {
                it.Enabled = enable;
            }
            // disable the camera button if no device to switch
            if (cameraCapabilities.Count <= 1)
            {
                cameraButton.Enabled = false;
            }
            // disable the resolution list if no resolution to switch
            if (cameraCapabilities[cameraIndex].Qualities.Count <= 1)
            {
                qualitySelection.Enabled = false;
            }
        }

        //
        // initialize UI for recording:
        //  - at recording: hide audio, qualitySelection,change camera UI; enable stop button
        //  - otherwise: show all except the stop button
        //
        private void ShowUi(UiState state, string status = "idle")
        {
            switch(state)
            {
                case UiState.Idle:
                    captureButton.SetImageResource(Resource.Drawable.ic_start);
                    stopButton.Visibility = ViewStates.Invisible;

                    cameraButton.Visibility = ViewStates.Visible;
                    audioSelection.Visibility = ViewStates.Visible;
                    qualitySelection.Visibility = ViewStates.Visible;
                    break;

                case UiState.Recording:
                    cameraButton.Visibility = ViewStates.Invisible;
                    audioSelection.Visibility = ViewStates.Invisible;
                    qualitySelection.Visibility = ViewStates.Invisible;

                    captureButton.SetImageResource(Resource.Drawable.ic_pause);
                    captureButton.Enabled = true;
                    stopButton.Visibility = ViewStates.Visible;
                    stopButton.Enabled = true;
                    break;

                case UiState.Finalized:
                    captureButton.SetImageResource(Resource.Drawable.ic_start);
                    stopButton.Visibility = ViewStates.Invisible;
                    break;

                default:
                    var errorMsg = "Error: showUI(" + state + ") is not supported";
                    Log.Error(Tag, errorMsg);
                    return;
            }
            captureStatus.Text = status;
        }

        //
        // ResetUI (restart):
        //    in case binding failed, let's give it another change for re-try. In future cases
        //    we might fail and user get notified on the status
        //
        private void ResetUIandState(string reason)
        {
            EnableUI(true);
            ShowUi(UiState.Idle, reason);

            cameraIndex = 0;
            qualityIndex = DefaultQualityIdx;
            audioEnabled = false;
            audioSelection.Checked = audioEnabled;
            InitializeQualitySectionsUI();
        }

        public class Listener : Object, View.IOnClickListener
        {
            CaptureFragment parent;
            private int position;

            public Listener(CaptureFragment parent, int position) =>
                (this.parent, this.position) = (parent, position);

            public void OnClick(View v)
            {
                if (parent.qualityIndex == position) return;

                // deselect the previous selection on UI.
                parent.qualitySelection.FindViewHolderForAdapterPosition(parent.qualityIndex)
                    .ItemView
                    .Selected = false;

                // turn on the new selection on UI.
                v.Selected = true;
                parent.qualityIndex = position;

                // rebind the use cases to put the new QualitySelection in action.
                parent.EnableUI(false);
                GetLifecycleScope(parent.ViewLifecycleOwner).Launch(() =>
                    parent.BindCaptureUsecase());
            }
        }

        public class Adapter : RecyclerView.Adapter
        {
            CaptureFragment parent;
            private string[] dataset;

            public Adapter(CaptureFragment parent, string[] dataset) =>
                (this.parent, this.dataset) = (parent, dataset);

            public class ViewHolder : RecyclerView.ViewHolder
            {
                public ViewHolder(View view) : base(view) { }
            }

            public override int ItemCount => dataset.Length;

            public override void OnBindViewHolder(RecyclerView.ViewHolder viewHolder, int position)
            {
                TextView textView = viewHolder.ItemView.FindViewById<TextView>(Resource.Id.qualityTextView);
                textView.Text = dataset[position];

                // select the default quality selector
                viewHolder.ItemView.Selected = position == parent.qualityIndex;
                viewHolder.ItemView.SetOnClickListener(new Listener(parent, position));
            }

            public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup viewGroup, int viewType)
            {
                View view = LayoutInflater.From(viewGroup.Context).Inflate(Resource.Layout.video_quality_item, viewGroup, false);
                return new ViewHolder(view);
            }
        }

        //
        //  initializeQualitySectionsUI():
        //    Populate a RecyclerView to display camera capabilities:
        //       - one front facing
        //       - one back facing
        //    User selection is saved to qualityIndex, will be used
        //    in the bindCaptureUsecase().
        //
        private void InitializeQualitySectionsUI()
        {
            var selectorStrings = cameraCapabilities[cameraIndex].Qualities.Select(it =>
                it.GetNameString()
            ).ToArray();
            // create the adapter to Quality selection RecyclerView
            qualitySelection.SetLayoutManager(new LinearLayoutManager(Context));
            qualitySelection.SetAdapter(new Adapter(this, selectorStrings));
            qualitySelection.Enabled = false;
        }

        // System function implementations
        public override View OnCreateView(
            LayoutInflater inflater,
            ViewGroup container,
            Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_capture, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            previewView = view.FindViewById<PreviewView>(Resource.Id.previewView);
            cameraButton = view.FindViewById<ImageButton>(Resource.Id.camera_button);
            captureButton = view.FindViewById<ImageButton>(Resource.Id.capture_button);
            stopButton = view.FindViewById<ImageButton>(Resource.Id.stop_button);
            audioSelection = view.FindViewById<CheckBox>(Resource.Id.audio_selection);
            qualitySelection = view.FindViewById<RecyclerView>(Resource.Id.quality_selection);
            captureStatus = view.FindViewById<TextView>(Resource.Id.capture_status);

            InitCameraFragment();
        }

        public override void OnDestroyView()
        {
            base.OnDestroyView();
        }

        // default Quality selection if no input from UI
        private const int DefaultQualityIdx = 0;
        private new const string Tag = "CaptureFragment";
        private const string FilenameFormat = "yyyy-MM-dd-HH-mm-ss-SSS";
    }

    public static class BuildersKtx
    {
        public class Function2 : Object, IFunction2
        {
            Action action;
            public Function2(Action action) => this.action = action;
            public Object Invoke(Object p0, Object p1)
            {
                action();
                return null;
            }
        }

        public static IJob Launch(this ICoroutineScope scope, Action action) =>
            BuildersKt.Launch(scope, EmptyCoroutineContext.Instance, CoroutineStart.Default, new Function2(action));
    }
}
