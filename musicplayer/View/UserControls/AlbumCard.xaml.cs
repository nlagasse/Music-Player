using musicplayer.Models;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using System.Windows.Interop;

using Forms = System.Windows.Forms;
using Drawing = System.Drawing;

using musicplayer.Services;

namespace musicplayer.View.UserControls
{
    public partial class AlbumCard : UserControl
    {
        private readonly OperatingSystemData operatingSystemData = new OperatingSystemData();

        private readonly DispatcherTimer systemDataTimer = new DispatcherTimer();

        private List<SongDisplayItem> currentSongItems = new List<SongDisplayItem>();

        public event Action<Song?>? SongSelected;

        public event Action<Song?>? SongRemoveRequested;

        private Window? albumArtPopupWindow;
        private Border? albumArtPopupBorder;
        private ScaleTransform? albumArtPopupScaleTransform;

        private double albumArtPopupBaseSize = 420;
        private double albumArtPopupScale = 1.0;

        private bool isDraggingAlbumArtPopup = false;
        private Point albumArtPopupDragStart;

        private Album? currentAlbum;
        private string albumTitleBeforeEdit = "";
        private bool isEditingAlbumTitle = false;

        public event Action? AlbumRenamed;

        private Album? albumBeingTitleEdited;

        public event Action<Album>? AlbumPlayRequested;

        public AlbumCard()
        {
            InitializeComponent();
            ClearAlbum();

            systemDataTimer.Interval = TimeSpan.FromMilliseconds(250);
            systemDataTimer.Tick += SystemDataTimer_Tick;
            systemDataTimer.Start();
        }

        private void AlbumInfoPlayButton_Click(object sender, RoutedEventArgs e)
        {
            if (isEditingAlbumTitle)
                CommitAlbumTitleEdit();

            if (currentAlbum == null)
                return;

            AlbumPlayRequested?.Invoke(currentAlbum);

            e.Handled = true;
        }

        private void AlbumInfoBorder_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!isEditingAlbumTitle)
                return;

            if (e.OriginalSource is DependencyObject source)
            {
                // Do not commit just because the user clicked inside the edit box.
                if (FindParent<TextBox>(source) != null)
                    return;
            }

            CommitAlbumTitleEdit();

