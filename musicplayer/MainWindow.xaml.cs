using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Interop;
using musicplayer.Models;
using System.Runtime.InteropServices;

namespace musicplayer
{
    public partial class MainWindow : Window
    {

        private musicplayer.Models.Song? currentPlayingSong;
        private musicplayer.Models.Album? selectedAlbum;
        public MainWindow()
        {
            InitializeComponent();
            SourceInitialized += MainWindow_SourceInitialized;
            LibraryStorage.LoadLibrary();
            AlbumGridControl.LoadSavedAlbums();
            StateChanged += MainWindow_StateChanged;
            UpdateWindowChromeForState();
            AlbumGridControl.SelectedAlbumChanged += AlbumGridControl_SelectedAlbumChanged;
            AlbumGridControl.AlbumRemoved += AlbumGridControl_AlbumRemoved;
            AlbumCardControl.SongSelected += AlbumCardControl_SongSelected;
            PlayerBarControl.CurrentSongChanged += PlayerBarControl_CurrentSongChanged;
            PlayerBarControl.PlaybackStateChanged += PlayerBarControl_PlaybackStateChanged;
            AlbumGridControl.LikedOnlyModeChanged += AlbumGridControl_LikedOnlyModeChanged;
            AlbumGridControl.AlbumMiddleClickedPlayRequested += AlbumGridControl_AlbumMiddleClickedPlayRequested;
            PlayerBarControl.BackCoverMiddleClicked += PlayerBarControl_BackCoverMiddleClicked;
            PlayerBarControl.AlbumPlaybackStarted += PlayerBarControl_AlbumPlaybackStarted;

        }

        private void PlayerBarControl_BackCoverMiddleClicked(musicplayer.Models.Album? album)
        {
            if (album == null)
                return;

            if (selectedAlbum == album)
                return;

            selectedAlbum = album;

            AlbumCardControl.DisplayAlbum(album);
            AlbumCardControl.SetPlayingSong(currentPlayingSong);

            PlayerBarControl.DisplayAlbum(album);
        }

        private void AlbumGridControl_AlbumMiddleClickedPlayRequested(
            musicplayer.Models.Album album,
            musicplayer.Models.Song song)
        {
            selectedAlbum = album;

            AlbumCardControl.DisplayAlbum(album);
            AlbumCardControl.SetPlayingSong(song);

            PlayerBarControl.DisplayAlbum(album);
            PlayerBarControl.LoadSong(album, song);
        }

        private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
        {
            if (e.Key != System.Windows.Input.Key.Space)
                return;

            PlayerBarControl.TogglePlayPause();

            e.Handled = true;
        }

        private void PlayerBarControl_CurrentSongChanged(musicplayer.Models.Song? song)
        {
            currentPlayingSong = song;
            AlbumCardControl.SetPlayingSong(song);
        }

        private void AlbumGridControl_LikedOnlyModeChanged(bool enabled)
        {
            PlayerBarControl.SetLikedOnlyMode(enabled);
        }

        private void PlayerBarControl_AlbumPlaybackStarted(musicplayer.Models.Album album)
        {
            AlbumGridControl.RefreshAlbumOrder();
        }

        private void MainWindow_StateChanged(object? sender, EventArgs e)
        {
            UpdateWindowChromeForState();
        }
        private void UpdateWindowChromeForState()
        {
            if (WindowState == WindowState.Maximized)
            {
                // When maximized, remove resize border so corners/edges do not behave resizable.
                // This also helps the top-right corner click the close button like normal apps.
                MainWindowChrome.ResizeBorderThickness = new Thickness(0);
            }
            else
            {
                // When restored, bring resize handles back.
                MainWindowChrome.ResizeBorderThickness = new Thickness(5);
            }
        }

        private void PlayerBarControl_PlaybackStateChanged(bool isPlaying)
        {
            if (isPlaying)
            {
                TaskbarPlayPauseButton.ImageSource =
                    new BitmapImage(new Uri("pack://application:,,,/assets/icons/pause-256.png"));

                TaskbarPlayPauseButton.Description = "Pause";
            }
            else
            {
                TaskbarPlayPauseButton.ImageSource =
                    new BitmapImage(new Uri("pack://application:,,,/assets/icons/play-256.png"));

                TaskbarPlayPauseButton.Description = "Play";
            }
        }

