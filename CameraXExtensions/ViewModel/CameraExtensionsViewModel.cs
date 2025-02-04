using Android.Content;
using Android.Graphics;
using Android.Util;
using AndroidX.Camera.Core;
using AndroidX.Camera.Core.ResolutionSelector;
using AndroidX.Camera.Extensions;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Java.Lang;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Core.Net.UriKt;
using static AndroidX.Lifecycle.ViewModelKt;
using static Xamarin.KotlinX.Coroutines.ExecutorsKt;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using File = Java.IO.File;
using Object = Java.Lang.Object;

namespace CameraXExtensions
{
    //
    // View model for camera extensions. This manages all the operations on the camera.
    // This includes opening and closing the camera, showing the camera preview, capturing a photo,
    // checking which extensions are available, and selecting an extension.
    //
    // Camera UI state is communicated via the cameraUiState flow.
    // Capture UI state is communicated via the captureUiState flow.
    //
    // Rebinding to the UI state flows will always emit the last UI state.
    //
    public class CameraExtensionsViewModel : AndroidViewModel,
        ImageCapture.IOnImageSavedCallback,
        IContinuation
    {
        public CameraExtensionsViewModel(
            Application application,
            ImageCaptureRepository imageCaptureRepository
        ) : base(application)
        {
            this.application = application;
            this.imageCaptureRepository = imageCaptureRepository;

            CameraUiState = cameraUiState;
            CaptureUiState = captureUiState;
        }
        Application application;
        ImageCaptureRepository imageCaptureRepository;

        private const string Tag = "CameraExtensionsViewModel";
        private const long RealtimeLatencyUpdateIntervalMillis = 1000L;

        public ICoroutineContext Context => GetViewModelScope(this).CoroutineContext;

        public void ResumeWith(Object result) { }

        private ProcessCameraProvider cameraProvider;
        private ExtensionsManager extensionsManager;

        private ICamera camera;
        private File photoFile;

        private static ResolutionSelector resolutionSelector = new ResolutionSelector.Builder().
            SetAspectRatioStrategy(AspectRatioStrategy.Ratio169FallbackAutoStrategy)
            .Build();

        private ImageCapture imageCapture = new ImageCapture.Builder()
            .SetResolutionSelector(resolutionSelector)
            .Build();
        private IJob realtimeLatencyEstimateJob;

        private Preview preview = new Preview.Builder()
            .SetResolutionSelector(resolutionSelector)
            .Build();

        private IMutableStateFlow cameraUiState = MutableStateFlow(new CameraUiState());
        private IMutableStateFlow captureUiState = MutableStateFlow(new CaptureState.CaptureNotReady());

        public IFlow CameraUiState;
        public IFlow CaptureUiState;

        public class CameraProvider : Object, ICameraProvider
        {
            ProcessCameraProvider processCameraProvider;
            public CameraProvider(ProcessCameraProvider processCameraProvider) =>
                this.processCameraProvider = processCameraProvider;

            public IList<ICameraInfo> AvailableCameraInfos => processCameraProvider.AvailableCameraInfos;

            public bool HasCamera(CameraSelector p0) => processCameraProvider.HasCamera(p0);
        }

        //
        // Initializes the camera and checks which extensions are available for the selected camera lens
        // face. If no extensions are available then the selected extension will be set to None and the
        // available extensions list will also contain None.
        // Because this operation is async, clients should wait for cameraUiState to emit
        // CameraState.READY. Once the camera is ready the client can start the preview.
        //
        public void InitializeCamera(Context context)
        {
            GetViewModelScope(this).Launch(() =>
            {
                var currentCameraUiState = cameraUiState.Value as CameraUiState;

                // get the camera selector for the select lens face
                var cameraSelector = CameraLensToSelector((int)currentCameraUiState.CameraLens);

                // wait for the camera provider instance and extensions manager instance
                var cameraProviderFuture = ProcessCameraProvider.GetInstance(application);
                cameraProviderFuture.AddListener(new Runnable(() =>
                {
                    cameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;
                    var extensionsManagerFuture = ExtensionsManager.GetInstanceAsync(application,
                        new CameraProvider(cameraProvider));
                    extensionsManagerFuture.AddListener(new Runnable(() =>
                    {
                        extensionsManager = extensionsManagerFuture.Get() as ExtensionsManager;

                        var availableCameraLens =
                            new int[] {
                                CameraSelector.LensFacingBack,
                                CameraSelector.LensFacingFront
                            }.Where(lensFacing =>
                                cameraProvider.HasCamera(CameraLensToSelector(lensFacing))
                            ).ToList();

                        // get the supported extensions for the selected camera lens by filtering the full list
                        // of extensions and checking each one if it's available
                        var availableExtensions = new int[] {
                            ExtensionMode.None,
                            ExtensionMode.Auto,
                            ExtensionMode.Bokeh,
                            ExtensionMode.Hdr,
                            ExtensionMode.Night,
                            ExtensionMode.FaceRetouch
                        }.Where(extensionMode =>
                            extensionsManager.IsExtensionAvailable(cameraSelector, extensionMode)
                        ).ToList();

                        // prepare the new camera UI state which is now in the READY state and contains the list
                        // of available extensions, available lens faces.
                        var newCameraUiState = new CameraUiState(currentCameraUiState)
                        {
                            CameraState = CameraState.Ready,
                            AvailableExtensions = availableExtensions,
                            AvailableCameraLens = availableCameraLens,
                            ExtensionMode = availableExtensions.Count == 0 ? ExtensionMode.None : currentCameraUiState.ExtensionMode,
                            RealtimeCaptureLatencyEstimate = ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency
                        };
                        cameraUiState.Emit(newCameraUiState, this);
                    }), ContextCompat.GetMainExecutor(context));
                }), ContextCompat.GetMainExecutor(context));
            });
        }

        //
        // Starts the preview stream. The camera state should be in the READY or PREVIEW_STOPPED state
        // when calling this operation.
        // This process will bind the preview and image capture uses cases to the camera provider.
        //
        public void StartPreview(
            ILifecycleOwner lifecycleOwner,
            PreviewView previewView)
        {
            realtimeLatencyEstimateJob?.Cancel(null);

            var currentCameraUiState = cameraUiState.Value as CameraUiState;
            var cameraSelector = currentCameraUiState.ExtensionMode == ExtensionMode.None ?
                CameraLensToSelector(currentCameraUiState.CameraLens) :
                extensionsManager.GetExtensionEnabledCameraSelector(
                    CameraLensToSelector(currentCameraUiState.CameraLens),
                    currentCameraUiState.ExtensionMode);

            cameraProvider.UnbindAll();
            camera = cameraProvider.BindToLifecycle(lifecycleOwner, cameraSelector);

            if (camera?.CameraInfo != null)
            {
                var isPostviewSupported =
                    ImageCapture.GetImageCaptureCapabilities(camera.CameraInfo).IsPostviewSupported;
#pragma warning disable CS0618
                imageCapture = new ImageCapture.Builder()
                    .SetTargetAspectRatio(AspectRatio.Ratio169)
#pragma warning restore CS0618
                    .SetPostviewEnabled(isPostviewSupported)
                    .Build();
            }

            var useCaseGroup = new UseCaseGroup.Builder()
                .SetViewPort(previewView.ViewPort)
                .AddUseCase(imageCapture)
                .AddUseCase(preview)
                .Build();
            cameraProvider.UnbindAll();
            camera = cameraProvider.BindToLifecycle(
                lifecycleOwner,
                cameraSelector,
                useCaseGroup
            );
            preview.SurfaceProvider = previewView.SurfaceProvider;

            GetViewModelScope(this).Launch(() =>
            {
                if ((cameraUiState.Value as CameraUiState).CameraState != CameraState.Ready)
                    cameraUiState.Emit(new CameraUiState((CameraUiState)cameraUiState.Value) { CameraState = CameraState.Ready },
                        this);
                captureUiState.Emit(new CaptureState.CaptureReady(), this);
                var previewStreamState = previewView.PreviewStreamState.Value as PreviewView.StreamState;
                if (previewStreamState == PreviewView.StreamState.Idle)
                {
                    realtimeLatencyEstimateJob?.Cancel(null);
                    realtimeLatencyEstimateJob = null;
                }
                if (previewStreamState == PreviewView.StreamState.Streaming)
                {
                    if (realtimeLatencyEstimateJob == null)
                    {
                        realtimeLatencyEstimateJob =
                            GetViewModelScope(this).Launch(() =>
                                ObserveRealtimeLatencyEstimate());
                    }
                }
            });
        }

        private void ObserveRealtimeLatencyEstimate()
        {
            Log.Debug(Tag, "Starting realtime latency estimate job");

            var currentCameraUiState = cameraUiState.Value as CameraUiState;
            var isSupported =
                currentCameraUiState.ExtensionMode != ExtensionMode.None &&
                    imageCapture.RealtimeCaptureLatencyEstimate != ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency;

            if (!isSupported)
            {
                Log.Debug(Tag, "Starting realtime latency estimate job: no extension mode or not supported");
                cameraUiState.Emit(
                    new CameraUiState((CameraUiState)cameraUiState.Value)
                    {
                        CameraState = CameraState.PreviewActive,
                        RealtimeCaptureLatencyEstimate = ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency
                    }, this);
                    return;
            }

            while (CoroutineScopeKt.IsActive(GetViewModelScope(this)))
            {
                UpdateRealtimeCaptureLatencyEstimate();
                DelayKt.Delay(RealtimeLatencyUpdateIntervalMillis, this);
            }
        }

        //
        // Stops the preview stream. This should be invoked when the captured image is displayed.
        //
        public void StopPreview()
        {
            realtimeLatencyEstimateJob?.Cancel(null);
            preview.SurfaceProvider = null;
            GetViewModelScope(this).Launch(() =>
                cameraUiState.Emit(new CameraUiState((CameraUiState)cameraUiState.Value)
                {
                    CameraState = CameraState.PreviewStopped,
                    RealtimeCaptureLatencyEstimate = ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency
                }, this));
        }

        //
        // Toggle the camera lens face. This has no effect if there is only one available camera lens.
        //
        public void SwitchCamera()
        {
            realtimeLatencyEstimateJob?.Cancel(null);
            var currentCameraUiState = cameraUiState.Value as CameraUiState;
            if (currentCameraUiState.CameraState == CameraState.Ready || currentCameraUiState.CameraState == CameraState.PreviewActive)
            {
                // To switch the camera lens, there has to be at least 2 camera lenses
                if (currentCameraUiState.AvailableCameraLens.Count == 1) return;

                var camLensFacing = currentCameraUiState.CameraLens;
                // Toggle the lens facing
                var newCameraUiState = camLensFacing == CameraSelector.LensFacingBack ?
                    new CameraUiState(currentCameraUiState) { CameraLens = CameraSelector.LensFacingFront } :
                    new CameraUiState(currentCameraUiState) { CameraLens = CameraSelector.LensFacingBack };

                GetViewModelScope(this).Launch(() =>
                {
                    cameraUiState.Emit(
                        new CameraUiState(newCameraUiState)
                        {
                            CameraState = CameraState.NotReady
                        }, this
                    );
                    captureUiState.Emit(new CaptureState.CaptureNotReady(), this);
                });
            }
        }

        //
        // Captures the photo and saves it to the pictures directory that's inside the app-specific
        // directory on external storage.
        // Upon successful capture, the captureUiState flow will emit CaptureFinished with the URI to
        // the captured photo.
        // If the capture operation failed then captureUiState flow will emit CaptureFailed with the
        // exception containing more details on the reason for failure.
        //
        public void CapturePhoto()
        {
            realtimeLatencyEstimateJob?.Cancel(null);
            GetViewModelScope(this).Launch(() =>
                captureUiState.Emit(new CaptureState.CaptureStarted(), this));
            photoFile = imageCaptureRepository.CreateImageOutputFile();
            var metadata = new ImageCapture.Metadata() {
                // Mirror image when using the front camera
                ReversedHorizontal =
                    ((CameraUiState)cameraUiState.Value).CameraLens == CameraSelector.LensFacingFront
            };
            var outputFileOptions =
                new ImageCapture.OutputFileOptions.Builder(photoFile)
                .SetMetadata(metadata)
                .Build();

            if (camera?.CameraInfo != null)
            {
                if (ImageCapture.GetImageCaptureCapabilities(camera.CameraInfo).IsCaptureProcessProgressSupported)
                {
                    GetViewModelScope(this).Launch(() =>
                    {
                        captureUiState.Emit(new CaptureState.CaptureProcessProgress(0), this);
                    });
                }
            }

            imageCapture.TakePicture(
                outputFileOptions,
                AsExecutor(Dispatchers.Default),
                this);
        }

        public void OnImageSaved(ImageCapture.OutputFileResults outputFileResults)
        {
            imageCaptureRepository.NotifyImageCreated(
                application,
                outputFileResults.SavedUri ?? ToUri(photoFile)
            );
            var isProcessProgressSupported = false;
            if (camera?.CameraInfo != null)
                isProcessProgressSupported =
                    ImageCapture.GetImageCaptureCapabilities(camera.CameraInfo).IsCaptureProcessProgressSupported;
            GetViewModelScope(this).Launch(() =>
            {
                if (isProcessProgressSupported)
                {
                    captureUiState.Emit(new CaptureState.CaptureProcessProgress(100), this);
                }
                captureUiState.Emit(
                    new CaptureState.CaptureFinished(
                        outputFileResults,
                        isProcessProgressSupported), this);
            });
        }

        public void OnError(ImageCaptureException exception)
        {
            GetViewModelScope(this).Launch(() =>
                captureUiState.Emit(new CaptureState.CaptureFailed(exception), this));
        }

        public void OnCaptureProcessProgressed(int progress)
        {
            GetViewModelScope(this).Launch(() =>
                captureUiState.Emit(new CaptureState.CaptureProcessProgress(progress), this));
        }

        public void OnPostviewBitmapAvailable(Bitmap bitmap)
        {
            GetViewModelScope(this).Launch(() =>
                captureUiState.Emit(new CaptureState.CapturePostview(bitmap), this));
        }

        //
        // Sets the current extension mode. This will force the camera to rebind the use cases.
        //
        public void SetExtensionMode(int extensionMode)
        {
            GetViewModelScope(this).Launch(() =>
            {
                cameraUiState.Emit(
                    new CameraUiState((CameraUiState)cameraUiState.Value)
                    {
                        CameraState = CameraState.NotReady,
                        ExtensionMode = extensionMode,
                        RealtimeCaptureLatencyEstimate = ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency
                    }, this
                );
                captureUiState.Emit(new CaptureState.CaptureNotReady(), this);
            });
        }

        public void Focus(MeteringPoint meteringPoint)
        {
            if (camera == null) return;

            var meteringAction = new FocusMeteringAction.Builder(meteringPoint).Build();
            camera.CameraControl.StartFocusAndMetering(meteringAction);
        }

        public void Scale(float scaleFactor)
        {
            if (camera == null) return;

            var currentZoomRatio = camera.CameraInfo.ZoomState.Value as IZoomState;
            camera.CameraControl.SetZoomRatio(scaleFactor *
                (currentZoomRatio != null ? currentZoomRatio.ZoomRatio : 1f));
        }

        public CameraSelector CameraLensToSelector(int lensFacing) =>
            lensFacing switch
            {
                CameraSelector.LensFacingFront => CameraSelector.DefaultFrontCamera,
                CameraSelector.LensFacingBack => CameraSelector.DefaultBackCamera,
                _ => throw new IllegalArgumentException("Invalid lens facing type: " + lensFacing)
            };

        private void UpdateRealtimeCaptureLatencyEstimate()
        {
            var estimate = imageCapture.RealtimeCaptureLatencyEstimate;
            Log.Debug(Tag, "Realtime capture latency estimate: " + estimate);
            if (estimate == ImageCaptureLatencyEstimate.UndefinedImageCaptureLatency)
            {
                return;
            }
            cameraUiState.Emit(
                new CameraUiState((CameraUiState)cameraUiState.Value)
                {
                    CameraState = CameraState.PreviewActive,
                    RealtimeCaptureLatencyEstimate = estimate
                }, this);
        }
    }
}
