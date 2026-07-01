using NAudio.Wave;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

using musicplayer.Models;
using musicplayer.Services;

using System.Windows.Media.Animation;

namespace musicplayer.View.UserControls
{
    public partial class PlayerBar : UserControl
    {
        private readonly AudioPlayerService audioPlayer = new AudioPlayerService();
        private readonly DispatcherTimer progressTimer = new DispatcherTimer();

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
        private readonly Stack<PlaybackHistoryItem> forwardHistory = new Stack<PlaybackHistoryItem>();

        private bool isLoadingSavedVolume = false;

        private readonly List<float> waveformPoints = new List<float>();
        private const int WaveformPointCount = 120;

        private double displayedBassLevel = 0;
        private double displayedMidLevel = 0;
        private double displayedTrebleLevel = 0;

        private readonly Dictionary<string, List<float>> waveformCache = new Dictionary<string, List<float>>(StringComparer.OrdinalIgnoreCase);
        private int waveformBuildVersion = 0;

        public PlayerBar()
        {
            InitializeComponent();

            progressTimer.Interval = TimeSpan.FromMilliseconds(25);
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

        private void LikeCurrentSongButton_Click(object sender, RoutedEventArgs e)
        {
            if (currentSong == null)
                return;

            currentSong.IsLiked = !currentSong.IsLiked;

            UpdateCurrentSongLikeIcon();

            LibraryStorage.SaveLibrary();

            CurrentSongChanged?.Invoke(currentSong);
        }

        private void UpdateCurrentSongLikeIcon()
        {
            if (currentSong == null || !currentSong.IsLiked)
            {
                LikeCurrentSongIcon.Source =
                    new BitmapImage(new Uri("/assets/icons/heart_hollow.png", UriKind.Relative));
                return;
            }

            LikeCurrentSongIcon.Source =
                new BitmapImage(new Uri("/assets/icons/heart_filled.png", UriKind.Relative));
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

            List<string> availableArt = GetCurrentAvailablePlayerArt(currentAlbum);

            if (availableArt.Count == 0)
                return;

            currentAlbum.PlayerArtIndex++;

            if (currentAlbum.PlayerArtIndex >= availableArt.Count)
                currentAlbum.PlayerArtIndex = 0;

            SetBackCoverImage(currentAlbum);

            LibraryStorage.SaveLibrary();
        }

        public void LoadSavedVolume()
        {
            isLoadingSavedVolume = true;

            double savedVolume = AppData.Library.VolumeLevel;

            if (savedVolume < 0.0 || savedVolume > 1.0)
                savedVolume = 0.5;

            VolumeSlider.Value = savedVolume;
            audioPlayer.Volume = (float)savedVolume;

            isLoadingSavedVolume = false;
        }

        public void LoadSong(Album album, Song song, bool addToHistory = true, bool clearForwardHistory = true)
        {
            bool albumChanged = currentAlbum != null && !ReferenceEquals(currentAlbum, album);

            if (albumChanged)
            {
                playbackHistory.Clear();
                forwardHistory.Clear();
                addToHistory = false;
            }
            else if (clearForwardHistory)
            {
                forwardHistory.Clear();
            }

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
                300
            );

            CurrentSongTitle.Text = song.Title;

            UpdateCurrentSongLikeIcon();

            ProgressSlider.Maximum = Math.Max(1, audioPlayer.TotalTime.TotalSeconds);
            ProgressSlider.Value = 0;

            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = FormatTime(audioPlayer.TotalTime);

            PrepareWaveformForLoading();

            audioPlayer.Play();
            progressTimer.Start();

            SetPlayIconToPause();
            PlaybackStateChanged?.Invoke(true);

            int buildVersion = ++waveformBuildVersion;
            BuildWaveformInBackground(song, buildVersion);
        }

