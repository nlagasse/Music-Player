using NAudio.Dsp;
using NAudio.Wave;

namespace musicplayer.Services
{
    public class FrequencyAnalyzerSampleProvider : ISampleProvider
    {
        private readonly ISampleProvider sourceProvider;

        private const int FftLength = 512;
        private const int M = 9;

        private readonly Complex[] fftBuffer = new Complex[FftLength];
        private int fftPosition = 0;

        private float bassLevel = 0f;
        private float midLevel = 0f;
        private float trebleLevel = 0f;

        public WaveFormat WaveFormat => sourceProvider.WaveFormat;

        public float BassLevel => bassLevel;
        public float MidLevel => midLevel;
        public float TrebleLevel => trebleLevel;

        public FrequencyAnalyzerSampleProvider(ISampleProvider sourceProvider)
        {
            this.sourceProvider = sourceProvider;
        }

        public int Read(float[] buffer, int offset, int count)
        {
            int samplesRead = sourceProvider.Read(buffer, offset, count);

            int channels = WaveFormat.Channels;

            for (int sample = 0; sample < samplesRead; sample += channels)
            {
                float monoSample = 0f;

                for (int channel = 0; channel < channels && sample + channel < samplesRead; channel++)
                    monoSample += buffer[offset + sample + channel];

                monoSample /= channels;

                AddSampleToFft(monoSample);
            }

            return samplesRead;
        }

        private void AddSampleToFft(float sample)
        {
            fftBuffer[fftPosition].X =
                (float)(sample * FastFourierTransform.HammingWindow(fftPosition, FftLength));

            fftBuffer[fftPosition].Y = 0f;

            fftPosition++;

            if (fftPosition >= FftLength)
            {
                fftPosition = 0;
                CalculateFrequencyBands();
            }
        }

        private void CalculateFrequencyBands()
        {
            Complex[] fftClone = new Complex[FftLength];
            Array.Copy(fftBuffer, fftClone, FftLength);

            FastFourierTransform.FFT(true, M, fftClone);

            float bass = GetBandEnergy(fftClone, 20, 350) * 3.5f;
            float mid = GetBandEnergy(fftClone, 350, 3500) * 4.5f;
            float treble = GetBandEnergy(fftClone, 3500, 16000) * 8.0f;

            bass = Math.Min(1f, bass);
            mid = Math.Min(1f, mid);
            treble = Math.Min(1f, treble);

            bassLevel = SmoothLevel(bassLevel, bass);
            midLevel = SmoothLevel(midLevel, mid);
            trebleLevel = SmoothLevel(trebleLevel, treble);
        }

        private float GetBandEnergy(Complex[] fftData, float lowFrequency, float highFrequency)
        {
            int sampleRate = WaveFormat.SampleRate;

            int lowBin = FrequencyToBin(lowFrequency, sampleRate);
            int highBin = FrequencyToBin(highFrequency, sampleRate);

            lowBin = Math.Max(1, lowBin);
            highBin = Math.Min(FftLength / 2 - 1, highBin);

            if (highBin <= lowBin)
                return 0f;

            double sum = 0;

            for (int bin = lowBin; bin <= highBin; bin++)
            {
                double real = fftData[bin].X;
                double imaginary = fftData[bin].Y;
                double magnitude = Math.Sqrt(real * real + imaginary * imaginary);

                sum += magnitude;
            }

            double average = sum / (highBin - lowBin + 1);
            double normalized = average * 18.0;

            if (double.IsNaN(normalized) || double.IsInfinity(normalized))
                return 0f;

            return (float)Math.Max(0.0, Math.Min(1.0, normalized));
        }

        private int FrequencyToBin(float frequency, int sampleRate)
        {
            return (int)(frequency * FftLength / sampleRate);
        }

        private float SmoothLevel(float oldValue, float newValue)
        {
            return newValue;
        }
    }
}