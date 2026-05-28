// Song representation
namespace musicplayer.Models
{
    public class Song
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public uint ReleaseYear { get; set; }
        public string FilePath { get; set; } = "";
        public uint TrackNumber { get; set; }
        public TimeSpan Duration { get; set; }
        public bool IsLiked { get; set; }

        public string DisplayDuration
        {
            get
            {
                if (Duration.Hours > 0)
                    return Duration.ToString(@"h\:mm\:ss");

                return Duration.ToString(@"m\:ss");
            }
        }
    }
}
