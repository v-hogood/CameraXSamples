using Android.Net;
using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Bumptech.Glide;
using CameraXBasic.Utils;

namespace CameraXBasic.Fragments
{
    // Fragment used for each individual page showing a photo inside of GalleryFragment
    [Android.App.Activity(Name = "com.android.example.cameraxbasic.fragments.PhotoFragment")]
    public class PhotoFragment : Fragment
    {
        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return new ImageView(this.Context);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            var uri = Arguments?.GetString(UriKey);
            if (string.IsNullOrEmpty(uri))
            {
                Glide.With(view).Load(Resource.Drawable.ic_photo).Into(view as ImageView);
            }
            else
            {
                Glide.With(view).Load(Uri.Parse(uri)).Into(view as ImageView);
            }
        }

        private const string UriKey = "uri";

        public static PhotoFragment Create(MediaStoreFile mediaStoreFile)
        {
            var bundle = new Bundle();
            bundle.PutString(UriKey, mediaStoreFile.Uri.ToString());
            return new PhotoFragment { Arguments = bundle };
        }
    }
}
