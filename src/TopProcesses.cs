using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace MiniPerfApp
{
    public class ProcInfo
    {
        public string Name;
        public double Cpu;      // percent
        public double MemoryMb;
    }

    // samples top processes every 2s on its own thread
    public static class TopProcesses
    {
        private class Prev
        {
            public TimeSpan Cpu;
        }

        private static Thread _thread;
        private static volatile bool _stop;
        private static volatile List<ProcInfo> _byCpu = new List<ProcInfo>();
        private static volatile List<ProcInfo> _byMem = new List<ProcInfo>();
        private static int _procCount = Environment.ProcessorCount;

        public static List<ProcInfo> ByCpu { get { return _byCpu; } }
        public static List<ProcInfo> ByMem { get { return _byMem; } }

        public static void Start()
        {
            _stop = false;
            _thread = new Thread(new ThreadStart(Run));
            _thread.IsBackground = true;
            _thread.Start();
        }

        public static void Stop()
        {
            _stop = true;
            if (_thread != null) _thread.Join(1000);
        }

        private static void Run()
        {
            Dictionary<int, Prev> prev = new Dictionary<int, Prev>();
            SampleOnce(prev); // warm-up, discard
            while (!_stop)
            {
                SampleOnce(prev);
                for (int i = 0; i < 20 && !_stop; i++) Thread.Sleep(100);
            }
        }

        private static void SampleOnce(Dictionary<int, Prev> prev)
        {
            try
            {
                Process[] ps = Process.GetProcesses();
                DateTime now = DateTime.Now;
                List<ProcInfo> all = new List<ProcInfo>();
                DateTime refNow = now;
                foreach (Process p in ps)
                {
                    try
                    {
                        TimeSpan cpu = p.TotalProcessorTime;
                        Prev pv;
                        if (!prev.TryGetValue(p.Id, out pv))
                        {
                            pv = new Prev();
                            pv.Cpu = cpu;
                            prev[p.Id] = pv;
                        }
                        double dms = (cpu - pv.Cpu).TotalMilliseconds;
                        double pct = dms / (refNow - _lastSample).TotalMilliseconds / _procCount * 100.0;
                        pv.Cpu = cpu;
                        ProcInfo pi = new ProcInfo();
                        pi.Name = p.ProcessName;
                        pi.Cpu = pct < 0 ? 0 : pct;
                        pi.MemoryMb = p.WorkingSet64 / (1024.0 * 1024.0);
                        all.Add(pi);
                    }
                    catch { }
                }
                // top5 cpu
                all.Sort(delegate(ProcInfo a, ProcInfo b) { return b.Cpu.CompareTo(a.Cpu); });
                List<ProcInfo> topCpu = new List<ProcInfo>();
                for (int i = 0; i < all.Count && i < 5; i++) topCpu.Add(all[i]);
                // top5 mem
                all.Sort(delegate(ProcInfo a, ProcInfo b) { return b.MemoryMb.CompareTo(a.MemoryMb); });
                List<ProcInfo> topMem = new List<ProcInfo>();
                for (int i = 0; i < all.Count && i < 5; i++) topMem.Add(all[i]);
                _byCpu = topCpu;
                _byMem = topMem;
                _lastSample = refNow;
            }
            catch { }
        }

        private static DateTime _lastSample = DateTime.Now;
    }
}
