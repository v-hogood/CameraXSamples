using System.Collections.Generic;
using Android.Content;
using Android.Database;
using Android.Net;
using Android.OS;
using Android.Provider;

namespace CameraXBasic.Utils
{
    public class MediaStoreUtils
    {
        public MediaStoreUtils(Context context)
        {
            this.context = context;

            MediaStoreCollection =
                Build.VERSION.SdkInt >= BuildVersionCodes.Q ?
                    MediaStore.Images.Media.GetContentUri(MediaStore.VolumeExternal) :
                    Uri.FromFile(context.GetExternalFilesDir(null));
        }
        Context context;

        public Uri MediaStoreCollection;

        private ICursor GetMediaStoreImageCursor(Uri mediaStoreCollection)
        {
            var projection = new string[] { imageIdColumnIndex };
            var sortOrder = "DATE_ADDED DESC";
            return context.ContentResolver.Query(
                mediaStoreCollection, projection, null, null, sortOrder
            );
        }

        public List<MediaStoreFile> GetImages()
        {
            var files = new List<MediaStoreFile>();
            if (MediaStoreCollection == null) return files;

            var cursor = GetMediaStoreImageCursor(MediaStoreCollection);
            var imageIdColumn = cursor?.GetColumnIndexOrThrow(imageIdColumnIndex);

            if (cursor != null && imageIdColumn != null)
            {
                while (cursor.MoveToNext())
                {
                    var id = cursor.GetLong((int)imageIdColumn);
                    var contentUri = ContentUris.WithAppendedId(
                        MediaStore.Images.Media.ExternalContentUri,
                        id
                    );
                    files.Add(new MediaStoreFile { Uri = contentUri, Id = id });
                }
            }

            return files;
        }

        private const string imageIdColumnIndex = /*MediaStore.Images.Media._ID*/"_id";
    }

    public struct MediaStoreFile
    {
        public Uri Uri;
        public long Id;
    }
}
