using System.Collections.ObjectModel;

// Album representation 
namespace musicplayer.Models
{
    public class Album
    {
        public string Title { get; set; } = "";
        public string Artist { get; set; } = "";
        public uint ReleaseYear { get; set; }
        public string FolderPath { get; set; } = "";
        public string CoverPath { get; set; } = "";
        public string BackCoverPath { get; set; } = "";
        public List<string> AlbumArtPaths { get; set; } = new List<string>();
        public int PlayerArtIndex { get; set; } = 1;
        public long LastPlayedUtcTicks { get; set; } = 0;
        public bool IsPlaylist { get; set; } = false;
        public long CreatedUtcTicks { get; set; } = DateTime.UtcNow.Ticks;
        public bool HasCustomTitle { get; set; } = false;

        public ObservableCollection<Song> Songs { get; set; } = new ObservableCollection<Song>();

        public int SongCount
        {
            get
            {
                return Songs.Count;
            }
        }
    }
}
