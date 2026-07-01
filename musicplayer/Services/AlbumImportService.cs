using musicplayer.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows;
using System.Security.Cryptography;

namespace musicplayer.Services
{
    public static class AlbumImportService
    {
        public static Album? CreateAlbumFromFolder(string folderPath)
        {
            List<string> albumArtPaths = ImageService.GetAlbumArtPaths(folderPath);

            if (albumArtPaths.Count == 0)
            {
                MessageBox.Show(
                    "No jpg or png image found in this folder:\n" + folderPath,
                    "Missing Album Art",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return null;
            }

            Album album = new Album
            {
                Title = Path.GetFileName(folderPath),
                Artist = "Unknown Artist",
                FolderPath = folderPath,
                CoverPath = albumArtPaths[0],
                BackCoverPath = albumArtPaths.Count > 1 ? albumArtPaths[1] : "",
                AlbumArtPaths = albumArtPaths
            };

            List<Song> songs = Directory.GetFiles(folderPath)
            .Where(file =>
                file.EndsWith(".mp3", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".flac", StringComparison.OrdinalIgnoreCase))
            .Select(CreateSongFromFile)
            .OrderBy(song => song.TrackNumber == 0 ? uint.MaxValue : song.TrackNumber)
            .ThenBy(song => song.FilePath)
            .ToList();

            if (songs.Count == 0)
            {
                MessageBox.Show(
                    "No MP3 or FLAC songs found in this folder:\n" + folderPath,
                    "Missing Songs",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );

                return null;
            }

            foreach (Song song in songs)
            {
                album.Songs.Add(song);
            }

            Song? firstSongWithArtist = songs.FirstOrDefault(song => !string.IsNullOrWhiteSpace(song.Artist));

            if (firstSongWithArtist != null)
                album.Artist = firstSongWithArtist.Artist;
            else
                album.Artist = "Unknown Artist";

            Song? firstSongWithYear = songs.FirstOrDefault(song => song.ReleaseYear > 0);

            if (firstSongWithYear != null)
                album.ReleaseYear = firstSongWithYear.ReleaseYear;

            album.Title = MetadataCleanup.CleanAlbumTitle(album.Title, album.Artist, album.ReleaseYear);

            return album;
        }

        public static Song? CreateSongFromFilePath(string filePath)
        {
            if (!File.Exists(filePath))
                return null;

            if (!MetadataCleanup.IsAudioFile(filePath))
                return null;

            return CreateSongFromFile(filePath);
        }

        //public void AddAlbumFromFolderPath(string folderPath)
        //{
        //    if (!Directory.Exists(folderPath))
        //        return;

        //    Album? album = CreateAlbumFromFolder(folderPath);

        //    if (album == null)
        //        return;

        //    AppData.Library.Albums.Add(album);
        //    LibraryStorage.SaveLibrary();

        //    RefreshAlbumOrder();
        //}

        //private string GetCurrentPlayerArtPath(Album album)
        //{
        //    if (album.AlbumArtPaths != null && album.AlbumArtPaths.Count > 0)
        //    {
        //        if (album.PlayerArtIndex < 0 || album.PlayerArtIndex >= album.AlbumArtPaths.Count)
        //            album.PlayerArtIndex = album.AlbumArtPaths.Count > 1 ? 1 : 0;

        //        return album.AlbumArtPaths[album.PlayerArtIndex];
        //    }

        //    if (!string.IsNullOrWhiteSpace(album.BackCoverPath))
        //        return album.BackCoverPath;

        //    return album.CoverPath;
        //}

        private static List<string> ExtractSongArtwork(string songFilePath, TagLib.File tagFile)
        {
            List<string> artPaths = new List<string>();

            if (tagFile.Tag.Pictures == null || tagFile.Tag.Pictures.Length == 0)
                return artPaths;

            string appDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "musicplayer",
                "SongArtwork"
            );

            Directory.CreateDirectory(appDataFolder);

            string safeSongName = Path.GetFileNameWithoutExtension(songFilePath);

            foreach (TagLib.IPicture picture in tagFile.Tag.Pictures)
            {
                if (picture.Data == null || picture.Data.Data == null || picture.Data.Data.Length == 0)
                    continue;

                string extension = ".jpg";

                if (picture.MimeType != null &&
                    picture.MimeType.Contains("png", StringComparison.OrdinalIgnoreCase))
                {
                    extension = ".png";
                }

                string hash = Convert.ToHexString(
                    System.Security.Cryptography.SHA1.HashData(
                        System.Text.Encoding.UTF8.GetBytes(songFilePath + picture.Description + artPaths.Count)
                    )
                );

                string outputPath = Path.Combine(appDataFolder, hash + extension);

                if (!System.IO.File.Exists(outputPath))
                    System.IO.File.WriteAllBytes(outputPath, picture.Data.Data);

                artPaths.Add(outputPath);
            }

            return artPaths;
        }

        private static Song CreateSongFromFile(string songFile)
        {
            string fileNameTitle = Path.GetFileNameWithoutExtension(songFile);

            try
            {
                TagLib.File tagFile = TagLib.File.Create(songFile);

                string title = tagFile.Tag.Title;

                if (string.IsNullOrWhiteSpace(title))
                    title = fileNameTitle;

                string artist = "";

                if (tagFile.Tag.Performers.Length > 0)
                    artist = tagFile.Tag.Performers[0];
                else if (tagFile.Tag.AlbumArtists.Length > 0)
                    artist = tagFile.Tag.AlbumArtists[0];

                title = MetadataCleanup.CleanSongTitle(title, artist);

                return new Song
                {
                    Title = title,
                    Artist = artist,
                    ReleaseYear = tagFile.Tag.Year,
                    FilePath = songFile,
                    TrackNumber = tagFile.Tag.Track,
                    Duration = tagFile.Properties.Duration,
                    AlbumArtPaths = ExtractSongArtwork(songFile, tagFile)
                };
            }
            catch
            {
                return new Song
                {
                    Title = MetadataCleanup.CleanSongTitle(fileNameTitle, ""),
                    Artist = "",
                    ReleaseYear = 0,
                    FilePath = songFile,
                    TrackNumber = 0,
                    Duration = TimeSpan.Zero,
                    AlbumArtPaths = new List<string>()
                };
            }
        }


    }
}
