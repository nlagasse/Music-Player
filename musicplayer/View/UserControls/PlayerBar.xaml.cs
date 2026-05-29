using NAudio.Wave;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

using musicplayer.Models;
using musicplayer.Services;

namespace musicplayer.View.UserControls
{
    public partial class PlayerBar : UserControl
    {
        private readonly AudioPlayerService audioPlayer = new AudioPlayerService();
        private readonly DispatcherTimer progressTimer = new DispatcherTimer();

        private readonly List<float> waveformPoints = new List<float>();
        private const int WaveformPointCount = 140;

        private Album? currentAlbum;
        private Song? currentSong;
        private int currentSongIndex = -1;

        private bool isSeekingWithMouse = false;
        private bool isChangingVolumeWithMouse = false;

        public event Action<bool>? PlaybackStateChanged;
        public event Action<Song?>? CurrentSongChanged;
        public event Action? EmptyPlayRequested;

        private bool likedOnlyMode = false;
        private bool shuffleMode = false;
        private readonly Random random = new();

        public event Action<Album?>? BackCoverMiddleClicked;

        public event Action<Album>? AlbumPlaybackStarted;

        public event Action<int, int, int?, int?>? AudioDebugInfoChanged;

        public event Action<ImageSource?>? PlayerArtChanged;

        private readonly Stack<PlaybackHistoryItem> playbackHistory = new Stack<PlaybackHistoryItem>();

        public PlayerBar()
        {
            InitializeComponent();

            progressTimer.Interval = TimeSpan.FromMilliseconds(50);
            progressTimer.Tick += ProgressTimer_Tick;

            audioPlayer.PlaybackStopped += AudioPlayer_PlaybackStopped;

            ProgressArea.SizeChanged += (sender, e) =>
            {
                DrawWaveformMountain();
            };
        }

        private class PlaybackHistoryItem
        {
            public Album Album { get; set; } = null!;
            public Song Song { get; set; } = null!;
        }

        public void ShowCurrentAlbumFromPlayerArt()
        {
            if (currentAlbum == null)
                return;

            BackCoverMiddleClicked?.Invoke(currentAlbum);
        }

        public void CycleCurrentAlbumArtFromPlayerArt()
        {
            CycleCurrentAlbumArt();
        }

        public void SetShuffleMode(bool enabled)
        {
            shuffleMode = enabled;
        }
        public void ShuffleCurrentAlbum()
        {
            if (currentAlbum == null)
                return;

            List<Song> playableSongs = GetPlayableSongsForCurrentAlbum();

            if (playableSongs.Count == 0)
                return;

            Song songToPlay;

            if (playableSongs.Count == 1)
            {
                songToPlay = playableSongs[0];
            }
            else
            {
                do
                {
                    songToPlay = playableSongs[random.Next(playableSongs.Count)];
                }
                while (songToPlay == currentSong);
            }

            LoadSong(currentAlbum, songToPlay);
        }

        public void DisplayAlbum(Album? album)
        {
            if (album == null)
            {
                if (currentSong == null)
                    BackCoverImage.Source = null;

                return;
            }

            if (currentSong != null)
                return;

            SetBackCoverImage(album);
        }

        private void SetBackCoverImage(Album album)
        {
            string imagePath = GetCurrentPlayerArtPath(album);

            ImageSource? imageSource = ImageService.LoadImage(imagePath);

            BackCoverImage.Source = imageSource;
            PlayerArtChanged?.Invoke(imageSource);

            BackCoverImage.ToolTip = new ToolTip
            {
                Content = album.Title,
                Background = new SolidColorBrush(Color.FromRgb(32, 33, 36)),
                Foreground = Brushes.White,
                BorderBrush = Brushes.Transparent,
                Padding = new Thickness(8, 4, 8, 4),
                FontSize = 12
            };

            ToolTipService.SetInitialShowDelay(BackCoverImage, 250);
            ToolTipService.SetShowDuration(BackCoverImage, 5000);
            ToolTipService.SetBetweenShowDelay(BackCoverImage, 100);
        }

        

        private void BackCoverImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (currentAlbum == null)
                return;

            BackCoverMiddleClicked?.Invoke(currentAlbum);

            e.Handled = true;
        }

        private void BackCoverImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            if (currentAlbum == null)
                return;

            CycleCurrentAlbumArt();

