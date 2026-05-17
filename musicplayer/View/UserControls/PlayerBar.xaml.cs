using musicplayer.Models;
using musicplayer.Services;
using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

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

        private bool likedOnlyMode = false;

        public event Action<Album?>? BackCoverMiddleClicked;

        public event Action<Album>? AlbumPlaybackStarted;

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

            BackCoverImage.Source = LoadImage(imagePath);

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

        private void BackCoverImage_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (currentAlbum == null)
                return;

            CycleCurrentAlbumArt();

            e.Handled = true;
        }

        private void BackCoverImage_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Middle)
                return;

            BackCoverMiddleClicked?.Invoke(currentAlbum);

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

        public void LoadSong(Album album, Song song)
        {
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
                return;

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
            if (currentAlbum == null || currentAlbum.Songs.Count == 0)
                return;

            if (likedOnlyMode)
            {
                Song? previousLikedSong = GetPreviousLikedSong();

                if (previousLikedSong != null)
                    LoadSong(currentAlbum, previousLikedSong);

                return;
            }

            if (currentSongIndex <= 0)
                LoadSong(currentAlbum, currentAlbum.Songs[currentAlbum.Songs.Count - 1]);
            else
                LoadSong(currentAlbum, currentAlbum.Songs[currentSongIndex - 1]);
        }

        private Song? GetPreviousLikedSong()
        {
            if (currentAlbum == null)
                return null;

            if (currentAlbum.Songs.Count == 0)
                return null;

            int startIndex = currentSongIndex - 1;

            for (int i = startIndex; i >= 0; i--)
            {
                if (currentAlbum.Songs[i].IsLiked)
                    return currentAlbum.Songs[i];
            }

            for (int i = currentAlbum.Songs.Count - 1; i >= currentSongIndex && i >= 0; i--)
            {
                if (currentAlbum.Songs[i].IsLiked)
                    return currentAlbum.Songs[i];
            }

            return null;
        }

        private void Next_Click(object sender, RoutedEventArgs e)
        {
            PlayNextSong();
        }

        private void PlayNextSong()
        {
            if (currentAlbum == null)
                return;

            if (currentAlbum.Songs.Count == 0)
                return;

            if (likedOnlyMode)
            {
                Song? nextLikedSong = GetNextLikedSong();

                if (nextLikedSong != null)
                    LoadSong(currentAlbum, nextLikedSong);

                return;
            }

            if (currentSongIndex + 1 >= currentAlbum.Songs.Count)
            {
                LoadSong(currentAlbum, currentAlbum.Songs[0]);
                return;
            }

            LoadSong(currentAlbum, currentAlbum.Songs[currentSongIndex + 1]);
        }

        private Song? GetNextLikedSong()
        {
            if (currentAlbum == null)
                return null;

            if (currentAlbum.Songs.Count == 0)
                return null;

            int startIndex = currentSongIndex + 1;

            for (int i = startIndex; i < currentAlbum.Songs.Count; i++)
            {
                if (currentAlbum.Songs[i].IsLiked)
                    return currentAlbum.Songs[i];
            }

            for (int i = 0; i <= currentSongIndex && i < currentAlbum.Songs.Count; i++)
            {
                if (currentAlbum.Songs[i].IsLiked)
                    return currentAlbum.Songs[i];
            }

            return null;
        }

        public void SetLikedOnlyMode(bool enabled)
        {
            likedOnlyMode = enabled;
        }

        private void AudioPlayer_PlaybackStopped()
        {
            // ProgressTimer_Tick handles auto-next.
        }

        private void BuildWaveformInBackground(Song song)
        {
            Task.Run(() =>
            {
                List<float> builtWaveform = BuildWaveformFromFile(song.FilePath, WaveformPointCount);

                Dispatcher.BeginInvoke(new Action(() =>
                {
                    if (currentSong != song)
                        return;

                    waveformPoints.Clear();
                    waveformPoints.AddRange(builtWaveform);
                    DrawWaveformMountain();
                }));
            });
        }

        private List<float> BuildWaveformFromFile(string filePath, int pointCount)
        {
            List<float> points = new List<float>();

            for (int i = 0; i < pointCount; i++)
                points.Add(0);

            try
            {
                using AudioFileReader reader = new AudioFileReader(filePath);

                int channels = reader.WaveFormat.Channels;
                int sampleRate = reader.WaveFormat.SampleRate;

                double totalSeconds = reader.TotalTime.TotalSeconds;

                if (totalSeconds <= 0)
                    return points;

                long totalSamples = (long)(sampleRate * channels * totalSeconds);
                long samplesPerPoint = Math.Max(1, totalSamples / pointCount);

                float[] buffer = new float[4096];

                long samplePosition = 0;
                int read;

                double sumSquares = 0;
                long samplesInCurrentPoint = 0;
                int currentPointIndex = 0;

                while ((read = reader.Read(buffer, 0, buffer.Length)) > 0)
                {
                    for (int i = 0; i < read; i++)
                    {
                        float sample = buffer[i];

                        sumSquares += sample * sample;
                        samplesInCurrentPoint++;
                        samplePosition++;

                        int pointIndex = (int)Math.Min(pointCount - 1, samplePosition / samplesPerPoint);

                        if (pointIndex != currentPointIndex || samplePosition >= totalSamples)
                        {
                            if (samplesInCurrentPoint > 0)
                            {
                                double rms = Math.Sqrt(sumSquares / samplesInCurrentPoint);
                                points[currentPointIndex] = (float)rms;
                            }

                            sumSquares = 0;
                            samplesInCurrentPoint = 0;
                            currentPointIndex = pointIndex;

                            if (currentPointIndex >= pointCount)
                                break;
                        }
                    }
                }

                float max = points.Max();

                if (max > 0)
                {
                    for (int i = 0; i < points.Count; i++)
                        points[i] = points[i] / max;
                }

                List<float> smoothed = new List<float>();

                for (int i = 0; i < points.Count; i++)
                {
                    float previous = i > 0 ? points[i - 1] : points[i];
                    float current = points[i];
                    float next = i < points.Count - 1 ? points[i + 1] : points[i];

                    smoothed.Add((previous + current + next) / 3f);
                }

                return smoothed;
            }
            catch
            {
                return points;
            }
        }

        private void DrawWaveformMountain()
        {
            double width = ProgressArea.ActualWidth;
            double height = ProgressArea.ActualHeight;

            if (width <= 0 || height <= 0 || waveformPoints.Count == 0)
                return;

            double baseY = height - 18;
            double maxMountainHeight = height - 32;

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
            double height = VolumeSlider.ActualHeight;

            if (height <= 0)
                return;

            double percent = 1.0 - mouseY / height;
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

            audioPlayer.Stop();
            progressTimer.Stop();

            CurrentSongTitle.Text = "";
            BackCoverImage.Source = null;
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

        public void PreviousSong()
        {
            Prev_Click(this, new RoutedEventArgs());
        }

        public void TogglePlayPause()
        {
            Play_Click(this, new RoutedEventArgs());
        }

        public void NextSong()
        {
            Next_Click(this, new RoutedEventArgs());
        }
    }
}