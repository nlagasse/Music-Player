using NAudio.Wave;

// Audio playback using NAudio 
namespace musicplayer.Services
{
    public class AudioPlayerService : IDisposable
    {
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;

        private bool suppressPlaybackStopped = false;

        public event Action? PlaybackStopped;

        public bool IsPlaying
        {
            get
            {
                return outputDevice?.PlaybackState == PlaybackState.Playing;
            }
        }

        public TimeSpan CurrentTime
        {
            get
            {
                return audioFile?.CurrentTime ?? TimeSpan.Zero;
            }
            set
            {
                if (audioFile != null)
                    audioFile.CurrentTime = value;
            }
        }

        public TimeSpan TotalTime
        {
            get
            {
                return audioFile?.TotalTime ?? TimeSpan.Zero;
            }
        }

        public float Volume
        {
            get
            {
                return audioFile?.Volume ?? 1.0f;
            }
            set
            {
                if (audioFile != null)
                    audioFile.Volume = value;
            }
        }

        public int SampleRate
        {
            get
            {
                return audioFile?.WaveFormat.SampleRate ?? 0;
            }
        }

        public int Channels
        {
            get
            {
                return audioFile?.WaveFormat.Channels ?? 0;
            }
        }

        public int LatencyMilliseconds
        {
            get
            {
                return outputDevice?.DesiredLatency ?? 0;
            }
        }

        public void Load(string filePath)
        {
            StopAndDisposeCurrentFile();

            audioFile = new AudioFileReader(filePath);

            outputDevice = new WaveOutEvent();
            outputDevice.Init(audioFile);

            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
        }

        public void Play()
        {
            outputDevice?.Play();
        }

        public void Pause()
        {
            outputDevice?.Pause();
        }

        public void Stop()
        {
            suppressPlaybackStopped = true;

            outputDevice?.Stop();

            if (audioFile != null)
                audioFile.CurrentTime = TimeSpan.Zero;

            suppressPlaybackStopped = false;
        }

        private void OutputDevice_PlaybackStopped(object? sender, StoppedEventArgs e)
        {
            if (suppressPlaybackStopped)
                return;

            PlaybackStopped?.Invoke();
        }

        private void StopAndDisposeCurrentFile()
        {
            suppressPlaybackStopped = true;

            if (outputDevice != null)
            {
                outputDevice.PlaybackStopped -= OutputDevice_PlaybackStopped;
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }

            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }

            suppressPlaybackStopped = false;
        }

        public void Dispose()
        {
            StopAndDisposeCurrentFile();
        }
    }
}