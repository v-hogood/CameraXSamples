using Android.OS;
using Android.Views;
using Android.Widget;
using AndroidX.Fragment.App;
using Bumptech.Glide;

namespace CameraXBasic.Fragments
{
    // Fragment used for each individual page showing a photo inside of GalleryFragment
    public class PhotoFragment : Fragment
    {
        protected PhotoFragment()
            : base()
        {
        }

        public static PhotoFragment Create(Java.IO.File image)
        {
            new Bundle().PutString("file_name", image.AbsolutePath);
            return new PhotoFragment();
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return new ImageView(this.Context);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);
            string filename = savedInstanceState.GetString("file_name");
            if (string.IsNullOrEmpty(filename))
            {
                Glide.With(view).Load(Resource.Drawable.ic_photo).Into(view);
            }
            else
            {
                Glide.With(view).Load(new Java.IO.File(filename)).Into(view);
            }
        }
    }
}
