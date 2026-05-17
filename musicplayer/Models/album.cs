using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

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
