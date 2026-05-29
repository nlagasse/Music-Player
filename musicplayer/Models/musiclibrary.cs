using System.Collections.ObjectModel;

// Collection of albums
namespace musicplayer.Models
{
    public class MusicLibrary
    {
        public ObservableCollection<Album> Albums { get; set; } = new ObservableCollection<Album>();

        public Album? SelectedAlbum { get; set; }

        public bool LikedOnlyModeEnabled { get; set; } = false;
        public bool ShuffleModeEnabled { get; set; } = false;
    }
}