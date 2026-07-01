using Microsoft.Win32;
using musicplayer.Models;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Controls.Primitives;
using Microsoft.VisualBasic;

using musicplayer.Services;

namespace musicplayer.View.UserControls
{
    public partial class AlbumGrid : UserControl
    {
        private Album? selectedAlbum;
        public event Action<Album>? AlbumRemoved;
        public event Action<Album?>? SelectedAlbumChanged;
        public event Action<bool>? LikedOnlyModeChanged;
        public event Action<Album, Song>? AlbumMiddleClickedPlayRequested;
        public bool KeepGridExpandedOnMiddleClick { get; set; } = false;
        public event Action<bool>? ShuffleModeChanged;

        private bool likedOnlyMode = false;
        private bool shuffleMode = false;

        public AlbumGrid()
        {
            InitializeComponent();
        }

        private void ShuffleButton_Click(object sender, RoutedEventArgs e)
        {
            shuffleMode = !shuffleMode;

            AppData.Library.ShuffleModeEnabled = shuffleMode;
            LibraryStorage.SaveLibrary();

            UpdateShuffleIcon();

            ShuffleModeChanged?.Invoke(shuffleMode);
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
                    Album? album = AlbumImportService.CreateAlbumFromFolder(folderPath);

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

            AppData.Library.LikedOnlyModeEnabled = likedOnlyMode;
            LibraryStorage.SaveLibrary();

            UpdateLikedOnlyIcon();

            LikedOnlyModeChanged?.Invoke(likedOnlyMode);
        }

        public void LoadSavedToggleModes()
        {
            likedOnlyMode = AppData.Library.LikedOnlyModeEnabled;
            shuffleMode = AppData.Library.ShuffleModeEnabled;

            UpdateLikedOnlyIcon();
            UpdateShuffleIcon();

            LikedOnlyModeChanged?.Invoke(likedOnlyMode);
            ShuffleModeChanged?.Invoke(shuffleMode);
        }

        private void UpdateLikedOnlyIcon()
        {
            LikedOnlyIcon.Source = new BitmapImage(
                new Uri(
                    likedOnlyMode
                        ? "/assets/icons/heart_filled.png"
                        : "/assets/icons/heart_hollow.png",
                    UriKind.Relative
                )
            );
        }

        private void UpdateShuffleIcon()
        {
            ShuffleIcon.Source = new BitmapImage(
                new Uri(
                    shuffleMode
                        ? "/assets/icons/shuffle.png"
                        : "/assets/icons/shuffle_unselected.png",
                    UriKind.Relative
                )
            );
        }

