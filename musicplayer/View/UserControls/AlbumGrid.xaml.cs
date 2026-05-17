using Microsoft.Win32;
using musicplayer.Models;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using TagLib;

namespace musicplayer.View.UserControls
{
    /// <summary>
    /// Interaction logic for AlbumGrid.xaml
    /// </summary>

    public partial class AlbumGrid : UserControl
    {
        private Album? selectedAlbum;
        public event Action<Album>? AlbumRemoved;
        public event Action<Album?>? SelectedAlbumChanged;
        public event Action<bool>? LikedOnlyModeChanged;
        public event Action<Album, Song>? AlbumMiddleClickedPlayRequested;

        private bool likedOnlyMode = false;
        public AlbumGrid()
        {
            InitializeComponent();
        }

        private void AddAlbum_Click(object sender, RoutedEventArgs e)
        {
            OpenFolderDialog openFolderDialog = new OpenFolderDialog();
            openFolderDialog.Title = "Select album folder to add";
            openFolderDialog.InitialDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            "Downloads");
            openFolderDialog.Multiselect = true;

            if (openFolderDialog.ShowDialog() == true)
            {
                string[] folderPaths = openFolderDialog.FolderNames;

                foreach (string folderPath in folderPaths)
                {
                    Album? album = CreateAlbumFromFolder(folderPath);

                    if (album == null)
                        continue;

                    AppData.Library.Albums.Add(album);

                    AddAlbumArtToGrid(album);

                    LibraryStorage.SaveLibrary();
                }
            }
        }

        private void LikedOnlyButton_Click(object sender, RoutedEventArgs e)
        {
            likedOnlyMode = !likedOnlyMode;

            if (likedOnlyMode)
            {
                LikedOnlyIcon.Source =
                    new BitmapImage(new Uri("/assets/icons/heart_filled.png", UriKind.Relative));
            }
            else
            {
                LikedOnlyIcon.Source =
                    new BitmapImage(new Uri("/assets/icons/heart_hollow.png", UriKind.Relative));
            }

            LikedOnlyModeChanged?.Invoke(likedOnlyMode);
        }

        private Album? CreateAlbumFromFolder(string folderPath)
        {
            List<string> albumArtPaths = GetAlbumArtPaths(folderPath);

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

            album.Title = CleanAlbumTitle(album.Title, album.Artist, album.ReleaseYear);

            return album;
        }