            e.Handled = true;
        }

        private void CycleCurrentAlbumArt()
        {
            if (currentAlbum == null)
                return;

            if (currentAlbum.AlbumArtPaths == null || currentAlbum.AlbumArtPaths.Count == 0)
                return;

            currentAlbum.PlayerArtIndex++;

            if (currentAlbum.PlayerArtIndex >= currentAlbum.AlbumArtPaths.Count)
                currentAlbum.PlayerArtIndex = 0;

            SetBackCoverImage(currentAlbum);

            LibraryStorage.SaveLibrary();
        }

        public void LoadSong(Album album, Song song, bool addToHistory = true)
        {
            if (addToHistory &&
                currentAlbum != null &&
                currentSong != null &&
                !currentSong.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase))
            {
                playbackHistory.Push(new PlaybackHistoryItem
                {
                    Album = currentAlbum,
                    Song = currentSong
                });
            }

            currentAlbum = album;
            currentSong = song;
            currentSongIndex = album.Songs.IndexOf(song);

            album.LastPlayedUtcTicks = DateTime.UtcNow.Ticks;
            LibraryStorage.SaveLibrary();
            AlbumPlaybackStarted?.Invoke(album);

            CurrentSongChanged?.Invoke(song);

            SetBackCoverImage(album);

            audioPlayer.Load(song.FilePath);
            audioPlayer.Volume = (float)VolumeSlider.Value;

            AudioDebugInfoChanged?.Invoke(
            GetAudioSampleRate(song.FilePath),
            GetAudioChannels(song.FilePath),
            GetAudioBitrate(song.FilePath),
            300);

            CurrentSongTitle.Text = song.Title;

            ProgressSlider.Maximum = Math.Max(1, audioPlayer.TotalTime.TotalSeconds);
            ProgressSlider.Value = 0;

            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = FormatTime(audioPlayer.TotalTime);

            waveformPoints.Clear();
            AmplitudeMountain.Points.Clear();

            audioPlayer.Play();
            progressTimer.Start();

            SetPlayIconToPause();
            PlaybackStateChanged?.Invoke(true);

            BuildWaveformInBackground(song);
        }

        private async void BuildWaveformInBackground(Song song)
        {
            List<float> builtWaveform = await WaveformService.BuildWaveformFromFileAsync(song.FilePath, WaveformPointCount);

            if (currentSong != song)
                return;

            waveformPoints.Clear();
            waveformPoints.AddRange(builtWaveform);
            DrawWaveformMountain();
        }

        private int? GetAudioBitrate(string filePath)
        {
            try
            {
                using TagLib.File tagFile = TagLib.File.Create(filePath);
                return tagFile.Properties.AudioBitrate;
            }
            catch
            {
                return null;
            }
        }

        private int GetAudioSampleRate(string filePath)
        {
            try
            {
                using TagLib.File tagFile = TagLib.File.Create(filePath);
                return tagFile.Properties.AudioSampleRate;
            }
            catch
            {
                return 0;
            }
        }

        private int GetAudioChannels(string filePath)
        {
            try
            {
                using TagLib.File tagFile = TagLib.File.Create(filePath);
                return tagFile.Properties.AudioChannels;
            }
            catch
            {
                return 0;
            }
        }

        private void PlayerBar_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            double volumeStep = 0.05;

            if (e.Delta > 0)
                VolumeSlider.Value = Math.Min(1.0, VolumeSlider.Value + volumeStep);
            else
                VolumeSlider.Value = Math.Max(0.0, VolumeSlider.Value - volumeStep);

            audioPlayer.Volume = (float)VolumeSlider.Value;

            e.Handled = true;
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            if (currentSong == null)
            {
                EmptyPlayRequested?.Invoke();
                return;
            }

            if (audioPlayer.IsPlaying)
            {
                audioPlayer.Pause();
                progressTimer.Stop();
                SetPlayIconToPlay();

                PlaybackStateChanged?.Invoke(false);
            }
            else
            {
                audioPlayer.Play();
                progressTimer.Start();
                SetPlayIconToPause();

                PlaybackStateChanged?.Invoke(true);
            }
        }

        private void Prev_Click(object sender, RoutedEventArgs e)
        {
            PreviousSong();
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            PlayNextSong();
        }

        private void PlayNextSong()
        {
            NextSong();
        }

        public void SetLikedOnlyMode(bool enabled)
        {
            likedOnlyMode = enabled;
        }

        

        private void ProgressArea_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isSeekingWithMouse = true;
            ProgressArea.CaptureMouse();

            SetProgressFromMouse(e.GetPosition(ProgressArea).X);

            e.Handled = true;
        }

        private void ProgressArea_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isSeekingWithMouse)
                return;

            SetProgressFromMouse(e.GetPosition(ProgressArea).X);
        }

        private void ProgressArea_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isSeekingWithMouse)
                return;

            isSeekingWithMouse = false;
            ProgressArea.ReleaseMouseCapture();

            e.Handled = true;
        }

        private void SetProgressFromMouse(double mouseX)
        {
            if (audioPlayer.TotalTime.TotalSeconds <= 0)
                return;

            // This must match the left/right margin inside GreenHorizontalSliderStyle.
            double trackHorizontalMargin = 8;

            Point mousePointInSlider = ProgressArea.TranslatePoint(
                new Point(mouseX, 0),
                ProgressSlider
            );

            double trackWidth = ProgressSlider.ActualWidth - trackHorizontalMargin * 2;

            if (trackWidth <= 0)
                return;

            double trackX = mousePointInSlider.X - trackHorizontalMargin;

            double percent = trackX / trackWidth;
            percent = Math.Max(0, Math.Min(1, percent));

            TimeSpan targetTime = TimeSpan.FromSeconds(audioPlayer.TotalTime.TotalSeconds * percent);
            targetTime = ClampToSongLength(targetTime);

            audioPlayer.CurrentTime = targetTime;

            ProgressSlider.Value = Math.Min(targetTime.TotalSeconds, ProgressSlider.Maximum);
            CurrentTimeText.Text = FormatTime(targetTime);
        }

        private void VolumeSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            isChangingVolumeWithMouse = true;
            VolumeSlider.CaptureMouse();

            SetVolumeFromMouse(e.GetPosition(VolumeSlider).Y);

            e.Handled = true;
        }

        private void VolumeSlider_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isChangingVolumeWithMouse)
                return;

            SetVolumeFromMouse(e.GetPosition(VolumeSlider).Y);
        }

        private void VolumeSlider_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!isChangingVolumeWithMouse)
                return;

            SetVolumeFromMouse(e.GetPosition(VolumeSlider).Y);

            isChangingVolumeWithMouse = false;
            VolumeSlider.ReleaseMouseCapture();
        }

        private void SetVolumeFromMouse(double mouseY)
        {
            // This must match the top/bottom margin inside GreenVerticalSliderStyle.
            double trackVerticalMargin = 10;

            double trackHeight = VolumeSlider.ActualHeight - trackVerticalMargin * 2;

            if (trackHeight <= 0)
                return;

            double trackY = mouseY - trackVerticalMargin;

            double percent = 1.0 - trackY / trackHeight;
            percent = Math.Max(0, Math.Min(1, percent));

            VolumeSlider.Value = percent;
            audioPlayer.Volume = (float)percent;
        }

        private TimeSpan ClampToSongLength(TimeSpan time)
        {
            if (audioPlayer.TotalTime <= TimeSpan.Zero)
                return TimeSpan.Zero;

            if (time < TimeSpan.Zero)
                return TimeSpan.Zero;

            if (time > audioPlayer.TotalTime)
                return audioPlayer.TotalTime;

            return time;
        }

        public void ClearAlbumIfRemoved(Album removedAlbum)
        {
            if (currentAlbum != removedAlbum)
                return;

            currentAlbum = null;
            currentSong = null;
            currentSongIndex = -1;
            playbackHistory.Clear();

            audioPlayer.Stop();
            progressTimer.Stop();

            CurrentSongTitle.Text = "";
            BackCoverImage.Source = null;
            PlayerArtChanged?.Invoke(null);
            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = "0:00";

            ProgressSlider.Value = 0;

            waveformPoints.Clear();
            AmplitudeMountain.Points.Clear();

            SetPlayIconToPlay();
            PlaybackStateChanged?.Invoke(false);
            CurrentSongChanged?.Invoke(null);
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            if (isSeekingWithMouse)
                return;

            TimeSpan currentTime = ClampToSongLength(audioPlayer.CurrentTime);

            ProgressSlider.Value = Math.Min(currentTime.TotalSeconds, ProgressSlider.Maximum);
            CurrentTimeText.Text = FormatTime(currentTime);

            if (audioPlayer.TotalTime > TimeSpan.Zero &&
                audioPlayer.IsPlaying &&
                audioPlayer.TotalTime - currentTime <= TimeSpan.FromMilliseconds(80))
            {
                PlayNextSong();
            }
        }

        private void ProgressSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (isSeekingWithMouse)
            {
                TimeSpan previewTime = ClampToSongLength(TimeSpan.FromSeconds(ProgressSlider.Value));
                CurrentTimeText.Text = FormatTime(previewTime);
            }
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            audioPlayer.Volume = (float)VolumeSlider.Value;
        }

        private void SetPlayIconToPlay()
        {
            PlayIcon.Source = new BitmapImage(new Uri("/assets/icons/play-256.png", UriKind.Relative));
        }

        private void SetPlayIconToPause()
        {
            PlayIcon.Source = new BitmapImage(new Uri("/assets/icons/pause-256.png", UriKind.Relative));
        }

        private string FormatTime(TimeSpan time)
        {
            if (time.TotalHours >= 1)
                return time.ToString(@"h\:mm\:ss");

            return time.ToString(@"m\:ss");
        }

        private void AudioPlayer_PlaybackStopped()
        {
            Dispatcher.Invoke(() =>
            {
                if (audioPlayer.TotalTime > TimeSpan.Zero &&
                    audioPlayer.CurrentTime >= audioPlayer.TotalTime - TimeSpan.FromMilliseconds(500))
                {
                    PlayNextSong();
                    return;
                }

                progressTimer.Stop();
                SetPlayIconToPlay();
                PlaybackStateChanged?.Invoke(false);
            });
        }

        private void DrawWaveformMountain()
        {
            double width = ProgressArea.ActualWidth;
            double height = ProgressArea.ActualHeight;

            if (width <= 0 || height <= 0 || waveformPoints.Count == 0)
                return;

            double baseY = height - 14;
            double maxMountainHeight = height - 20;

            PointCollection points = new PointCollection();

            points.Add(new Point(0, baseY));

            for (int i = 0; i < waveformPoints.Count; i++)
            {
                double x = i * (width / Math.Max(1, waveformPoints.Count - 1));
                double value = Math.Pow(waveformPoints[i], 0.65);
                double y = baseY - value * maxMountainHeight;

                points.Add(new Point(x, y));
            }

            points.Add(new Point(width, baseY));

            AmplitudeMountain.Points = points;
        }

        private List<Song> GetPlayableSongsForCurrentAlbum()
        {
            if (currentAlbum == null)
                return new List<Song>();

            if (!likedOnlyMode)
                return currentAlbum.Songs.ToList();

            List<Song> likedSongs = currentAlbum.Songs
                .Where(song => song.IsLiked)
                .ToList();

            if (likedSongs.Count == 0)
                return currentAlbum.Songs.ToList();

            return likedSongs;
        }

        private string GetCurrentPlayerArtPath(Album album)
        {
            if (album.AlbumArtPaths != null && album.AlbumArtPaths.Count > 0)
            {
                if (album.PlayerArtIndex < 0 || album.PlayerArtIndex >= album.AlbumArtPaths.Count)
                    album.PlayerArtIndex = album.AlbumArtPaths.Count > 1 ? 1 : 0;

                return album.AlbumArtPaths[album.PlayerArtIndex];
            }

            if (!string.IsNullOrWhiteSpace(album.BackCoverPath))
                return album.BackCoverPath;

            return album.CoverPath;
        }

        private void ProgressArea_MouseEnter(object sender, MouseEventArgs e)
        {
            ProgressSlider.Tag = "Hover";
            AmplitudeMountain.Tag = "Hover";
        }

        private void ProgressArea_MouseLeave(object sender, MouseEventArgs e)
        {
            ProgressSlider.Tag = null;
            AmplitudeMountain.Tag = null;
        }

        private Song GetRandomPlayableSong(List<Song> playableSongs)
        {
            if (playableSongs.Count == 1)
                return playableSongs[0];

            Song randomSong;

            do
            {
                randomSong = playableSongs[random.Next(playableSongs.Count)];
            }
            while (randomSong == currentSong);

            return randomSong;
        }

        public void PreviousSong()
        {
            if (playbackHistory.Count > 0)
            {
                PlaybackHistoryItem previousItem = playbackHistory.Pop();

                LoadSong(previousItem.Album, previousItem.Song, addToHistory: false);
                return;
            }

            if (currentAlbum == null || currentSong == null)
                return;

            List<Song> playableSongs = GetPlayableSongsForCurrentAlbum();

            if (playableSongs.Count == 0)
                return;

            int currentIndex = playableSongs.IndexOf(currentSong);

            if (currentIndex < 0)
                currentIndex = 0;
            else
                currentIndex--;

            if (currentIndex < 0)
                currentIndex = playableSongs.Count - 1;

            LoadSong(currentAlbum, playableSongs[currentIndex]);
        }

        public void TogglePlayPause()
        {
            Play_Click(this, new RoutedEventArgs());
        }

        public void NextSong()
        {
            if (currentAlbum == null || currentSong == null)
                return;

            List<Song> playableSongs = GetPlayableSongsForCurrentAlbum();

            if (playableSongs.Count == 0)
                return;

            if (shuffleMode)
            {
                Song randomSong = GetRandomPlayableSong(playableSongs);
                LoadSong(currentAlbum, randomSong);
                return;
            }

            int currentIndex = playableSongs.IndexOf(currentSong);

            if (currentIndex < 0)
                currentIndex = 0;
            else
                currentIndex++;

            if (currentIndex >= playableSongs.Count)
                currentIndex = 0;

            LoadSong(currentAlbum, playableSongs[currentIndex]);
        }
    }
}