            // Do not set e.Handled = true.
            // This lets other clicks still do their normal behavior.
        }

        private void AlbumCover_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (AlbumCover.Source == null)
                return;

            OpenAlbumArtPopup();

            e.Handled = true;
        }

        private void SongRow_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not FrameworkElement row)
                return;

            if (row.DataContext is not SongDisplayItem item)
                return;

            if (item.Song == null)
                return;

            ContextMenu contextMenu = BuildSongContextMenu(item.Song);

            row.ContextMenu = contextMenu;
            contextMenu.PlacementTarget = row;
            contextMenu.IsOpen = true;

            e.Handled = true;
        }

        private ContextMenu BuildSongContextMenu(Song song)
        {
            ContextMenu contextMenu = new ContextMenu();

            List<Album> playlists = AppData.Library.Albums
                .Where(album => album.IsPlaylist)
                .OrderBy(album => album.Title)
                .ToList();

            foreach (Album playlist in playlists)
            {
                MenuItem playlistItem = new MenuItem
                {
                    Header = playlist.Title
                };

                playlistItem.Click += (sender, e) =>
                {
                    AddSongToPlaylist(song, playlist);
                };

                contextMenu.Items.Add(playlistItem);
            }

            if (playlists.Count > 0)
                contextMenu.Items.Add(new Separator());

            MenuItem removeItem = new MenuItem
            {
                Header = "Remove"
            };

            removeItem.Click += (sender, e) =>
            {
                SongRemoveRequested?.Invoke(song);
            };

            contextMenu.Items.Add(removeItem);

            return contextMenu;
        }

        private Point ScreenPixelsToWpfUnits(Point screenPoint)
        {
            PresentationSource source = PresentationSource.FromVisual(this);

            if (source?.CompositionTarget == null)
                return screenPoint;

            return source.CompositionTarget.TransformFromDevice.Transform(screenPoint);
        }

        public void DisplayAlbum(Album? album)
        {
            if (isEditingAlbumTitle && albumBeingTitleEdited != null)
            {
                CommitAlbumTitleEdit();
            }

            currentAlbum = album;

            CloseAlbumArtPopup();

            if (album == null)
            {
                ClearAlbum();
                return;
            }

            AlbumCoverBorder.Visibility = Visibility.Visible;
            Grid.SetColumnSpan(AlbumInfoBorder, 1);

            AlbumTitle.Text = album.Title;
            ArtistName.Text = album.Artist;
            ReleaseDate.Text = album.ReleaseYear > 0 ? album.ReleaseYear.ToString() : "";
            NumSongs.Text = album.SongCount + " songs";
            RunTime.Text = FormatAlbumDuration(album.Songs.Sum(song => song.Duration.TotalSeconds));

            AlbumCover.Source = ImageService.LoadImage(album.CoverPath);
            AlbumCoverBorder.Visibility = AlbumCover.Source == null ? Visibility.Collapsed : Visibility.Visible;

            currentSongItems = album.Songs
                .Select((song, index) => new SongDisplayItem
                {
                    TrackNumber = index + 1,
                    Title = song.Title,
                    Duration = song.DisplayDuration,
                    Song = song,
                    IsLiked = song.IsLiked
                })
                .ToList();

            Songs.ItemsSource = null;
            Songs.ItemsSource = currentSongItems;

            Dispatcher.BeginInvoke(new Action(() =>
            {
                ScrollViewer? scrollViewer = FindChild<ScrollViewer>(Songs);

                if (scrollViewer != null)
                    scrollViewer.ScrollToTop();
            }), DispatcherPriority.Loaded);
        }

        private static T? FindChild<T>(DependencyObject parent) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(parent, i);

                if (child is T typedChild)
                    return typedChild;

                T? childOfChild = FindChild<T>(child);

                if (childOfChild != null)
                    return childOfChild;
            }

            return null;
        }

        public void SetPlayingSong(Song? song)
        {
            SongDisplayItem? playingItem = null;

            foreach (SongDisplayItem item in currentSongItems)
            {
                if (item.Song == null)
                    continue;

                bool isSameSong =
                    song != null &&
                    item.Song.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase);

                item.IsPlaying = isSameSong;

                // This keeps the album-card heart icon synced with the actual song model.
                item.IsLiked = item.Song.IsLiked;

                if (isSameSong)
                    playingItem = item;
            }

            Songs.SelectedItem = playingItem;
        }

        private void RemoveSong_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not MenuItem menuItem)
                return;

            if (menuItem.DataContext is not SongDisplayItem item)
                return;

            SongRemoveRequested?.Invoke(item.Song);
        }

        private void ClearAlbum()
        {
            AlbumTitle.Text = "";
            ArtistName.Text = "";
            ReleaseDate.Text = "";
            NumSongs.Text = "";
            RunTime.Text = "";

            AlbumCover.Source = null;
            AlbumCoverBorder.Visibility = Visibility.Collapsed;

            Grid.SetColumnSpan(AlbumInfoBorder, 2);

            currentSongItems.Clear();
            Songs.ItemsSource = null;
        }

        private void SongRow_ContextMenuOpening(object sender, ContextMenuEventArgs e)
        {
            if (sender is not FrameworkElement row)
                return;

            if (row.DataContext is not SongDisplayItem item)
                return;

            if (item.Song == null)
                return;

            ContextMenu contextMenu = new ContextMenu();

            List<Album> playlists = AppData.Library.Albums
                .Where(album => album.IsPlaylist)
                .OrderBy(album => album.Title)
                .ToList();

            foreach (Album playlist in playlists)
            {
                MenuItem playlistItem = new MenuItem
                {
                    Header = playlist.Title
                };

                playlistItem.Click += (sender, e) =>
                {
                    AddSongToPlaylist(item.Song, playlist);
                };

                contextMenu.Items.Add(playlistItem);
            }

            if (playlists.Count > 0)
                contextMenu.Items.Add(new Separator());

            MenuItem removeItem = new MenuItem
            {
                Header = "Remove"
            };

            removeItem.Click += RemoveSong_Click;

            contextMenu.Items.Add(removeItem);

            row.ContextMenu = contextMenu;
        }

        private void AddSongToPlaylist(Song sourceSong, Album playlist)
        {
            bool alreadyExists = playlist.Songs.Any(song =>
                song.FilePath.Equals(sourceSong.FilePath, StringComparison.OrdinalIgnoreCase));

            if (alreadyExists)
                return;

            Song playlistSong = new Song
            {
                Title = sourceSong.Title,
                Artist = sourceSong.Artist,
                ReleaseYear = sourceSong.ReleaseYear,
                FilePath = sourceSong.FilePath,
                TrackNumber = (uint)(playlist.Songs.Count + 1),
                Duration = sourceSong.Duration,
                IsLiked = sourceSong.IsLiked
            };

            playlist.Songs.Add(playlistSong);

            LibraryStorage.SaveLibrary();
        }

        private void LikeButton_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button)
                return;

            if (button.DataContext is not SongDisplayItem item)
                return;

            if (item.Song == null)
                return;

            item.Song.IsLiked = !item.Song.IsLiked;
            item.IsLiked = item.Song.IsLiked;

            LibraryStorage.SaveLibrary();

            e.Handled = true;
        }

        private void SongRow_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is DependencyObject source)
            {
                if (FindParent<Button>(source) != null)
                    return;
            }

            if (sender is not FrameworkElement row)
                return;

            if (row.DataContext is not SongDisplayItem item)
                return;

            SongSelected?.Invoke(item.Song);

            e.Handled = true;
        }

        private void SongRow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            if (sender is not FrameworkElement row)
                return;

            if (row.DataContext is not SongDisplayItem item)
                return;

            if (item.Song == null)
                return;

            item.Song.IsLiked = !item.Song.IsLiked;
            item.IsLiked = item.Song.IsLiked;

            LibraryStorage.SaveLibrary();

            e.Handled = true;
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

        private string FormatAlbumDuration(double totalSeconds)
        {
            TimeSpan time = TimeSpan.FromSeconds(totalSeconds);

            if (time.TotalHours >= 1)
                return $"{(int)time.TotalHours}h{time.Minutes}m";

            return $"{time.Minutes}m";
        }

        private class SongDisplayItem : INotifyPropertyChanged
        {
            private bool isLiked;
            private bool isPlaying;
            private string likeIconPath = "";

            public int TrackNumber { get; set; }
            public string Title { get; set; } = "";
            public string Duration { get; set; } = "";
            public Song? Song { get; set; }

            public bool IsPlaying
            {
                get
                {
                    return isPlaying;
                }
                set
                {
                    isPlaying = value;
                    OnPropertyChanged(nameof(IsPlaying));
                }
            }

            public bool IsLiked
            {
                get
                {
                    return isLiked;
                }
                set
                {
                    isLiked = value;

                    LikeIconPath = isLiked
                        ? "/assets/icons/heart_filled.png"
                        : "/assets/icons/heart_hollow.png";

                    OnPropertyChanged(nameof(IsLiked));
                }
            }

            public string LikeIconPath
            {
                get
                {
                    return likeIconPath;
                }
                set
                {
                    likeIconPath = value;
                    OnPropertyChanged(nameof(LikeIconPath));
                }
            }

            public event PropertyChangedEventHandler? PropertyChanged;

            private void OnPropertyChanged(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        private void OpenAlbumArtPopup()
        {
            if (albumArtPopupWindow != null)
            {
                albumArtPopupWindow.Activate();
                return;
            }

            Window? ownerWindow = Window.GetWindow(this);

            if (ownerWindow == null)
                return;

            albumArtPopupScale = 1.0;

            // Make the album title card fill the space where the art normally sits.
            AlbumCoverBorder.Visibility = Visibility.Collapsed;
            Grid.SetColumnSpan(AlbumInfoBorder, 2);

            albumArtPopupScaleTransform = new ScaleTransform(1.0, 1.0);

            ImageSource? popupSource = AlbumCover.Source;

            double popupWidth = albumArtPopupBaseSize;
            double popupHeight = albumArtPopupBaseSize;

            if (popupSource is BitmapSource bitmapSource &&
                bitmapSource.PixelWidth > 0 &&
                bitmapSource.PixelHeight > 0)
            {
                double aspectRatio = (double)bitmapSource.PixelWidth / bitmapSource.PixelHeight;

                if (aspectRatio >= 1)
                {
                    popupWidth = albumArtPopupBaseSize;
                    popupHeight = albumArtPopupBaseSize / aspectRatio;
                }
                else
                {
                    popupHeight = albumArtPopupBaseSize;
                    popupWidth = albumArtPopupBaseSize * aspectRatio;
                }
            }

            Image popupImage = new Image
            {
                Source = popupSource,
                Stretch = Stretch.Fill,
                Width = popupWidth,
                Height = popupHeight
            };

            RenderOptions.SetBitmapScalingMode(popupImage, BitmapScalingMode.HighQuality);

            albumArtPopupBorder = new Border
            {
                Width = popupWidth,
                Height = popupHeight,
                Background = Brushes.Transparent,
                Child = popupImage,
                Cursor = Cursors.SizeAll,
                RenderTransform = albumArtPopupScaleTransform,
                RenderTransformOrigin = new Point(0.5, 0.5)
            };

            Canvas overlayCanvas = new Canvas
            {
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0))
            };

            overlayCanvas.Children.Add(albumArtPopupBorder);

            Point coverScreenPositionPixels = AlbumCover.PointToScreen(new Point(0, 0));

            Forms.Screen screen = Forms.Screen.FromPoint(
                new Drawing.Point(
                    (int)coverScreenPositionPixels.X,
                    (int)coverScreenPositionPixels.Y
                )
            );

            Drawing.Rectangle workingArea = screen.WorkingArea;

            albumArtPopupWindow = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = new SolidColorBrush(Color.FromArgb(1, 0, 0, 0)),
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = true,
                Content = overlayCanvas,
                WindowStartupLocation = WindowStartupLocation.Manual,
                Owner = ownerWindow,

                Left = workingArea.Left,
                Top = workingArea.Top,
                Width = workingArea.Width,
                Height = workingArea.Height
            };

            double popupLeft = coverScreenPositionPixels.X - workingArea.Left;
            double popupTop = coverScreenPositionPixels.Y - workingArea.Top;

            popupLeft -= 80;

            if (popupLeft < 20)
                popupLeft = 20;

            if (popupTop < 20)
                popupTop = 20;

            Canvas.SetLeft(albumArtPopupBorder, popupLeft);
            Canvas.SetTop(albumArtPopupBorder, popupTop);

            // Click outside the album art:
            // close popup, keep focus in the music player, and eat the click.
            overlayCanvas.PreviewMouseDown += (sender, e) =>
            {
                if (e.OriginalSource == overlayCanvas)
                {
                    CloseAlbumArtPopup();

                    ownerWindow.Activate();
                    ownerWindow.Focus();

                    e.Handled = true;
                }
            };

            albumArtPopupBorder.PreviewMouseLeftButtonDown += (sender, e) =>
            {
                isDraggingAlbumArtPopup = true;
                albumArtPopupDragStart = e.GetPosition(overlayCanvas);

                albumArtPopupBorder.CaptureMouse();

                e.Handled = true;
            };

            albumArtPopupBorder.PreviewMouseMove += (sender, e) =>
            {
                if (!isDraggingAlbumArtPopup || albumArtPopupBorder == null)
                    return;

                Point currentPosition = e.GetPosition(overlayCanvas);

                double left = Canvas.GetLeft(albumArtPopupBorder);
                double top = Canvas.GetTop(albumArtPopupBorder);

                double deltaX = currentPosition.X - albumArtPopupDragStart.X;
                double deltaY = currentPosition.Y - albumArtPopupDragStart.Y;

                Canvas.SetLeft(albumArtPopupBorder, left + deltaX);
                Canvas.SetTop(albumArtPopupBorder, top + deltaY);

                albumArtPopupDragStart = currentPosition;

                e.Handled = true;
            };

            albumArtPopupBorder.PreviewMouseLeftButtonUp += (sender, e) =>
            {
                isDraggingAlbumArtPopup = false;

                if (albumArtPopupBorder != null)
                    albumArtPopupBorder.ReleaseMouseCapture();

                e.Handled = true;
            };

            albumArtPopupBorder.PreviewMouseWheel += (sender, e) =>
            {
                ZoomAlbumArtPopup(e.Delta > 0 ? 0.18 : -0.18);
                e.Handled = true;
            };

            albumArtPopupWindow.Closed += (sender, e) =>
            {
                albumArtPopupWindow = null;
                albumArtPopupBorder = null;
                albumArtPopupScaleTransform = null;
                isDraggingAlbumArtPopup = false;

                AlbumCoverBorder.Visibility = Visibility.Visible;
                Grid.SetColumnSpan(AlbumInfoBorder, 1);
            };

            albumArtPopupWindow.Show();
            albumArtPopupWindow.Activate();
        }

        private void ZoomAlbumArtPopup(double amount)
        {
            if (albumArtPopupScaleTransform == null)
                return;

            albumArtPopupScale += amount;
            albumArtPopupScale = Math.Max(0.35, Math.Min(5.0, albumArtPopupScale));

            albumArtPopupScaleTransform.ScaleX = albumArtPopupScale;
            albumArtPopupScaleTransform.ScaleY = albumArtPopupScale;
        }

        private void CloseAlbumArtPopup()
        {
            if (albumArtPopupWindow == null)
                return;

            Window popupToClose = albumArtPopupWindow;
            albumArtPopupWindow = null;

            popupToClose.Close();
        }

        private void SystemDataTimer_Tick(object? sender, EventArgs e)
        {
            SystemDataText.Text = operatingSystemData.BuildText();
        }

        public void SetAudioDebugInfo(int sampleRate, int channels, int? bitrateKbps, int? latencyMs)
        {
            operatingSystemData.SetAudioDebugInfo(sampleRate, channels, bitrateKbps, latencyMs);
        }

        private void Songs_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            ScrollViewer? scrollViewer = FindChild<ScrollViewer>(Songs);

            if (scrollViewer == null)
                return;

            double scrollAmount = e.Delta > 0 ? -28 : 28;

            scrollViewer.ScrollToVerticalOffset(scrollViewer.VerticalOffset + scrollAmount);

            e.Handled = true;
        }

        private void AlbumTitle_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            BeginAlbumTitleEdit();
            e.Handled = true;
        }

        private void BeginAlbumTitleEdit()
        {
            if (currentAlbum == null)
                return;

            albumBeingTitleEdited = currentAlbum;
            isEditingAlbumTitle = true;

            AlbumTitleEdit.Text = currentAlbum.Title;

            AlbumTitle.Visibility = Visibility.Collapsed;
            AlbumTitleEdit.Visibility = Visibility.Visible;

            AlbumTitleEdit.Focus();
            AlbumTitleEdit.SelectAll();
        }

        private void AlbumTitleEdit_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                CommitAlbumTitleEdit();
                e.Handled = true;
                return;
            }

            if (e.Key == Key.Escape)
            {
                CancelAlbumTitleEdit();
                e.Handled = true;
            }
        }

        private void AlbumTitleEdit_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitAlbumTitleEdit();
        }

        private void CommitAlbumTitleEdit()
        {
            if (!isEditingAlbumTitle)
                return;

            Album? albumToEdit = albumBeingTitleEdited;

            if (albumToEdit == null)
            {
                EndAlbumTitleEdit();
                return;
            }

            string newTitle = AlbumTitleEdit.Text.Trim();

            if (!string.IsNullOrWhiteSpace(newTitle) && albumToEdit.Title != newTitle)
            {
                albumToEdit.Title = newTitle;
                albumToEdit.HasCustomTitle = true;

                LibraryStorage.SaveLibrary();

                AlbumRenamed?.Invoke();
            }

            EndAlbumTitleEdit();

            if (currentAlbum == albumToEdit)
                DisplayAlbum(albumToEdit);
        }

        private void CancelAlbumTitleEdit()
        {
            EndAlbumTitleEdit();

            if (currentAlbum != null)
                DisplayAlbum(currentAlbum);
        }

        private void EndAlbumTitleEdit()
        {
            isEditingAlbumTitle = false;
            albumBeingTitleEdited = null;

            AlbumTitleEdit.Visibility = Visibility.Collapsed;
            AlbumTitle.Visibility = Visibility.Visible;
        }

    }
}