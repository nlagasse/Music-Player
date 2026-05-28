using NAudio.Wave;

namespace musicplayer.Services
{
    public class PeakSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider source;
        private readonly int samplesPerNotification;
        private int sampleCounter;

        public event Action<float>? PeakCalculated;

        public WaveFormat WaveFormat => source.WaveFormat;

        public PeakSampleProvider(ISampleProvider source, int samplesPerNotification = 1024)
        {
            this.source = source;
            this.samplesPerNotification = samplesPerNotification;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = source.Read(buffer, offset, count);

            float max = 0;

            for (int i = 0; i < samplesRead; i++)
            {
                float sample = Math.Abs(buffer[offset + i]);

                if (sample > max)
                    max = sample;

                sampleCounter++;

                if (sampleCounter >= samplesPerNotification)
                {
                    PeakCalculated?.Invoke(max);
                    sampleCounter = 0;
                    max = 0;
                }
            }

            return samplesRead;
        }
    }
}