using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.AppCompat.App;
using AlertDialog = Android.App.AlertDialog;

namespace HdrViewfinder
{
    public class MessageDialogFragment : AppCompatDialogFragment
    {
        private const string ArgMessageInt = "message_int";
        private const string ArgMessageString = "message_string";

        public static MessageDialogFragment newInstance(int message)
        {
            MessageDialogFragment fragment = new MessageDialogFragment();
            Bundle args = new Bundle();
            args.PutInt(ArgMessageInt, message);
            fragment.Arguments = args;
            return fragment;
        }

        public static MessageDialogFragment newInstance(string message)
        {
            MessageDialogFragment fragment = new MessageDialogFragment();
            Bundle args = new Bundle();
            args.PutString(ArgMessageString, message);
            fragment.Arguments = args;
            return fragment;
        }

        public override Dialog OnCreateDialog(Bundle savedInstanceState)
        {
            AlertDialog.Builder builder = new AlertDialog.Builder(Activity);
            builder.SetPositiveButton(Android.Resource.String.Ok, (IDialogInterfaceOnClickListener) null);
            Bundle args = Arguments;
            if (args.ContainsKey(ArgMessageInt))
            {
                builder.SetMessage(args.GetInt(ArgMessageInt));
            }
            else if (args.ContainsKey(ArgMessageString))
            {
                builder.SetMessage(args.GetString(ArgMessageString));
            }
            return builder.Create();
        }
    }
}