        private void AlbumGridControl_AlbumRemoved(musicplayer.Models.Album removedAlbum)
        {
            if (selectedAlbum == removedAlbum)
            {
                selectedAlbum = null;
                AlbumCardControl.DisplayAlbum(null);
                PlayerBarControl.DisplayAlbum(null);
            }

            PlayerBarControl.ClearAlbumIfRemoved(removedAlbum);
        }

        private void btnErrorCheck_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show("Error check", "Error Box!", MessageBoxButton.OK, MessageBoxImage.Warning);
            MessageBoxResult result = MessageBox.Show("Do you agree?", "Agreement!",
                                                    MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result == MessageBoxResult.Yes)
            {
                MessageBox.Show("You agreed!", "Agreement Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                MessageBox.Show("You disagreed!", "Agreement Result", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }


        private void Grid_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void AlbumCardControl_SongSelected(musicplayer.Models.Song? song)
        {
            if (selectedAlbum == null || song == null)
                return;

            PlayerBarControl.LoadSong(selectedAlbum, song);
        }

        private void AlbumGridControl_SelectedAlbumChanged(musicplayer.Models.Album? album)
        {
            selectedAlbum = album;

            AlbumCardControl.DisplayAlbum(album);
            AlbumCardControl.SetPlayingSong(currentPlayingSong);

            PlayerBarControl.DisplayAlbum(album);
        }

        private void TaskbarPrev_Click(object sender, EventArgs e)
        {
            PlayerBarControl.PreviousSong();
        }

        private void TaskbarPlayPause_Click(object sender, EventArgs e)
        {
            PlayerBarControl.TogglePlayPause();
        }

        private void TaskbarNext_Click(object sender, EventArgs e)
        {
            PlayerBarControl.NextSong();
        }

        private void MainWindow_SourceInitialized(object? sender, EventArgs e)
        {
            IntPtr handle = new WindowInteropHelper(this).Handle;
            HwndSource source = HwndSource.FromHwnd(handle);

            source.AddHook(WindowProc);
        }

        private IntPtr WindowProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            const int WM_GETMINMAXINFO = 0x0024;

            if (msg == WM_GETMINMAXINFO)
            {
                WmGetMinMaxInfo(hwnd, lParam);
                handled = true;
            }

            return IntPtr.Zero;
        }

        private void WmGetMinMaxInfo(IntPtr hwnd, IntPtr lParam)
        {
            MINMAXINFO mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);

            IntPtr monitor = MonitorFromWindow(hwnd, MONITOR_DEFAULTTONEAREST);

            if (monitor != IntPtr.Zero)
            {
                MONITORINFO monitorInfo = new MONITORINFO();
                monitorInfo.cbSize = Marshal.SizeOf(typeof(MONITORINFO));

                GetMonitorInfo(monitor, ref monitorInfo);

                RECT workArea = monitorInfo.rcWork;
                RECT monitorArea = monitorInfo.rcMonitor;

                mmi.ptMaxPosition.x = Math.Abs(workArea.Left - monitorArea.Left);
                mmi.ptMaxPosition.y = Math.Abs(workArea.Top - monitorArea.Top);

                mmi.ptMaxSize.x = Math.Abs(workArea.Right - workArea.Left);
                mmi.ptMaxSize.y = Math.Abs(workArea.Bottom - workArea.Top);
            }

            Marshal.StructureToPtr(mmi, lParam, true);
        }

        private const int MONITOR_DEFAULTTONEAREST = 0x00000002;

        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int dwFlags);

        [DllImport("user32.dll")]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);

        [StructLayout(LayoutKind.Sequential)]
        private struct POINT
        {
            public int x;
            public int y;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public POINT ptReserved;
            public POINT ptMaxSize;
            public POINT ptMaxPosition;
            public POINT ptMinTrackSize;
            public POINT ptMaxTrackSize;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public int dwFlags;
        }

    }
}