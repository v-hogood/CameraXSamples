using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Android.Content;
using Android.Media;
using Android.OS;
using Android.Views;
using Android.Webkit;
using Android.Widget;
using AndroidX.AppCompat.App;
using AndroidX.ConstraintLayout.Widget;
using AndroidX.Core.Content;
using AndroidX.Fragment.App;
using AndroidX.Navigation;
using AndroidX.ViewPager.Widget;
using CameraXBasic.Utils;

namespace CameraXBasic.Fragments
{
    // Fragment used to present the user with a gallery of photos taken
    [Android.App.Activity(Name = "com.android.example.cameraxbasic.fragments.GalleryFragment")]
    public class GalleryFragment : Fragment
    {
        public static readonly HashSet<string> ExtensionWhitelist = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg" };

        private List<Java.IO.File> mediaList;

        // Adapter class used to present a fragment containing one photo or video as a page
        private class MediaPagerAdapter : FragmentStatePagerAdapter
        {
            private readonly GalleryFragment parent;

            public MediaPagerAdapter(GalleryFragment f, FragmentManager fm)
                : base(fm, BehaviorResumeOnlyCurrentFragment)
            {
                parent = f;
            }

            public override int Count => parent.mediaList.Count;
            public override Fragment GetItem(int position) => PhotoFragment.Create(parent.mediaList[position]);
            public override int GetItemPosition(Java.Lang.Object _) => PositionNone;
        }

        public override void OnCreate(Bundle savedInstanceState)
        {
            base.OnCreate(savedInstanceState);

            // Mark this as a retain fragment, so the lifecycle does not get restarted on config change
            RetainInstance = true;

            // Get root directory of media from navigation arguments
            var rootDirectory = new Java.IO.File(Arguments?.GetString("root_directory"));

            // Walk through all files in the root directory
            // We reverse the order of the list to present the last photos first
            mediaList = rootDirectory.ListFiles().Where(x =>
                ExtensionWhitelist.Contains(Path.GetExtension(x.Name).ToLower())).
                OrderByDescending(x => x.Name).ToList();
        }

        public override View OnCreateView(LayoutInflater inflater, ViewGroup container, Bundle savedInstanceState)
        {
            return inflater.Inflate(Resource.Layout.fragment_gallery, container, false);
        }

        public override void OnViewCreated(View view, Bundle savedInstanceState)
        {
            base.OnViewCreated(view, savedInstanceState);

            //Checking media files list
            if (!mediaList.Any())
            {
                view.FindViewById<ImageButton>(Resource.Id.delete_button).Enabled = false;
                view.FindViewById<ImageButton>(Resource.Id.share_button).Enabled = false;
            }

            // Populate the ViewPager and implement a cache of two media items
            var mediaViewPager = view.FindViewById<ViewPager>(Resource.Id.photo_view_pager);
            mediaViewPager.OffscreenPageLimit = 2;
            mediaViewPager.Adapter = new MediaPagerAdapter(this, this.ChildFragmentManager);

            // Make sure that the cutout "safe area" avoids the screen notch if any
            if (Build.VERSION.SdkInt >= BuildVersionCodes.P)
            {
                // Use extension method to pad "inside" view containing UI using display cutout's bounds
                view.FindViewById<ConstraintLayout>(Resource.Id.cutout_safe_area).PadWithDisplayCutout();
            }

            // Handle back button press
            view.FindViewById<ImageButton>(Resource.Id.back_button).Click += (sender, e) =>
            {
                Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).NavigateUp();
            };

            // Handle share button press
            view.FindViewById<ImageButton>(Resource.Id.share_button).Click += (sender, e) =>
            {
                Java.IO.File mediaFile = mediaList[mediaViewPager.CurrentItem];
                // Create a sharing intent
                var intent = new Intent();
                // Infer media type from file extension
                string mediaType = MimeTypeMap.Singleton.GetMimeTypeFromExtension(MimeTypeMap.GetFileExtensionFromUrl(mediaFile.Path));
                // Get URI from our FileProvider implementation
                Android.Net.Uri uri = FileProvider.GetUriForFile(view.Context, Context.PackageName + ".provider", mediaFile);
                // Set the appropriate intent extra, type, action and flags
                intent.PutExtra(Intent.ExtraStream, uri);
                intent.SetType(mediaType);
                intent.SetAction(Intent.ActionSend);
                intent.AddFlags(ActivityFlags.GrantReadUriPermission);

                // Launch the intent letting the user choose which app to share with
                StartActivity(Intent.CreateChooser(intent, GetString(Resource.String.share_hint)));
            };

            // Handle delete button press
            view.FindViewById<ImageButton>(Resource.Id.delete_button).Click += (sender, e) =>
            {
                Java.IO.File mediaFile = mediaList[mediaViewPager.CurrentItem];
                new AlertDialog.Builder(view.Context, Android.Resource.Style.ThemeMaterialDialog)
                .SetTitle(GetString(Resource.String.delete_title))
                .SetMessage(GetString(Resource.String.delete_dialog))
                .SetIcon(Android.Resource.Drawable.IcDialogAlert)
                .SetPositiveButton(Android.Resource.String.Yes, (sender2, e2) =>
                {
                    // Delete current photo
                    mediaFile.Delete();

                    // Send relevant broadcast to notify other apps of deletion
                    MediaScannerConnection.ScanFile(view.Context, new[] { mediaFile.AbsolutePath }, null, null);

                    // Notify our view pager
                    mediaList.RemoveAt(mediaViewPager.CurrentItem);
                    mediaViewPager.Adapter.NotifyDataSetChanged();

                    // If all photos have been deleted, return to camera
                    if (!mediaList.Any())
                    {
                        Navigation.FindNavController(RequireActivity(), Resource.Id.fragment_container).NavigateUp();
                    }
                })
                .SetNegativeButton(Android.Resource.String.No, handler: null)
                .Create().ShowImmersive();
            };
        }
    }
}
