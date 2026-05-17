using musicplayer.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace musicplayer.View.UserControls
{
    public partial class AlbumCard : UserControl
    {
        private readonly DispatcherTimer systemDataTimer = new DispatcherTimer();

        private List<SongDisplayItem> currentSongItems = new List<SongDisplayItem>();

        private int mouseX = 0;
        private int mouseY = 0;

        public event Action<Song?>? SongSelected;

        public AlbumCard()
        {
            InitializeComponent();
            ClearAlbum();

            systemDataTimer.Interval = TimeSpan.FromMilliseconds(16);
            systemDataTimer.Tick += SystemDataTimer_Tick;
            systemDataTimer.Start();
        }

        public void DisplayAlbum(Album? album)
        {
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

            AlbumCover.Source = LoadImage(album.CoverPath);

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
                bool isPlaying = item.Song == song;

                item.IsPlaying = isPlaying;

                if (isPlaying)
                    playingItem = item;
            }

            Songs.SelectedItem = playingItem;
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

        private void Songs_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is not DependencyObject source)
                return;

            if (FindParent<Button>(source) != null)
                return;

            ListViewItem? clickedItem = FindParent<ListViewItem>(source);

            if (clickedItem == null)
                return;

            if (clickedItem.DataContext is SongDisplayItem item)
                SongSelected?.Invoke(item.Song);
        }

        private void SystemDataTimer_Tick(object? sender, EventArgs e)
        {
            if (GetCursorPos(out POINT cursorPoint))
            {
                mouseX = cursorPoint.X;
                mouseY = cursorPoint.Y;
            }

            long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string osDescription = RuntimeInformation.OSDescription;
            string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
            string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString();

            double memoryMb = Environment.WorkingSet / 1024.0 / 1024.0;

            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            SystemDataText.Text =
                $"OS:{osDescription}  ARCH:{osArchitecture}/{processArchitecture}  SCREEN:{screenWidth}x{screenHeight}\n" +
                $"UNIX:{unixSeconds}  MS:{unixMilliseconds}  LOCAL:{DateTime.Now:HH:mm:ss}  UTC:{DateTime.UtcNow:HH:mm:ss}\n" +
                $"MOUSE:{mouseX},{mouseY}  CPU:{Environment.ProcessorCount}  MEM:{memoryMb:0.0}MB  CLR:{Environment.Version}";
        }

        private BitmapImage LoadImage(string path)
        {
            BitmapImage image = new BitmapImage();

            image.BeginInit();
            image.UriSource = new Uri(path);
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.EndInit();
            image.Freeze();

            return image;
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

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll")]
        private static extern bool GetCursorPos(out POINT lpPoint);
    }
}