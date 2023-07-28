using Android.Content;
using Android.Hardware;
using Android.Media;
using Android.Webkit;
using AndroidX.Core.Content;
using Java.Util;
using static AndroidX.Core.Net.UriKt;
using static Kotlin.IO.FilesKt;
using File = Java.IO.File;
using Object = Java.Lang.Object;
using Uri = Android.Net.Uri;

namespace CameraXExtensions
{
    //
    // Manages photo capture filename and location generation. Once a photo is captured and saved to
    // disk, the repository will also notify that the image has been created such that other
    // applications can view it.
    //
    public class ImageCaptureRepository : Object,
        MediaScannerConnection.IOnScanCompletedListener        
    {
        ImageCaptureRepository(File rootDirectory)
        {
            this.rootDirectory = rootDirectory;
        }
        File rootDirectory;

        const string Tag = "ImageCaptureRepository";

        private const string PhotoExtension = ".jpg";

        public static ImageCaptureRepository Create(Context context)
        {
            // Use external media if it is available and this app's file directory otherwise
            var appContext = context.ApplicationContext;
#pragma warning disable CA1422
            var mediaDir = new File(context.GetExternalMediaDirs().FirstOrDefault(),
#pragma warning restore CA1422
                appContext.Resources.GetString(Resource.String.app_name));
            mediaDir?.Mkdirs();
            var file = mediaDir.Exists() ? mediaDir : appContext.FilesDir;
            return new ImageCaptureRepository(file);
        }

        public void NotifyImageCreated(Context context, Uri savedUri)
        {
            var file = ToFile(savedUri);
            var fileProviderUri =
                FileProvider.GetUriForFile(context, context.PackageName + ".provider", file);
            context.SendBroadcast(new
#pragma warning disable 0618
#pragma warning disable CA1422
                Intent(Camera.ActionNewPicture, fileProviderUri)
#pragma warning restore CA1422
#pragma warning restore 0618
            );

            // Notify other apps so they can access the captured image
            var mimeType = MimeTypeMap.Singleton
                .GetMimeTypeFromExtension(GetExtension(file));

            MediaScannerConnection.ScanFile(
                context,
                new string[] { file.AbsolutePath },
                new string[] { mimeType },
                this);
        }

        public void OnScanCompleted(string path, Uri uri) { }

        public File CreateImageOutputFile() => new File(rootDirectory, GenerateFilename(PhotoExtension));

        private string GenerateFilename(string extension) =>
            UUID.RandomUUID().ToString() + extension;
    }
}
