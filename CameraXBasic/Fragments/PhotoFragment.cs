using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Bumptech.Glide;

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
            string filename = Arguments?.GetString(FileNameKey);
            if (string.IsNullOrEmpty(filename))
            {
                Glide.With(view).Load(Resource.Drawable.ic_photo).Into(view as ImageView);
            }
            else
            {
                Glide.With(view).Load(new Java.IO.File(filename)).Into(view as ImageView);
            }
        }

        private const string FileNameKey = "file_name";

        public static PhotoFragment Create(Java.IO.File image)
        {
            var bundle = new Bundle();
            bundle.PutString(FileNameKey, image.AbsolutePath);
            return new PhotoFragment { Arguments = bundle };
        }
    }
}
