using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;

namespace musicplayer.Models
{
    public class MusicLibrary
    {
        public ObservableCollection<Album> Albums { get; set; } = new ObservableCollection<Album>();

        public Album? SelectedAlbum { get; set; }
    }
}