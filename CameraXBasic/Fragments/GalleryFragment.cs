using Android.Content;
using Android.Media;
using Android.OS;
using Android.Views;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Navigation;
using AndroidX.ViewPager2.Adapter;
using AndroidX.ViewPager2.Widget;
using CameraXBasic.Utils;
using static AndroidX.Lifecycle.LifecycleOwnerKt;
using AlertDialog = AndroidX.AppCompat.App.AlertDialog;
using Fragment = AndroidX.Fragment.App.Fragment;
using FragmentManager = AndroidX.Fragment.App.FragmentManager;

namespace CameraXBasic.Fragments
{
    // Fragment used to present the user with a gallery of photos taken
    [Android.App.Activity(Name = "com.android.example.cameraxbasic.fragments.GalleryFragment")]
    public class GalleryFragment : Fragment
    {
        private List<MediaStoreFile> mediaList;
        private bool hasMediaItems;

        // Adapter class used to present a fragment containing one photo or video as a page
        public class MediaPagerAdapter : FragmentStateAdapter
        {
            public MediaPagerAdapter(GalleryFragment f, FragmentManager fm,
                List<MediaStoreFile> mediaList)
                : base(fm, f.Lifecycle)
            {
                this.mediaList = mediaList;
            }
            private List<MediaStoreFile> mediaList;

            public override int ItemCount => mediaList.Count;
            public override Fragment CreateFragment(int position) =>
                PhotoFragment.Create(mediaList[position]);
            public override long GetItemId(int position) =>
                mediaList[position].Id;
            public override bool ContainsItem(long itemId) =>
                mediaList.Exists(it => it.Id == itemId);
            public void SetMediaListAndNotify(List<MediaStoreFile> mediaList)
            {
                this.mediaList = mediaList;
                NotifyDataSetChanged();
            }
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            GetLifecycleScope(this).Launch(() =>
            {
                // Get images this app has access to from MediaStore
                mediaList = new MediaStoreUtils(RequireContext()).GetImages();
                hasMediaItems = mediaList.Any();
            });
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_gallery, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            GetLifecycleScope(this).Launch(() =>
            {
                view.FindViewById<ImageButton>(Resource.Id.delete_button).Enabled = hasMediaItems;
                view.FindViewById<ImageButton>(Resource.Id.share_button).Enabled = hasMediaItems;
            });

            // Populate the ViewPager and implement a cache of two media items
            var photoViewPager = view.FindViewById<ViewPager2>(Resource.Id.photo_view_pager);
            photoViewPager.OffscreenPageLimit = 2;
            photoViewPager.Adapter = new MediaPagerAdapter(this, ChildFragmentManager, mediaList);
            (photoViewPager.Adapter as MediaPagerAdapter)
                .SetMediaListAndNotify(mediaList);

            // Make sure that the cutout "safe area" avoids the screen notch if any
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                // Use extension method to pad "inside" view containing UI using display cutout's bounds
                view.FindViewById<ConstraintLayout>(Resource.Id.cutout_safe_area).PadWithDisplayCutout();
            }

            // Handle back button press
            view.FindViewById<ImageButton>(Resource.Id.back_button).Click += (sender, e) =>
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container)
                    .NavigateUp();
            };

            // Handle share button press
            view.FindViewById<ImageButton>(Resource.Id.share_button).Click += (sender, e) =>
            {
                var mediaStoreFile = mediaList.ElementAtOrDefault(photoViewPager.CurrentItem);
                var mediaUri = mediaStoreFile.Uri;
                // Create a sharing intent
                var intent = new Intent();
                var mediaType = RequireContext().ContentResolver.GetType(mediaUri);
                // Set the appropriate intent extra, type, action and flags
                intent.SetType(mediaType);
                intent.SetAction(Intent.ActionSend);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);
                intent.PutExtra(Intent.ExtraStream, mediaUri);

                // Launch the intent letting the user choose which app to share with
                StartActivity(Intent.CreateChooser(intent, GetString(Resource.String.share_hint)));
            };

            // Handle delete button press
            view.FindViewById<ImageButton>(Resource.Id.delete_button).Click += (sender, e) =>
            {
                var mediaStoreFile = mediaList.ElementAtOrDefault(photoViewPager.CurrentItem);
                var mediaUri = mediaStoreFile.Uri;
                new AlertDialog.Builder(view.Context, Android.Resource.Style.ThemeMaterialDialog)
                    .SetTitle(GetString(Resource.String.delete_title))
                    .SetMessage(GetString(Resource.String.delete_dialog))
                    .SetIcon(Android.Resource.Drawable.IcDialogAlert)
                    .SetPositiveButton(Android.Resource.String.Ok, (sender2, e2) =>
                    {
                        // Delete current photo
                        RequireContext().ContentResolver.Delete(mediaUri, null, null);

                        // Send relevant broadcast to notify other apps of deletion
                        MediaScannerConnection.ScanFile(
                            view.Context, new[] { mediaUri.ToString() }, null, null
                        );

                        // Notify our view pager
                        mediaList.RemoveAt(photoViewPager.CurrentItem);
                        photoViewPager.Adapter.NotifyDataSetChanged();

                        // If all photos have been deleted, return to camera
                        if (!mediaList.Any())
                        {
                            Navigation.FindNavController(
                                RequireActivity(),
                                Resource.Id.fragment_container
                            ).NavigateUp();
                        }
                    })
                    .SetNegativeButton(Android.Resource.String.Cancel, handler: null)
                    .Create().ShowImmersive();
            };
        }
    }
}
