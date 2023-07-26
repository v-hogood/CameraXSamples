using Android.Animation;
using Android.Content;
using Android.Util;
using Android.Views;
using AndroidX.Camera.View;
using AndroidX.Core.View;
using AndroidX.DynamicAnimation;
using AndroidX.Lifecycle;
using AndroidX.RecyclerView.Widget;
using Kotlin.Coroutines;
using Xamarin.KotlinX.Coroutines.Flow;
using static AndroidX.Core.View.ViewKt;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using static Xamarin.KotlinX.Coroutines.Flow.StateFlowKt;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace CameraXExtensions
{
    //
    // Displays the camera preview and captured photo.
    // Encapsulates the details of how the screen is constructed and exposes a set of explicit
    // operations clients can perform on the screen.
    //
    class CameraExtensionsScreen : RecyclerView.OnScrollListener,
        IContinuation,
        View.IOnClickListener,
        View.IOnTouchListener,
        Animator.IAnimatorListener,
        DynamicAnimation.IOnAnimationEndListener
    {
        // animation constants for focus point
        private const float SpringStiffnessAlphaOut = 100f;
        private const float SpringStiffness = 800f;
        private const float SpringDampingRatio = 0.35f;

        private View root;
        private Context context;

        private View cameraShutterButton;
        private ImageView photoPreview;
        private View closePhotoPreview;
        private ImageView switchLensButton;
        private RecyclerView extensionSelector;
        private CameraExtensionsSelectorAdapter extensionsAdapter;
        private View focusPointView;
        private View permissionsRationaleContainer;
        private TextView permissionsRationale;
        private TextView permissionsRequestButton;

        public PreviewView PreviewView;

        private IMutableStateFlow action = MutableStateFlow(new CameraUiAction());
        public IFlow Action;

        public static ILifecycleOwner FindViewTreeLifecycleOwner(View root) =>
            root.Context as ILifecycleOwner;

        public ICoroutineContext Context => GetLifecycleScope(FindViewTreeLifecycleOwner(root)).CoroutineContext;

        public void ResumeWith(Object result) { }

        public void OnClick(View v)
        {
            if (v.Id == Resource.Id.cameraShutter)
            {
                GetLifecycleScope(FindViewTreeLifecycleOwner(root)).Launch(() =>
                    action.Emit(new CameraUiAction.ShutterButtonClick(), this));
            }
            else if (v.Id == Resource.Id.switchLens)
            {
                SwitchLens();
            }
            else if (v.Id == Resource.Id.closePhotoPreview)
            {
                GetLifecycleScope(FindViewTreeLifecycleOwner(root)).Launch(() =>
                    action.Emit(new CameraUiAction.ClosePhotoPreviewClick(), this));
            }
            else if (v.Id == Resource.Id.permissionsRequestButton)
            {
                GetLifecycleScope(FindViewTreeLifecycleOwner(root)).Launch(() =>
                    action.Emit(new CameraUiAction.RequestPermissionClick(), this));
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

        public GestureDetectorCompat gestureDetector;
        public ScaleGestureDetector scaleGestureDetector;

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
            GetLifecycleScope(FindViewTreeLifecycleOwner(root)).Launch(() => action.Emit(new CameraUiAction.SelectCameraExtension
                { Extension = it.ExtensionMode }, this));
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
            focusPointView = root.FindViewById(Resource.Id.focusPoint);
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

            gestureDetector = new GestureDetectorCompat(context, new SimpleGestureListener(this));

            scaleGestureDetector = new ScaleGestureDetector(context, new ScaleGestureListener(this));

            PreviewView.SetOnTouchListener(this);
        }

        public class SimpleGestureListener : GestureDetector.SimpleOnGestureListener
        {
            public SimpleGestureListener(CameraExtensionsScreen parent) { this.parent = parent; }
            CameraExtensionsScreen parent;

            public override bool OnDown(MotionEvent e) => true;

            public override bool OnSingleTapUp(MotionEvent e)
            {
                var meteringPointFactory = parent.PreviewView.MeteringPointFactory;
                var focusPoint = meteringPointFactory.CreatePoint(e.GetX(), e.GetY());
                GetLifecycleScope(FindViewTreeLifecycleOwner(parent.root)).Launch(() =>
                    parent.action.Emit(new CameraUiAction.Focus { meteringPoint = focusPoint }, parent));
                parent.ShowFocusPoint(e.GetX(), e.GetY());
                return true;
            }

            public override bool OnDoubleTap(MotionEvent e)
            {
                parent.SwitchLens();
                return true;
            }
        }

        public class ScaleGestureListener : ScaleGestureDetector.SimpleOnScaleGestureListener
        {
            public ScaleGestureListener(CameraExtensionsScreen parent) { this.parent = parent; }
            CameraExtensionsScreen parent;

            public override bool OnScale(ScaleGestureDetector detector)
            {
                GetLifecycleScope(FindViewTreeLifecycleOwner(parent.root)).Launch(() =>
                    parent.action.Emit(new CameraUiAction.Scale { scaleFactor = detector.ScaleFactor }, parent));
                return true;
            }
        }

        public bool OnTouch(View v, MotionEvent e)
        {
            var didConsume = scaleGestureDetector.OnTouchEvent(e);
            if (!scaleGestureDetector.IsInProgress)
            {
                didConsume = gestureDetector.OnTouchEvent(e);
            }
            return didConsume;
        }

        public void SetCaptureScreenViewState(CaptureScreenViewState state)
        {
            SetCameraScreenViewState(state.cameraPreviewScreenViewState);
            if (state.postCaptureScreenViewState is PostCaptureScreenViewState.HiddenViewState)
                HidePhoto();
            else if (state.postCaptureScreenViewState is PostCaptureScreenViewState.VisibleViewState)
                ShowPhoto(((PostCaptureScreenViewState.VisibleViewState)state.postCaptureScreenViewState).uri);
        }

        public void ShowCaptureError(string errorMessage)
        {
            Toast.MakeText(context, errorMessage, ToastLength.Long).Show();
        }

        public void HidePermissionsRequest()
        {
            SetVisible(permissionsRationaleContainer, false);
        }

        public void ShowPermissionsRequest(bool shouldShowRationale)
        {
            SetVisible(permissionsRationaleContainer, true);
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

        public void ShowPhoto(Uri uri)
        {
            if (uri == null) return;
            SetVisible(photoPreview, true);
            // photoPreview.Load(uri);
            SetVisible(closePhotoPreview, true);
        }

        public void HidePhoto()
        {
            SetVisible(photoPreview, false);
            SetVisible(closePhotoPreview, false);
        }

        private void SetCameraScreenViewState(CameraPreviewScreenViewState state)
        {
            cameraShutterButton.Enabled = state.shutterButtonViewState.isEnabled;
            SetVisible(cameraShutterButton, state.shutterButtonViewState.isVisible);

            switchLensButton.Enabled = state.switchLensButtonViewState.isEnabled;
            SetVisible(switchLensButton, state.switchLensButtonViewState.isVisible);

            SetVisible(extensionSelector, state.extensionsSelectorViewState.isVisible);
            extensionsAdapter.SubmitList(state.extensionsSelectorViewState.extensions);
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

        public void SwitchLens()
        {
            GetLifecycleScope(FindViewTreeLifecycleOwner(root)).Launch(() =>
                action.Emit(new CameraUiAction.SwitchCameraClick(), this));
            switchLensButton.Animate().Rotation(180f);
            switchLensButton.Animate().SetDuration(300L);
            switchLensButton.Animate().SetListener(this);
            switchLensButton.Animate().Start();
        }

        public void ShowFocusPoint(float x, float y)
        {
            View view = focusPointView;
            var drawable = new FocusPointDrawable();
            var strokeWidth = TypedValue.ApplyDimension(
                ComplexUnitType.Dip,
                3f,
                context.Resources.DisplayMetrics
            );
            drawable.SetStrokeWidth(strokeWidth);

            var alphaAnimation = new SpringAnimation(view, DynamicAnimation.Alpha, 1f);
            alphaAnimation.Spring.SetStiffness(SpringStiffnessAlphaOut);
            alphaAnimation.Spring.SetDampingRatio(SpringDampingRatio);

            alphaAnimation.AddEndListener(this);

            var scaleAnimationX = new SpringAnimation(view, DynamicAnimation.ScaleX, 1f);
            alphaAnimation.Spring.SetStiffness(SpringStiffness);
            alphaAnimation.Spring.SetDampingRatio(SpringDampingRatio);

            var scaleAnimationY = new SpringAnimation(view, DynamicAnimation.ScaleY, 1f);
            alphaAnimation.Spring.SetStiffness(SpringStiffness);
            alphaAnimation.Spring.SetDampingRatio(SpringDampingRatio);

            view.Background = drawable;
            SetVisible(view, true);
            view.TranslationX = x - view.Width / 2f;
            view.TranslationY = y - view.Height / 2f;
            view.Alpha = 0f;
            view.ScaleX = 1.5f;
            view.ScaleY = 1.5f;

            alphaAnimation.Start();
            scaleAnimationX.Start();
            scaleAnimationY.Start();
        }

        public void OnAnimationEnd(DynamicAnimation animation, bool canceled, float value, float velocity)
        {
            var springForce = new SpringForce();
            springForce.SetStiffness(SpringStiffnessAlphaOut);
            springForce.SetDampingRatio(SpringForce.DampingRatioNoBouncy);
            new SpringAnimation(focusPointView, DynamicAnimation.Alpha, 0f).Start();
        }
    }
}
