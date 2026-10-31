using System;
using System.Diagnostics;
using System.Threading;

namespace MiniPerfApp
{
    public class MetricsSnapshot
    {
        public double Cpu;
        public double Gpu;
        public double Npu;
        public double RamPct;
        public double NetDown;   // bytes/s
        public double NetUp;     // bytes/s
        public double DiskPct;
        public DateTime At;

        public MetricsSnapshot()
        {
            At = DateTime.Now;
        }
    }

    public class MetricsCollector : IDisposable
    {
        private Thread _thread;
        private volatile bool _stop;
        private volatile MetricsSnapshot _latest;
        private PerformanceCounter _cpu;
        private readonly object _lock = new object();

        public MetricsSnapshot Latest
        {
            get { return _latest; }
        }

        public void Start()
        {
            _cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true);
            _cpu.NextValue(); // warm-up discard
            _stop = false;
            _thread = new Thread(new ThreadStart(RunLoop));
            _thread.IsBackground = true;
            _thread.Start();
        }

        private void RunLoop()
        {
            while (!_stop)
            {
                try
                {
                    MetricsSnapshot s = new MetricsSnapshot();
                    s.Cpu = Clamp(_cpu.NextValue());
                    NativeMethods.MEMORYSTATUSEX mem = new NativeMethods.MEMORYSTATUSEX();
                    mem.dwLength = (uint)System.Runtime.InteropServices.Marshal.SizeOf(typeof(NativeMethods.MEMORYSTATUSEX));
                    if (NativeMethods.GlobalMemoryStatusEx(ref mem))
                    {
                        s.RamPct = Clamp((double)mem.dwMemoryLoad);
                    }
                    s.Gpu = 0; // segment 2
                    s.Npu = 0; // segment 2
                    s.NetDown = 0; // segment 2
                    s.NetUp = 0; // segment 2
                    s.DiskPct = 0; // segment 2
                    _latest = s;
                }
                catch { }
                for (int i = 0; i < 10 && !_stop; i++) Thread.Sleep(100);
            }
        }

        private static double Clamp(double v)
        {
            if (v < 0) return 0;
            if (v > 100) return 100;
            return v;
        }

        public void Stop()
        {
            _stop = true;
            if (_thread != null)
            {
                _thread.Join(1500);
            }
        }

        public void Dispose()
        {
            Stop();
            if (_cpu != null) _cpu.Dispose();
        }
    }
}
