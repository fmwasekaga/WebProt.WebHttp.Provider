using System;
using System.Diagnostics;

namespace WebProt.WebHttp.Provider.Helpers
{
    public class Usage
    {
        const float sampleFrequencyMillis = 1000;

        protected object syncLock = new object();
        protected PerformanceCounter cpuCounter;
        protected PerformanceCounter ramCounter;
        protected float lastCpuSample;
        protected float lastMemorySample;
        protected DateTime lastSampleTime;

        protected static readonly string[] suffixes = { "Bytes", "KB", "MB", "GB", "TB", "PB" };

        /// <summary>
        /// 
        /// </summary>
        public Usage()
        {
            this.cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
            this.ramCounter = new PerformanceCounter("Memory", "Available MBytes");
        }

        public void Sync()
        {
            if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > sampleFrequencyMillis)
            {
                lock (syncLock)
                {
                    if ((DateTime.UtcNow - lastSampleTime).TotalMilliseconds > sampleFrequencyMillis)
                    {
                        lastCpuSample = cpuCounter.NextValue();
                        lastMemorySample = ramCounter.NextValue();
                        lastSampleTime = DateTime.UtcNow;
                    }
                }
            }
        }

        public string GetCurrentCpuUsage()
        {
            return string.Format("{0:n1}{1}", lastCpuSample, "%");
        }

        public string GetAvailableRAM()
        {
            return FormatSize(lastMemorySample);
        }

        private string FormatSize(float bytes)
        {
            int counter = 0;
            decimal number = (decimal)bytes;
            while (Math.Round(number / 1024) >= 1)
            {
                number = number / 1024;
                counter++;
            }
            return string.Format("{0:n1}{1}", number, suffixes[counter]);
        }
    }
}
