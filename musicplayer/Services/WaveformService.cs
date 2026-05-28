using NAudio.Wave;

namespace musicplayer.Services
{
    public static class WaveformService
    {
        public static Task<List<float>> BuildWaveformFromFileAsync(string filePath, int pointCount)
        {
            return Task.Run(() => BuildWaveformFromFile(filePath, pointCount));
        }

        private static List<float> BuildWaveformFromFile(string filePath, int pointCount)
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
    }
}