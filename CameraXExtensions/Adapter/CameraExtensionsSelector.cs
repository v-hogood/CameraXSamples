using Android.Graphics;
using Android.Views;
using Android.Widget;
using AndroidX.RecyclerView.Widget;
using Java.Lang;

namespace CameraXExtensions
{
    //
    // Adapter used to display CameraExtensionItems in a RecyclerView.
    //
    public class CameraExtensionsSelectorAdapter : ListAdapter
    {
        public CameraExtensionsSelectorAdapter(View.IOnClickListener onItemClick) :
            base(new ItemCallback())
        {
            this.onItemClick = onItemClick;
        }
        View.IOnClickListener onItemClick;

        public override RecyclerView.ViewHolder OnCreateViewHolder(ViewGroup parent, int viewType)
        {
            return new CameraExtensionItemViewHolder(
                LayoutInflater.From(parent.Context)
                    .Inflate(Resource.Layout.view_extension_type, parent, false) as TextView,
                onItemClick
            );
        }

        public override void OnBindViewHolder(RecyclerView.ViewHolder holder, int position)
        {
            (holder as CameraExtensionItemViewHolder).Bind(GetItem(position) as CameraExtensionItem);
        }

        internal class ItemCallback : DiffUtil.ItemCallback
        {
            public override bool AreItemsTheSame(Object p0, Object p1)
            {
                CameraExtensionItem oldItem = p0 as CameraExtensionItem;
                CameraExtensionItem newItem = p1 as CameraExtensionItem;
                return oldItem.ExtensionMode == newItem.ExtensionMode;
            }

            public override bool AreContentsTheSame(Object p0, Object p1)
            {
                CameraExtensionItem oldItem = p0 as CameraExtensionItem;
                CameraExtensionItem newItem = p1 as CameraExtensionItem;
                return oldItem.Selected == newItem.Selected;

            }
        }
    }

    public class CameraExtensionItemViewHolder : RecyclerView.ViewHolder
    {
        public CameraExtensionItemViewHolder(
            TextView extensionView,
            View.IOnClickListener onItemClick) :
            base(extensionView)
        {
            this.extensionView = extensionView;
            extensionView.SetOnClickListener(onItemClick);
        }
        TextView extensionView;

        internal void Bind(CameraExtensionItem extensionModel)
        {
            extensionView.Text = extensionModel.Name;
            if (extensionModel.Selected)
            {
                extensionView.SetBackgroundResource(Resource.Drawable.pill_selected_background);
                extensionView.SetTextColor(Color.Black);
            }
            else
            {
                extensionView.SetBackgroundResource(Resource.Drawable.pill_unselected_background);
                extensionView.SetTextColor(Color.White);
            }
        }
    }
}
