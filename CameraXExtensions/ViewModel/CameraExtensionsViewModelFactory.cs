using Java.Lang;
using static AndroidX.Lifecycle.ViewModelProvider;
using Object = Java.Lang.Object;

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

        public override Object Create(Class modelClass) => new CameraExtensionsViewModel(application, imageCaptureRepository);
    }
}
