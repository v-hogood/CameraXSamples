using System.Collections.Generic;
using System.Linq;
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Camera.Core;
using AndroidX.Camera.Extensions;
using AndroidX.Camera.Lifecycle;
using AndroidX.Camera.View;
using AndroidX.Core.Content;
using AndroidX.Lifecycle;
using Java.IO;
using Java.Lang;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static CameraXExtensions.MainActivity;

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
        public CameraExtensionsViewModel(Application application) : base(application)
        {
            CameraUiState = cameraUiState;
            CaptureUiState = captureUiState;
        }

        public ICoroutineContext Context => LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).CoroutineContext;

        public void ResumeWith(Object result) { }

        private ProcessCameraProvider cameraProvider;
        private ExtensionsManager extensionsManager;

        private ImageCapture imageCapture = new ImageCapture.Builder()
            .SetTargetAspectRatio(AspectRatio.Ratio169)
            .Build();

        private Preview preview = new Preview.Builder()
            .SetTargetAspectRatio(AspectRatio.Ratio169)
            .Build();

        private IMutableStateFlow cameraUiState = StateFlowKt.MutableStateFlow(new CameraUiState());
        private IMutableStateFlow captureUiState = StateFlowKt.MutableStateFlow(new CaptureState.CaptureNotReady());

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
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
            new Function2(() =>
            {
                var currentCameraUiState = cameraUiState.Value as CameraUiState;

                // get the camera selector for the select lens face
                var cameraSelector = CameraLensToSelector((int)currentCameraUiState.CameraLens);

                // wait for the camera provider instance and extensions manager instance
                var cameraProviderFuture = ProcessCameraProvider.GetInstance(Application as Application);
                cameraProviderFuture.AddListener(new Runnable(() =>
                {
                    cameraProvider = cameraProviderFuture.Get() as ProcessCameraProvider;
                    var extensionsManagerFuture = ExtensionsManager.GetInstanceAsync(Application as Application,
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
                            ExtensionMode = ExtensionMode.None
                        };
                        cameraUiState.Emit(newCameraUiState, this);
                    }), ContextCompat.GetMainExecutor(context));
                }), ContextCompat.GetMainExecutor(context));
            }));
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
            var currentCameraUiState = cameraUiState.Value as CameraUiState;
            var cameraSelector = currentCameraUiState.ExtensionMode == ExtensionMode.None ?
                CameraLensToSelector(currentCameraUiState.CameraLens) :
                extensionsManager.GetExtensionEnabledCameraSelector(
                    CameraLensToSelector(currentCameraUiState.CameraLens),
                    currentCameraUiState.ExtensionMode);

            var useCaseGroup = new UseCaseGroup.Builder()
                .SetViewPort(previewView.ViewPort)
                .AddUseCase(imageCapture)
                .AddUseCase(preview)
                .Build();
            cameraProvider.UnbindAll();
            cameraProvider.BindToLifecycle(
                lifecycleOwner,
                cameraSelector,
                useCaseGroup
            );
            preview.SetSurfaceProvider(previewView.SurfaceProvider);

            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
            new Function2(() =>
            {
                if ((cameraUiState.Value as CameraUiState).CameraState != CameraState.Ready)
                    cameraUiState.Emit(new CameraUiState((CameraUiState)cameraUiState.Value) { CameraState = CameraState.Ready },
                        this);
                captureUiState.Emit(new CaptureState.CaptureReady(), this);
            }));
        }

        //
        // Stops the preview stream. This should be invoked when the captured image is displayed.
        //
        public void StopPreview()
        {
            preview.SetSurfaceProvider(null);
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
                new Function2(() =>
                    cameraUiState.Emit(new CameraUiState((CameraUiState)cameraUiState.Value) { CameraState = CameraState.PreviewStopped }, this)));
        }

        //
        // Toggle the camera lens face. This has no effect if there is only one available camera lens.
        //
        public void SwitchCamera()
        {
            var currentCameraUiState = cameraUiState.Value as CameraUiState;
            if (currentCameraUiState.CameraState == CameraState.Ready)
            {
                // To switch the camera lens, there has to be at least 2 camera lenses
                if (currentCameraUiState.AvailableCameraLens.Count == 1) return;

                var camLensFacing = currentCameraUiState.CameraLens;
                // Toggle the lens facing
                var newCameraUiState = camLensFacing == CameraSelector.LensFacingBack ?
                    new CameraUiState(currentCameraUiState) { CameraLens = CameraSelector.LensFacingFront } :
                    new CameraUiState(currentCameraUiState) { CameraLens = CameraSelector.LensFacingBack };

                LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
                new Function2(() =>
                {
                    cameraUiState.Emit(
                        new CameraUiState(newCameraUiState)
                        {
                            CameraState = CameraState.NotReady
                        }, this
                    );
                    captureUiState.Emit(new CaptureState.CaptureNotReady(), this);
                }));
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
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
                new Function2(() =>
                    captureUiState.Emit(new CaptureState.CaptureStarted(), this)));
            var photoFile = new File(GetCaptureStorageDirectory(), "test.jpg");
            var metadata = new ImageCapture.Metadata() {
                // Mirror image when using the front camera
                ReversedHorizontal =
                    ((CameraUiState)cameraUiState.Value).CameraLens == CameraSelector.LensFacingFront
            };
            var outputFileOptions =
                new ImageCapture.OutputFileOptions.Builder(photoFile)
                .SetMetadata(metadata)
                .Build();

            imageCapture.TakePicture(
                outputFileOptions,
                new DispatcherExecutor(Dispatchers.Default),
                this);
        }

        public void OnImageSaved(ImageCapture.OutputFileResults outputFileResults)
        {
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
                new Function2(() =>
                    captureUiState.Emit(new CaptureState.CaptureFinished(outputFileResults), this)));
        }

        public void OnError(ImageCaptureException exception)
        {
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
                new Function2(() =>
                    captureUiState.Emit(new CaptureState.CaptureFailed(exception), this)));
        }

        //
        // Sets the current extension mode. This will force the camera to rebind the use cases.
        //
        public void SetExtensionMode(int extensionMode)
        {
            LifecycleOwnerKt.GetLifecycleScope(ProcessLifecycleOwner.Get()).Launch(
            new Function2(() =>
            {
                cameraUiState.Emit(
                    new CameraUiState((CameraUiState)cameraUiState.Value)
                    {
                        CameraState = CameraState.NotReady,
                        ExtensionMode = extensionMode
                    }, this
                );
                captureUiState.Emit(new CaptureState.CaptureNotReady(), this);
            }));
        }

        public CameraSelector CameraLensToSelector(int lensFacing) =>
            lensFacing switch
            {
                CameraSelector.LensFacingFront => CameraSelector.DefaultFrontCamera,
                CameraSelector.LensFacingBack => CameraSelector.DefaultBackCamera,
                _ => throw new IllegalArgumentException("Invalid lens facing type: " + lensFacing)
            };

        public File GetCaptureStorageDirectory()
        {
            // Get the pictures directory that's inside the app-specific directory on
            // external storage.
            var context = Application as CameraExtensionsApplication;

            var file =
                new File(context.GetExternalFilesDir(Environment.DirectoryPictures), "camera_extensions");
            file.Mkdirs();
            return file;
        }
    }
}