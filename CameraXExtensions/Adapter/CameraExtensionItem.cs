using Object = Java.Lang.Object;

namespace CameraXExtensions
{
    //
    // Defines the item model for a camera extension displayed by the adapter.
    // @see CameraExtensionsSelectorAdapter
    //
    public class CameraExtensionItem : Object
    {
        public CameraExtensionItem() { }
        public CameraExtensionItem(CameraExtensionItem cameraExtensionItem)
        {
            ExtensionMode = cameraExtensionItem.ExtensionMode;
            Name = cameraExtensionItem.Name;
            Selected = cameraExtensionItem.Selected;
        }
        public int ExtensionMode;
        public string Name;
        public bool Selected;
    }
}