        private List<string> GetAlbumArtPaths(string folderPath)
        {
            List<string> imageFiles = Directory.GetFiles(folderPath)
            .Where(file =>
                file.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase) ||
                file.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            .OrderBy(file => file)
            .ToList();

            if (imageFiles.Count == 0)
                return new List<string>();

            string? frontCover = imageFiles.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).Equals("cover", StringComparison.OrdinalIgnoreCase) ||
                Path.GetFileNameWithoutExtension(file).Equals("front", StringComparison.OrdinalIgnoreCase));

            string? backCover = imageFiles.FirstOrDefault(file =>
                Path.GetFileNameWithoutExtension(file).Equals("back", StringComparison.OrdinalIgnoreCase));

            List<string> sortedArt = new List<string>();

            if (frontCover != null)
            {
                sortedArt.Add(frontCover);
            }
            else
            {
                sortedArt.Add(imageFiles[0]);
            }

            if (backCover != null && backCover != sortedArt[0])
            {
                sortedArt.Add(backCover);
            }
            else
            {
                string? secondImage = imageFiles.FirstOrDefault(file => file != sortedArt[0]);

                if (secondImage != null)
                    sortedArt.Add(secondImage);
            }

            foreach (string imageFile in imageFiles)
            {
                if (!sortedArt.Contains(imageFile))
                    sortedArt.Add(imageFile);
            }

            return sortedArt;
        }

        public void RefreshAlbumOrder()
        {
            AlbumsPanel.Children.Clear();

            List<Album> sortedAlbums = AppData.Library.Albums
                .OrderByDescending(album => album.LastPlayedUtcTicks)
                .ThenBy(album => album.Title)
                .ToList();

            foreach (Album album in sortedAlbums)
            {
                if (!System.IO.File.Exists(album.CoverPath))
                    continue;

                AddAlbumArtToGrid(album);
            }
        }

        private void AddAlbumArtToGrid(Album album)
        {
            Border albumBorder = new Border
            {
                Width = 118,
                Height = 118,
                Margin = new Thickness(5),
                Background = Brushes.Transparent,
                CornerRadius = new CornerRadius(6),
                Padding = new Thickness(4),
                Cursor = Cursors.Hand,
                Tag = album
            };

            albumBorder.ToolTip = new ToolTip
            {
                Content = album.Title,
                Background = new SolidColorBrush(Color.FromRgb(32, 33, 36)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12
            };

            ToolTipService.SetInitialShowDelay(albumBorder, 250);
            ToolTipService.SetShowDuration(albumBorder, 5000);
            ToolTipService.SetBetweenShowDelay(albumBorder, 100);

            BitmapImage coverImage = new BitmapImage();
            coverImage.BeginInit();
            coverImage.UriSource = new Uri(album.CoverPath);
            coverImage.CacheOption = BitmapCacheOption.OnLoad;
            coverImage.EndInit();
            coverImage.Freeze();

            Image albumImage = new Image
            {
                Source = coverImage,
                Stretch = Stretch.UniformToFill
            };

            RenderOptions.SetBitmapScalingMode(albumImage, BitmapScalingMode.HighQuality);
            RenderOptions.SetEdgeMode(albumImage, EdgeMode.Unspecified);

            Border imageBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = albumImage
            };

            albumBorder.Child = imageBorder;

            albumBorder.MouseEnter += (sender, e) =>
            {
                albumBorder.Background = new SolidColorBrush(Color.FromRgb(32, 33, 36));
            };

            albumBorder.MouseLeave += (sender, e) =>
            {
                albumBorder.Background = Brushes.Transparent;
            };

            ContextMenu contextMenu = new ContextMenu();

            MenuItem removeMenuItem = new MenuItem
            {
                Header = "Remove"
            };

            removeMenuItem.Click += (sender, e) =>
            {
                bool removedAlbumWasSelected = selectedAlbum == album;

                AppData.Library.Albums.Remove(album);
                AlbumsPanel.Children.Remove(albumBorder);

                LibraryStorage.SaveLibrary();

                AlbumRemoved?.Invoke(album);

                if (removedAlbumWasSelected)
                {
                    selectedAlbum = null;
                    SelectedAlbumChanged?.Invoke(null);
                }

                e.Handled = true;
            };

            contextMenu.Items.Add(removeMenuItem);

            albumBorder.ContextMenu = contextMenu;

            albumBorder.MouseLeftButtonDown += (sender, e) =>
            {
                selectedAlbum = album;
                SelectedAlbumChanged?.Invoke(album);
                e.Handled = true;
            };

            albumBorder.MouseDown += (sender, e) =>
            {
                if (e.ChangedButton != MouseButton.Middle)
                    return;

                selectedAlbum = album;
                SelectedAlbumChanged?.Invoke(album);

                Song? songToPlay;

                if (likedOnlyMode)
                {
                    songToPlay = album.Songs.FirstOrDefault(song => song.IsLiked);

                    if (songToPlay == null)
                    {
                        MessageBox.Show(
                            "No songs are liked, so liked song autoplay cannot play!",
                            "No Liked Songs",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning
                        );

                        e.Handled = true;
                        return;
                    }
                }
                else
                {
                    songToPlay = album.Songs.FirstOrDefault();
                }

                if (songToPlay != null)
                    AlbumMiddleClickedPlayRequested?.Invoke(album, songToPlay);

                e.Handled = true;
            };

            AlbumsPanel.Children.Add(albumBorder);
        }

        private void RefreshSavedAlbumFromFolder(Album savedAlbum)
        {
            if (string.IsNullOrWhiteSpace(savedAlbum.FolderPath))
                return;

            if (!Directory.Exists(savedAlbum.FolderPath))
                return;

            Album? refreshedAlbum = CreateAlbumFromFolder(savedAlbum.FolderPath);

            if (refreshedAlbum == null)
                return;

            savedAlbum.Title = refreshedAlbum.Title;
            savedAlbum.Artist = refreshedAlbum.Artist;
            savedAlbum.ReleaseYear = refreshedAlbum.ReleaseYear;
            savedAlbum.CoverPath = refreshedAlbum.CoverPath;
            savedAlbum.BackCoverPath = refreshedAlbum.BackCoverPath;
            savedAlbum.AlbumArtPaths = refreshedAlbum.AlbumArtPaths;

            foreach (Song refreshedSong in refreshedAlbum.Songs)
            {
                Song? existingSong = savedAlbum.Songs.FirstOrDefault(song =>
                    song.FilePath.Equals(refreshedSong.FilePath, StringComparison.OrdinalIgnoreCase));

                if (existingSong == null)
                {
                    savedAlbum.Songs.Add(refreshedSong);
                }
                else
                {
                    bool wasLiked = existingSong.IsLiked;

                    existingSong.Title = refreshedSong.Title;
                    existingSong.Artist = refreshedSong.Artist;
                    existingSong.ReleaseYear = refreshedSong.ReleaseYear;
                    existingSong.TrackNumber = refreshedSong.TrackNumber;
                    existingSong.Duration = refreshedSong.Duration;

                    existingSong.IsLiked = wasLiked;
                }
            }

            List<Song> sortedSongs = savedAlbum.Songs
            .Where(song => System.IO.File.Exists(song.FilePath))
            .OrderBy(song => song.TrackNumber == 0 ? uint.MaxValue : song.TrackNumber)
            .ThenBy(song => song.FilePath)
            .ToList();

            savedAlbum.Songs.Clear();

            foreach (Song song in sortedSongs)
            {
                savedAlbum.Songs.Add(song);
            }

        }

        private Song CreateSongFromFile(string songFile)
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

                title = CleanSongTitle(title, artist);

                return new Song
                {
                    Title = title,
                    Artist = artist,
                    ReleaseYear = tagFile.Tag.Year,
                    FilePath = songFile,
                    TrackNumber = tagFile.Tag.Track,
                    Duration = tagFile.Properties.Duration
                };
            }
            catch
            {
                return new Song
                {
                    Title = CleanSongTitle(fileNameTitle, ""),
                    Artist = "",
                    ReleaseYear = 0,
                    FilePath = songFile,
                    TrackNumber = 0,
                    Duration = TimeSpan.Zero
                };
            }
        }

        private string CleanSongTitle(string title, string artist)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string originalTitle = title.Trim();
            string cleanedTitle = originalTitle;

            // Removes leading track numbers like:
            // "1. Song"
            // "01 - Song"
            // "03_ Song"
            // "9) Song"
            // "1 Song"
            //
            // But if the whole title is just "4", keep it.
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"^\s*\d+\s*(?:[._)-]\s*|\s+)",
                ""
            );

            if (string.IsNullOrWhiteSpace(cleanedTitle))
                cleanedTitle = originalTitle;

            if (!string.IsNullOrWhiteSpace(artist))
            {
                string escapedArtist = Regex.Escape(artist.Trim());

                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—_]\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"\s*[-–—_]\s*" + escapedArtist + @"\s*$",
                    "",
                    RegexOptions.IgnoreCase
                );
            }

            if (string.IsNullOrWhiteSpace(cleanedTitle))
                cleanedTitle = originalTitle;

            return cleanedTitle.Trim();
        }
        public void LoadSavedAlbums()
        {
            AlbumsPanel.Children.Clear();

            foreach (Album album in AppData.Library.Albums)
            {
                RefreshSavedAlbumFromFolder(album);

                if (!System.IO.File.Exists(album.CoverPath))
                    continue;
            }

            RefreshAlbumOrder();

            LibraryStorage.SaveLibrary();
        }

        private void AlbumGridBackground_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            // Do not clear when clicking the Add button or context menu buttons.
            if (FindParent<Button>(source) != null)
                return;

            // Do not clear when clicking an actual album cover.
            Border? albumBorder = FindAlbumBorder(source);

            if (albumBorder != null)
                return;

            selectedAlbum = null;
            SelectedAlbumChanged?.Invoke(null);
        }

        private static Border? FindAlbumBorder(DependencyObject child)
        {
            DependencyObject? parent = child;

            while (parent != null)
            {
                if (parent is Border border && border.Tag is Album)
                    return border;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject? parent = VisualTreeHelper.GetParent(child);

            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;

                parent = VisualTreeHelper.GetParent(parent);
            }

            return null;
        }

        private string CleanAlbumTitle(string title, string artist, uint releaseYear)
        {
            if (string.IsNullOrWhiteSpace(title))
                return "";

            string originalTitle = title.Trim();
            string cleanedTitle = originalTitle;

            if (!string.IsNullOrWhiteSpace(artist))
            {
                string escapedArtist = Regex.Escape(artist.Trim());

                // Artist | Year | Album
                // Artist - Year - Album
                // Artist_Year_Album
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—|_]+\s*(19|20)\d{2}\s*[-–—|_]+\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                // Artist - Album
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"^\s*" + escapedArtist + @"\s*[-–—|_]+\s*",
                    "",
                    RegexOptions.IgnoreCase
                );

                // Album - Artist
                cleanedTitle = Regex.Replace(
                    cleanedTitle,
                    @"\s*[-–—|_]+\s*" + escapedArtist + @"\s*$",
                    "",
                    RegexOptions.IgnoreCase
                );
            }

            // Remove years in brackets/parentheses:
            // (2005) Album -> Album
            // Album (2005) -> Album
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"\s*[\[\(]\s*(19|20)\d{2}\s*[\]\)]\s*",
                " ",
                RegexOptions.IgnoreCase
            );

            // Remove year at beginning:
            // 2003 - Album
            // 2003 Album
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"^\s*(19|20)\d{2}\s*[-–—|_]*\s*",
                "",
                RegexOptions.IgnoreCase
            );

            // Remove year at end:
            // Album - 2003
            // Album 2003
            cleanedTitle = Regex.Replace(
                cleanedTitle,
                @"\s*[-–—|_]*\s*(19|20)\d{2}\s*$",
                "",
                RegexOptions.IgnoreCase
            );

            // Clean leftover separators at beginning/end.
            cleanedTitle = Regex.Replace(cleanedTitle, @"^\s*[-–—|_]+\s*", "");
            cleanedTitle = Regex.Replace(cleanedTitle, @"\s*[-–—|_]+\s*$", "");

            cleanedTitle = Regex.Replace(cleanedTitle, @"\s{2,}", " ").Trim();

            // Self-titled fallback:
            // If removing artist/year made the title empty, keep artist as album title.
            if (string.IsNullOrWhiteSpace(cleanedTitle) && !string.IsNullOrWhiteSpace(artist))
                cleanedTitle = artist.Trim();

            return cleanedTitle;
        }

    }
}
