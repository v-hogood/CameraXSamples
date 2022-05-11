using AndroidX.Camera.Video;
using Java.Lang;

namespace CameraXVideo
{
    public static class VideoRecordEventExt
    {
        //
        // A helper extended function to get the name(string) for the VideoRecordEvent.
        //
        public static string GetNameString(this VideoRecordEvent videoRecordEvent)
        {
            return videoRecordEvent switch
            {
                VideoRecordEvent.Status => "Status",
                VideoRecordEvent.Start => "Started",
                VideoRecordEvent.Finalize => "Finalized",
                VideoRecordEvent.Pause => "Paused",
                VideoRecordEvent.Resume => "Resumed",
                _ => throw new IllegalArgumentException("Unknown VideoRecordEvent: $this")
            };
        }
    }
}
