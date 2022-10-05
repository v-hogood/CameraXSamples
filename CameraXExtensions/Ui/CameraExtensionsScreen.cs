using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Android.Animation;
using Android.Content;
using Android.Net;
using Android.Views;
using Android.Widget;
using AndroidX.Camera.View;
using AndroidX.Lifecycle;
using AndroidX.RecyclerView.Widget;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static Android.Icu.Text.Transliterator;
using static Android.Provider.DocumentsContract;
using static CameraXExtensions.MainActivity;

namespace CameraXExtensions
{
    //
    // Displays the camera preview and captured photo.
    // Encapsulates the details of how the screen is constructed and exposes a set of explicit
    // operations clients can perform on the screen.
    //
    class CameraExtensionsScreen :
        RecyclerView.OnScrollListener,
        View.IOnClickListener,
        IContinuation,
        Animator.IAnimatorListener
    {
        private View root;
        private Context context;

        private View cameraShutterButton;
        private ImageView photoPreview;
        private View closePhotoPreview;
        private ImageView switchLensButton;
        private RecyclerView extensionSelector;
        private CameraExtensionsSelectorAdapter extensionsAdapter;
        private View permissionsRationaleContainer;
        private TextView permissionsRationale;
        private TextView permissionsRequestButton;

        public PreviewView PreviewView;

        private IMutableStateFlow action = StateFlowKt.MutableStateFlow(new CameraUiAction());
        public IFlow Action;

        public ICoroutineContext Context => LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).CoroutineContext;

