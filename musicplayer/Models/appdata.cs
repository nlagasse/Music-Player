using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace musicplayer.Models
{
    public static class AppData
    {
        public static MusicLibrary Library { get; set; } = new MusicLibrary();
    }
}