        private async void BuildWaveformInBackground(Song song, int buildVersion)
        {
            if (waveformCache.TryGetValue(song.FilePath, out List<float>? cachedWaveform))
            {
                if (currentSong != song || buildVersion != waveformBuildVersion)
                    return;

                waveformPoints.Clear();
                waveformPoints.AddRange(cachedWaveform);

                DrawWaveformMountain();
                AnimateWaveformScale(1.0, 180);
                return;
            }

            List<float> builtWaveform =
                await WaveformService.BuildWaveformFromFileAsync(song.FilePath, WaveformPointCount);

            if (currentSong != song || buildVersion != waveformBuildVersion)
                return;

            waveformCache[song.FilePath] = builtWaveform;

            waveformPoints.Clear();
            waveformPoints.AddRange(builtWaveform);

            DrawWaveformMountain();
            AnimateWaveformScale(1.0, 180);
        }

        private void PrepareWaveformForLoading()
        {
            if (AmplitudeMountain.Points.Count == 0)
                DrawFlatWaveformPlaceholder();

            AnimateWaveformScale(0.08, 130);
        }

        private void DrawFlatWaveformPlaceholder()
        {
            double width = ProgressArea.ActualWidth;
            double height = ProgressArea.ActualHeight - 8;

            if (width <= 0 || height <= 0)
                return;

            double baseY = height;
            double topY = height - 5;

            PointCollection points = new PointCollection
    {
        new Point(0, baseY),
        new Point(0, topY),
        new Point(width, topY),
        new Point(width, baseY)
    };

            AmplitudeMountain.Points = points;
        }

        private void DrawWaveformMountain()
        {
            double width = ProgressArea.ActualWidth;
            double height = ProgressArea.ActualHeight - 8;

            if (width <= 0 || height <= 0 || waveformPoints.Count == 0)
                return;

            PointCollection points = new PointCollection();

            double baseY = height;
            points.Add(new Point(0, baseY));

            double stepX = waveformPoints.Count > 1
                ? width / (waveformPoints.Count - 1)
                : width;

            for (int i = 0; i < waveformPoints.Count; i++)
            {
                double x = i * stepX;

                double value = waveformPoints[i];
                value = Math.Max(0.0, Math.Min(1.0, value));
                value = Math.Sqrt(value);

                double amplitude = value * height * 0.95;
                double y = baseY - amplitude;

                points.Add(new Point(x, y));
            }

            points.Add(new Point(width, baseY));

            AmplitudeMountain.Points = points;
        }

        private void AnimateWaveformScale(double targetScale, int milliseconds)
        {
            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetScale,
                Duration = TimeSpan.FromMilliseconds(milliseconds),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseInOut
                }
            };

            WaveformScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }

        private void ClearWaveformAndEqualizer()
        {
            waveformPoints.Clear();
            AmplitudeMountain.Points.Clear();

            WaveformScaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, null);
            WaveformScaleTransform.ScaleY = 1;

            displayedBassLevel = 0;
            displayedMidLevel = 0;
            displayedTrebleLevel = 0;

            BassEqBar.Height = 3;
            MidEqBar.Height = 3;
            TrebleEqBar.Height = 3;
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

            double width = ProgressArea.ActualWidth;

            if (width <= 0)
                return;

            double percent = mouseX / width;
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
            forwardHistory.Clear();

            audioPlayer.Stop();
            progressTimer.Stop();

            CurrentSongTitle.Text = "";
            BackCoverImage.Source = null;
            PlayerArtChanged?.Invoke(null);
            CurrentTimeText.Text = "0:00";
            TotalTimeText.Text = "0:00";

            ProgressSlider.Value = 0;

            ClearWaveformAndEqualizer();

            SetPlayIconToPlay();
            PlaybackStateChanged?.Invoke(false);
            CurrentSongChanged?.Invoke(null);
            UpdateCurrentSongLikeIcon();
        }

        private void ProgressTimer_Tick(object? sender, EventArgs e)
        {
            TimeSpan currentTime = ClampToSongLength(audioPlayer.CurrentTime);

            UpdateEqualizerBars();

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

            if (isLoadingSavedVolume)
                return;

            AppData.Library.VolumeLevel = VolumeSlider.Value;
            LibraryStorage.SaveLibrary();
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

        private bool IsSongPlayableInCurrentMode(Song song)
        {
            if (currentAlbum == null)
                return false;

            List<Song> playableSongs = GetPlayableSongsForCurrentAlbum();

            return playableSongs.Any(playableSong =>
                playableSong.FilePath.Equals(song.FilePath, StringComparison.OrdinalIgnoreCase));
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
            List<string> availableArt = GetCurrentAvailablePlayerArt(album);

            if (availableArt.Count == 0)
                return "";

            if (album.PlayerArtIndex < 0 || album.PlayerArtIndex >= availableArt.Count)
                album.PlayerArtIndex = 0;

            return availableArt[album.PlayerArtIndex];
        }

        private List<string> GetCurrentAvailablePlayerArt(Album album)
        {
            List<string> availableArt = new List<string>();

            if (currentSong?.AlbumArtPaths != null)
            {
                availableArt.AddRange(
                    currentSong.AlbumArtPaths
                        .Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                );
            }

            if (album.AlbumArtPaths != null)
            {
                availableArt.AddRange(
                    album.AlbumArtPaths
                        .Where(path => !string.IsNullOrWhiteSpace(path) && System.IO.File.Exists(path))
                );
            }

            if (!string.IsNullOrWhiteSpace(album.BackCoverPath) && System.IO.File.Exists(album.BackCoverPath))
                availableArt.Add(album.BackCoverPath);

            if (!string.IsNullOrWhiteSpace(album.CoverPath) && System.IO.File.Exists(album.CoverPath))
                availableArt.Add(album.CoverPath);

            return availableArt
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
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
            if (currentAlbum == null || currentSong == null)
                return;

            while (playbackHistory.Count > 0)
            {
                PlaybackHistoryItem previousItem = playbackHistory.Pop();

                if (!ReferenceEquals(previousItem.Album, currentAlbum))
                    continue;

                if (!IsSongPlayableInCurrentMode(previousItem.Song))
                    continue;

                forwardHistory.Push(new PlaybackHistoryItem
                {
                    Album = currentAlbum,
                    Song = currentSong
                });

                LoadSong(
                    previousItem.Album,
                    previousItem.Song,
                    addToHistory: false,
                    clearForwardHistory: false
                );

                return;
            }

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

            forwardHistory.Push(new PlaybackHistoryItem
            {
                Album = currentAlbum,
                Song = currentSong
            });

            LoadSong(
                currentAlbum,
                playableSongs[currentIndex],
                addToHistory: false,
                clearForwardHistory: false
            );
        }

        public void TogglePlayPause()
        {
            Play_Click(this, new RoutedEventArgs());
        }

        public void NextSong()
        {
            if (currentAlbum == null || currentSong == null)
                return;

            while (forwardHistory.Count > 0)
            {
                PlaybackHistoryItem nextItem = forwardHistory.Pop();

                if (!ReferenceEquals(nextItem.Album, currentAlbum))
                    continue;

                if (!IsSongPlayableInCurrentMode(nextItem.Song))
                    continue;

                LoadSong(
                    nextItem.Album,
                    nextItem.Song,
                    addToHistory: true,
                    clearForwardHistory: false
                );

                return;
            }

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

        private void UpdateEqualizerBars()
        {
            float bassTarget = audioPlayer.IsPlaying ? audioPlayer.BassLevel : 0f;
            float midTarget = audioPlayer.IsPlaying ? audioPlayer.MidLevel : 0f;
            float trebleTarget = audioPlayer.IsPlaying ? audioPlayer.TrebleLevel : 0f;

            AnimateEqualizerBar(BassEqBar, bassTarget, 3.0);
            AnimateEqualizerBar(MidEqBar, midTarget, 2.6);
            AnimateEqualizerBar(TrebleEqBar, trebleTarget, 4.2);
        }

        private void AnimateEqualizerBar(Border bar, float level, double boost)
        {
            double minHeight = 3;
            double maxHeight = 78;

            double value = level * boost;
            value = Math.Max(0.0, Math.Min(1.0, value));

            // Makes smaller values more visible.
            value = Math.Sqrt(value);

            double targetHeight = minHeight + value * (maxHeight - minHeight);

            DoubleAnimation animation = new DoubleAnimation
            {
                To = targetHeight,
                Duration = TimeSpan.FromMilliseconds(45),
                EasingFunction = new QuadraticEase
                {
                    EasingMode = EasingMode.EaseOut
                }
            };

            bar.BeginAnimation(HeightProperty, animation, HandoffBehavior.SnapshotAndReplace);
        }

    }
}