        public void ResumeWith(Java.Lang.Object result) { }

        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.cameraShutter)
            {
                LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).Launch(
                    new Function2(() =>
                        action.Emit(new CameraUiAction.ShutterButtonClick(), this)));
            }
            else if (v.Id == Resource.Id.switchLens)
            {
                LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).Launch(
                    new Function2(() =>
                        action.Emit(new CameraUiAction.SwitchCameraClick(), this)));
                switchLensButton.Animate().Rotation(180f);
                switchLensButton.Animate().SetDuration(300L);
                switchLensButton.Animate().SetListener(this);
                switchLensButton.Animate().Start();
            }
            else if (v.Id == Resource.Id.closePhotoPreview)
            {
                LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).Launch(
                    new Function2(() =>
                        action.Emit(new CameraUiAction.ClosePhotoPreviewClick(), this)));
            }
            else if (v.Id == Resource.Id.permissionsRequestButton)
            {
                LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).Launch(
                    new Function2(() =>
                        action.Emit(new CameraUiAction.RequestPermissionClick(), this)));
            }
        }

        public void OnAnimationStart(Animator animation) { }

        public void OnAnimationEnd(Animator animation)
        {
            switchLensButton.Animate().Rotation(0f);
        }

        public void OnAnimationRepeat(Animator animation) { }

        public void OnAnimationCancel(Animator animation) { }

        private SnapHelper snapHelper = new CenterItemSnapHelper();
        private int snapPosition = RecyclerView.NoPosition;

        public override void OnScrollStateChanged(RecyclerView recyclerView, int newState)
        {
            if (newState == RecyclerView.ScrollStateIdle)
            {
                var layoutManager = recyclerView.GetLayoutManager();
                var snapView = snapHelper.FindSnapView(layoutManager);
                var newSnapPosition = layoutManager.GetPosition(snapView);
                OnItemSelected(snapPosition, newSnapPosition);
                snapPosition = newSnapPosition;
            }
        }

        private void OnItemSelected(int oldPosition, int newPosition)
        {
            if (oldPosition == newPosition) return;
            SelectItem(newPosition);
            var it = extensionsAdapter.CurrentList[newPosition] as CameraExtensionItem;
            LifecycleOwnerKt.GetLifecycleScope(ViewKt.FindViewTreeLifecycleOwner(root)).Launch(
                new Function2(() =>
                    action.Emit(new CameraUiAction.SelectCameraExtension(it.ExtensionMode), this)));
        }

        private void SelectItem(int position)
        {
            var data =
                (extensionsAdapter.CurrentList as IList<CameraExtensionItem>).
                    Select((cameraExtensionModel, index) =>
                    {
                        return new CameraExtensionItem(cameraExtensionModel) { Selected = position == index };
                    }).ToList();
            extensionsAdapter.SubmitList(data);
        }

        public CameraExtensionsScreen(View root)
        {
            Action = action;
            this.root = root;
            context = root.Context;

            cameraShutterButton = root.FindViewById(Resource.Id.cameraShutter);
            photoPreview = root.FindViewById<ImageView>(Resource.Id.photoPreview);
            closePhotoPreview = root.FindViewById(Resource.Id.closePhotoPreview);
            switchLensButton = root.FindViewById<ImageView>(Resource.Id.switchLens);
            extensionSelector = root.FindViewById<RecyclerView>(Resource.Id.extensionSelector);
            permissionsRationaleContainer =
                root.FindViewById(Resource.Id.permissionsRationaleContainer);
            permissionsRationale = root.FindViewById<TextView>(Resource.Id.permissionsRationale);
            permissionsRequestButton =
                root.FindViewById<TextView>(Resource.Id.permissionsRequestButton);

            PreviewView = root.FindViewById<PreviewView>(Resource.Id.previewView);

            extensionsAdapter = new CameraExtensionsSelectorAdapter(this);
            extensionSelector.SetLayoutManager(
                new LinearLayoutManager(context, RecyclerView.Horizontal, false));
            extensionSelector.SetAdapter(extensionsAdapter);
            extensionSelector.AddItemDecoration(new OffsetCenterItemDecoration());
            extensionSelector.AddOnScrollListener(this);

            snapHelper.AttachToRecyclerView(extensionSelector);

            cameraShutterButton.SetOnClickListener(this);

            switchLensButton.SetOnClickListener(this);

            closePhotoPreview.SetOnClickListener(this);

            permissionsRequestButton.SetOnClickListener(this);
        }

        public void SetAvailableExtensions(List<CameraExtensionItem> extensions)
        {
            extensionsAdapter.SubmitList(extensions);
        }

        public void ShowPhoto(Uri uri)
        {
            if (uri == null) return;
            photoPreview.Visibility = ViewStates.Visible;
            // photoPreview.Load(uri);
            closePhotoPreview.Visibility = ViewStates.Visible;
        }

        public void HidePhoto()
        {
            photoPreview.Visibility = ViewStates.Invisible;
            closePhotoPreview.Visibility = ViewStates.Invisible;
            extensionSelector.Visibility = ViewStates.Invisible;
        }

        public void ShowCameraControls()
        {
            cameraShutterButton.Visibility = ViewStates.Visible;
            switchLensButton.Visibility = ViewStates.Visible;
            extensionSelector.Visibility = ViewStates.Visible;
        }

        public void HideCameraControls()
        {
            cameraShutterButton.Visibility = ViewStates.Invisible;
            switchLensButton.Visibility = ViewStates.Invisible;
        }

        public void EnableCameraShutter(bool isEnabled)
        {
            cameraShutterButton.Enabled = isEnabled;
        }

        public void EnableSwitchLens(bool isEnabled)
        {
            switchLensButton.Enabled = isEnabled;
        }

        public void ShowCaptureError(string errorMessage)
        {
            Toast.MakeText(context, errorMessage, ToastLength.Long).Show();
        }

        public void HidePermissionsRequest()
        {
            permissionsRationaleContainer.Visibility = ViewStates.Invisible;
        }

        public void ShowPermissionsRequest(bool shouldShowRationale)
        {
            permissionsRationaleContainer.Visibility = ViewStates.Visible;
            if (shouldShowRationale)
            {
                permissionsRationale.Text =
                    context.GetString(Resource.String.camera_permissions_request_with_rationale);
            }
            else
            {
                permissionsRationale.Text = context.GetString(Resource.String.camera_permissions_request);
            }
        }

        void OnItemClick(View view)
        {
            var layoutManager = extensionSelector.GetLayoutManager() as LinearLayoutManager;
            if (layoutManager == null) return;
            var viewMiddle = view.Left + view.Width / 2;
            var middle = layoutManager.Width / 2;
            var dx = viewMiddle - middle;
            extensionSelector.SmoothScrollBy(dx, 0);
        }
    }
}