        public void RefreshAlbumOrder()
        {
            AlbumsPanel.Children.Clear();

            List<Album> sortedAlbums = AppData.Library.Albums
                .OrderByDescending(album => album.LastPlayedUtcTicks)
                .ThenByDescending(album => album.IsPlaylist)
                .ThenBy(album => album.IsPlaylist ? album.CreatedUtcTicks : 0)
                .ThenBy(album => album.Title)
                .ToList();

            foreach (Album album in sortedAlbums)
            {
                if (!album.IsPlaylist && !System.IO.File.Exists(album.CoverPath))
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

            UIElement albumVisual;

            if (!string.IsNullOrWhiteSpace(album.CoverPath) && System.IO.File.Exists(album.CoverPath))
            {
                BitmapImage coverImage = ImageService.LoadImage(album.CoverPath);

                Image albumImage = new Image
                {
                    Source = coverImage,
                    Stretch = Stretch.UniformToFill
                };

                RenderOptions.SetBitmapScalingMode(albumImage, BitmapScalingMode.HighQuality);
                RenderOptions.SetEdgeMode(albumImage, EdgeMode.Unspecified);

                albumVisual = albumImage;
            }
            else
            {
                albumVisual = new Grid
                {
                    Background = new SolidColorBrush(Color.FromRgb(24, 51, 51)),
                    Children =
                        {

                        new TextBlock
                        {
                            Text = album.IsPlaylist ? album.Title : "No Art",
                            Foreground = Brushes.White,
                            FontSize = 14,
                            FontWeight = FontWeights.Bold,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            TextAlignment = TextAlignment.Center,
                            TextWrapping = TextWrapping.Wrap,
                            Margin = new Thickness(6)
                        }
                    }
                };
            }

            Border imageBorder = new Border
            {
                CornerRadius = new CornerRadius(4),
                ClipToBounds = true,
                Child = albumVisual
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

            if (album.IsPlaylist)
            {
                MenuItem addArtMenuItem = new MenuItem
                {
                    Header = "Add album art"
                };

                addArtMenuItem.Click += (sender, e) =>
                {
                    OpenFileDialog openFileDialog = new OpenFileDialog();
                    openFileDialog.Title = "Select playlist artwork";
                    openFileDialog.Filter = "Image files (*.jpg;*.jpeg;*.png)|*.jpg;*.jpeg;*.png|All files (*.*)|*.*";

                    if (openFileDialog.ShowDialog() == true)
                    {
                        album.CoverPath = openFileDialog.FileName;
                        album.BackCoverPath = openFileDialog.FileName;
                        album.AlbumArtPaths = new List<string> { openFileDialog.FileName };

                        LibraryStorage.SaveLibrary();
                        RefreshAlbumOrder();

                        if (selectedAlbum == album)
                            SelectedAlbumChanged?.Invoke(album);
                    }

                    e.Handled = true;
                };

                contextMenu.Items.Add(addArtMenuItem);
            }

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

                if (!KeepGridExpandedOnMiddleClick)
                {
                    selectedAlbum = album;
                    SelectedAlbumChanged?.Invoke(album);
                }

                Song? songToPlay = GetMiddleClickSongToPlay(album);

                if (songToPlay != null)
                    AlbumMiddleClickedPlayRequested?.Invoke(album, songToPlay);

                e.Handled = true;
            };

            AlbumsPanel.Children.Add(albumBorder);
        }

        private Song? GetMiddleClickSongToPlay(Album album)
        {
            List<Song> playableSongs;

            if (likedOnlyMode)
            {
                playableSongs = album.Songs
                    .Where(song => song.IsLiked)
                    .ToList();

                if (playableSongs.Count == 0)
                    playableSongs = album.Songs.ToList();
            }
            else
            {
                playableSongs = album.Songs.ToList();
            }

            if (playableSongs.Count == 0)
                return null;

            if (!shuffleMode)
                return playableSongs[0];

            Random random = new Random();
            return playableSongs[random.Next(playableSongs.Count)];
        }

        private void RefreshSavedAlbumFromFolder(Album savedAlbum)
        {
            if (string.IsNullOrWhiteSpace(savedAlbum.FolderPath))
                return;

            if (!Directory.Exists(savedAlbum.FolderPath))
                return;

            Album? refreshedAlbum = AlbumImportService.CreateAlbumFromFolder(savedAlbum.FolderPath);

            if (refreshedAlbum == null)
                return;

            if (!savedAlbum.HasCustomTitle)
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
                    existingSong.AlbumArtPaths = refreshedSong.AlbumArtPaths;

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



        public void LoadSavedAlbums()
        {
            AlbumsPanel.Children.Clear();

            foreach (Album album in AppData.Library.Albums)
            {
                if (!album.IsPlaylist)
                {
                    RefreshSavedAlbumFromFolder(album);

                    if (!System.IO.File.Exists(album.CoverPath))
                        continue;
                }
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

            if (FindParent<ScrollBar>(source) != null)
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

        public void AddAlbumFromFolderPath(string folderPath)
        {
            if (!Directory.Exists(folderPath))
                return;

            Album? album = AlbumImportService.CreateAlbumFromFolder(folderPath);

            if (album == null)
                return;

            AppData.Library.Albums.Add(album);
            LibraryStorage.SaveLibrary();

            RefreshAlbumOrder();
        }

        public Song? CreateSongFromFilePath(string filePath)
        {
            return AlbumImportService.CreateSongFromFilePath(filePath);
        }

        public bool IsAudioFile(string filePath)
        {
            return MetadataCleanup.IsAudioFile(filePath);
        }

        private void AlbumsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (sender is not ScrollViewer scrollViewer)
                return;

            double scrollAmount = e.Delta > 0 ? -36 : 36;

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);

            e.Handled = true;
        }

        private void CreatePlaylistButton_Click(object sender, RoutedEventArgs e)
        {
            int playlistNumber = AppData.Library.Albums.Count(album => album.IsPlaylist) + 1;

            Album playlist = new Album
            {
                Title = "Playlist " + playlistNumber,
                Artist = "Playlist",
                ReleaseYear = 0,
                FolderPath = "",
                CoverPath = "",
                BackCoverPath = "",
                AlbumArtPaths = new List<string>(),
                IsPlaylist = true,
                CreatedUtcTicks = DateTime.UtcNow.Ticks
            };

            AppData.Library.Albums.Add(playlist);
            LibraryStorage.SaveLibrary();

            RefreshAlbumOrder();

            selectedAlbum = playlist;
            SelectedAlbumChanged?.Invoke(playlist);
        }


    }
}
