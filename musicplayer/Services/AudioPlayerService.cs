using NAudio.Wave;

namespace musicplayer.Services
{
    public class AudioPlayerService : IDisposable
    {
        private WaveOutEvent? outputDevice;
        private AudioFileReader? audioFile;
        private FrequencyAnalyzerSampleProvider? analyzerProvider;

        private bool suppressPlaybackStopped = false;

        public event Action? PlaybackStopped;

        public bool IsPlaying => outputDevice?.PlaybackState == PlaybackState.Playing;

        public float BassLevel => analyzerProvider?.BassLevel ?? 0f;
        public float MidLevel => analyzerProvider?.MidLevel ?? 0f;
        public float TrebleLevel => analyzerProvider?.TrebleLevel ?? 0f;

        public TimeSpan CurrentTime
        {
            get => audioFile?.CurrentTime ?? TimeSpan.Zero;
            set
            {
                if (audioFile != null)
                    audioFile.CurrentTime = value;
            }
        }

        public TimeSpan TotalTime => audioFile?.TotalTime ?? TimeSpan.Zero;

        public float Volume
        {
            get => audioFile?.Volume ?? 1.0f;
            set
            {
                if (audioFile != null)
                    audioFile.Volume = value;
            }
        }

        public int SampleRate => audioFile?.WaveFormat.SampleRate ?? 0;
        public int Channels => audioFile?.WaveFormat.Channels ?? 0;
        public int LatencyMilliseconds => outputDevice?.DesiredLatency ?? 0;

        public void Load(string filePath)
        {
            StopAndDisposeCurrentFile();

            audioFile = new AudioFileReader(filePath);
            analyzerProvider = new FrequencyAnalyzerSampleProvider(audioFile);

            outputDevice = new WaveOutEvent();
            outputDevice.PlaybackStopped += OutputDevice_PlaybackStopped;
            outputDevice.Init(analyzerProvider);
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

            analyzerProvider = null;

            suppressPlaybackStopped = false;
        }

        public void Dispose()
        {
            StopAndDisposeCurrentFile();
        }
    }
}