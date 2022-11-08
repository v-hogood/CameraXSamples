using Android.App;
using Java.Lang;
using static AndroidX.Lifecycle.ViewModelProvider;

namespace CameraXExtensions
{
    //
    // Creates ViewModel instances of [CameraExtensionsViewModel] to support injection of [Application]
    // and [ImageCaptureRepository]
    //
    public class CameraExtensionsViewModelFactory :  AndroidViewModelFactory
    {
        public CameraExtensionsViewModelFactory(
            Application application,
            ImageCaptureRepository imageCaptureRepository) :
            base(application)
        {
            this.application = application;
            this.imageCaptureRepository = imageCaptureRepository;
        }
        private Application application;
        private ImageCaptureRepository imageCaptureRepository;

        public override Object Create(Class modelClass)
        {
            return new CameraExtensionsViewModel(application, imageCaptureRepository);
        }
    }
}
