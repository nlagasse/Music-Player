using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows;

namespace musicplayer.Services
{
    public class OperatingSystemData
    {
        private readonly DateTime appStartTime = DateTime.Now;

        public string AudioDebugText { get; private set; } = "AUDIO:--  BITRATE:--  LATENCY:--";

        public string BuildText()
        {
            GetCursorPos(out POINT cursorPoint);

            long unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long unixMilliseconds = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string osDescription = RuntimeInformation.OSDescription;
            string osArchitecture = RuntimeInformation.OSArchitecture.ToString();
            string processArchitecture = RuntimeInformation.ProcessArchitecture.ToString();

            Process currentProcess = Process.GetCurrentProcess();

            double memoryMb = currentProcess.WorkingSet64 / 1024.0 / 1024.0;
            int threadCount = currentProcess.Threads.Count;

            TimeSpan uptime = DateTime.Now - appStartTime;
            string uptimeText = $"{(int)uptime.TotalHours:00}:{uptime.Minutes:00}:{uptime.Seconds:00}";

            int gen0 = GC.CollectionCount(0);
            int gen1 = GC.CollectionCount(1);
            int gen2 = GC.CollectionCount(2);

            int screenWidth = (int)SystemParameters.PrimaryScreenWidth;
            int screenHeight = (int)SystemParameters.PrimaryScreenHeight;

            return
                $"OS:{osDescription}  ARCH:{osArchitecture}/{processArchitecture}  SCREEN:{screenWidth}x{screenHeight}\n" +
                $"UNIX:{unixSeconds}  MS:{unixMilliseconds}  LOCAL:{DateTime.Now:HH:mm:ss}  UTC:{DateTime.UtcNow:HH:mm:ss}\n" +
                $"MOUSE:{cursorPoint.X},{cursorPoint.Y}  PROCESS:{memoryMb:0.0}MB  THREADS:{threadCount}  CLR:{Environment.Version}\n" +
                $"UPTIME:{uptimeText}  GC:{gen0 + gen1 + gen2}  GEN0:{gen0} GEN1:{gen1} GEN2:{gen2}\n" +
                $"{AudioDebugText}";
        }

        public void SetAudioDebugInfo(int sampleRate, int channels, int? bitrateKbps, int? latencyMs)
        {
            string audioText = sampleRate > 0 && channels > 0
                ? $"AUDIO:{sampleRate}HZ {channels}CH"
                : "AUDIO:--";

            string bitrateText = bitrateKbps.HasValue && bitrateKbps.Value > 0
                ? $"BITRATE:{bitrateKbps.Value}KBPS"
                : "BITRATE:--";

            string latencyText = latencyMs.HasValue && latencyMs.Value > 0
                ? $"LATENCY:{latencyMs.Value}MS"
                : "LATENCY:--";

            AudioDebugText = $"{audioText}  {bitrateText}  {latencyText}";
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