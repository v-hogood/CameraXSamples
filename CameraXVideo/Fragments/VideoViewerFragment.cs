using Android.Database;
using Android.Media;
using Android.Net;
using Android.OS;
using Android.Provider;
using Android.Util;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Java.Lang;

namespace CameraXVideo
{
    //
    // VideoViewerFragment:
    //      Accept MediaStore URI and play it with VideoView (Also displaying file size and location)
    //      Note: Might be good to retrieve the encoded file mime type (not based on file type)
    //
    [Android.App.Activity(Name = "com.android.example.cameraxvideo.fragments.VideoViewerFragment")]
    class VideoViewerFragment : Fragment,
        MediaScannerConnection.IOnScanCompletedListener
    {
        // These properties are only valid between OnCreateView and OnDestroyView.
        VideoView videoViewer;
        TextView videoViewerTips;

        public override View OnCreateView(
            LayoutInflater inflater,
            ViewGroup container,
            Bundle savedInstanceState)
        {
            View view = inflater.Inflate(Resource.Layout.fragment_video_viewer, container, false);

            videoViewer = view.FindViewById<VideoView>(Resource.Id.video_viewer);
            videoViewerTips = view.FindViewById<TextView>(Resource.Id.video_viewer_tips);

            // UI adjustment + hacking to display VideoView use tips / capture result
            var tv = new TypedValue();
            if (RequireActivity().Theme.ResolveAttribute(Android.Resource.Attribute.ActionBarSize, tv, true))
            {
                var actionBarHeight = TypedValue.ComplexToDimensionPixelSize(tv.Data, Resources.DisplayMetrics);
                videoViewerTips.SetY(videoViewerTips.GetY() - actionBarHeight);
            }

            return view;
        }

        public void OnScanCompleted(string path, Uri uri)
        {
            // playback video on main thread with VideoView
            if (uri != null)
            {
                Activity.RunOnUiThread(() => ShowVideo(uri));
            }
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            Uri uri = Uri.Parse(Arguments.GetString("uri"));

            if (Build.VERSION.SdkInt > BuildVersionCodes.P)
            {
                ShowVideo(uri);
            }
            else
            {
                // force MediaScanner to re-scan the media file.
                var path = GetAbsolutePathFromUri(uri);
                MediaScannerConnection.ScanFile(
                    Context, new string[] { path }, null, this);
            }
        }

        public override void OnDestroyView()
        {
            videoViewer = null;
            videoViewerTips = null;
            base.OnDestroyView();
        }

        //
        // A helper function to play the recorded video. Note that VideoView/MediaController auto-hides
        // the play control menus, touch on the video area would bring it back for 3 second.
        // This functionality not really related to capture, provided here for convenient purpose to view:
        //   - the captured video
        //   - the file size and location
        //
        private void ShowVideo(Uri uri)
        {
            var fileSize = GetFileSizeFromUri(uri);
            if (fileSize <= 0)
            {
                Log.Error("VideoViewerFragment", "Failed to get recorded file size, could not be played!");
                return;
            }

            var filePath = GetAbsolutePathFromUri(uri);
            var fileInfo = "FileSize: " + fileSize + "\n " + filePath;
            Log.Info("VideoViewerFragment", fileInfo);
            videoViewerTips.Text = fileInfo;

            var mc = new MediaController(RequireContext());
            videoViewer.SetVideoURI(uri);
            videoViewer.SetMediaController(mc);
            videoViewer.RequestFocus();
            videoViewer.Start();
            mc.Show(0);
        }

        //
        // A helper function to get the captured file location.
        //
        private string GetAbsolutePathFromUri(Uri contentUri)
        {
            ICursor cursor = null;
            try
            {
                cursor = RequireContext()
                    .ContentResolver
                    .Query(contentUri, new string[] { /*MediaStore.Images.Media.DATA*/"_data" }, null, null, null);
                if (cursor == null)
                {
                    return null;
                }
                var columnIndex = cursor.GetColumnIndexOrThrow(/*MediaStore.Images.Media.DATA*/"_data");
                cursor.MoveToFirst();
                return cursor.GetString(columnIndex);
            }
            catch (RuntimeException e)
            {
                Log.Error("VideoViewerFragment", string.Format(
                    "Failed in getting absolute path for Uri {0} with Exception {1]",
                    contentUri.ToString(), e.ToString()));
                return null;
            }
            finally
            {
                cursor?.Close();
            }
        }

        //
        // A helper function to retrieve the captured file size.
        //
        private long GetFileSizeFromUri(Uri contentUri)
        {
            var cursor = RequireContext()
                .ContentResolver
                .Query(contentUri, null, null, null, null);

            var sizeIndex = cursor.GetColumnIndex(IOpenableColumns.Size);
            cursor.MoveToFirst();

            return cursor.GetLong(sizeIndex);
        }
    }
}
