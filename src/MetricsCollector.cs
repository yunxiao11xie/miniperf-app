using System;
using System.Collections.Generic;
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

    // tracks dynamic "GPU Engine" counter instances, groups by engine type
    internal class GpuEngineSampler
    {
        private const string Category = "GPU Engine";
        private const string Counter = "Utilization Percentage";
        private readonly Dictionary<string, PerformanceCounter> _counters =
            new Dictionary<string, PerformanceCounter>();

        private static string EngineTypeOf(string instance)
        {
            int i = instance.LastIndexOf("engtype_", StringComparison.OrdinalIgnoreCase);
            if (i < 0) return "";
            return instance.Substring(i + 8);
        }

        public void Sample(out double gpu, out double npu)
        {
            gpu = 0; npu = 0;
            try
            {
                string[] names = new PerformanceCounterCategory(Category).GetInstanceNames();
                Dictionary<string, double> byType = new Dictionary<string, double>();

                // drop vanished instances
                List<string> dead = new List<string>();
                foreach (KeyValuePair<string, PerformanceCounter> kv in _counters)
                {
                    bool alive = false;
                    foreach (string n in names) { if (n == kv.Key) { alive = true; break; } }
                    if (!alive) dead.Add(kv.Key);
                }
                foreach (string d in dead)
                {
                    _counters[d].Dispose();
                    _counters.Remove(d);
                }

                foreach (string n in names)
                {
                    string t = EngineTypeOf(n);
                    if (t.Length == 0) continue;
                    PerformanceCounter pc;
                    if (!_counters.TryGetValue(n, out pc))
                    {
                        pc = new PerformanceCounter(Category, Counter, n, true);
                        pc.NextValue(); // warm-up
                        _counters[n] = pc;
                    }
                    double v = pc.NextValue();
                    double cur;
                    byType.TryGetValue(t, out cur);
                    byType[t] = cur + v;
                }

                foreach (KeyValuePair<string, double> kv in byType)
                {
                    if (kv.Key == "Neural") { npu += kv.Value; }
                    else { if (kv.Value > gpu) gpu = kv.Value; }
                }
            }
            catch { }
            if (gpu > 100) gpu = 100;
            if (npu > 100) npu = 100;
        }
    }

    public class MetricsCollector : IDisposable
    {
        private Thread _thread;
        private volatile bool _stop;
        private volatile MetricsSnapshot _latest;
        private PerformanceCounter _cpu;
        private PerformanceCounter _diskIdle;
        private PerformanceCounter _netDown;
        private PerformanceCounter _netUp;
        private GpuEngineSampler _gpu;
        private readonly object _lock = new object();
        private bool _loggedEngines = false;

        public MetricsSnapshot Latest
        {
            get { return _latest; }
        }

        public void Start()
        {
            _cpu = new PerformanceCounter("Processor Information", "% Processor Utility", "_Total", true);
            _cpu.NextValue();
            _gpu = new GpuEngineSampler();
            try
            {
                _diskIdle = new PerformanceCounter("PhysicalDisk", "% Idle Time", "_Total", true);
                _diskIdle.NextValue();
                List<PerformanceCounter> downs = new List<PerformanceCounter>();
                List<PerformanceCounter> ups = new List<PerformanceCounter>();
                string[] ifaces = new PerformanceCounterCategory("Network Interface").GetInstanceNames();
                foreach (string n in ifaces)
                {
                    if (n.IndexOf("loopback", StringComparison.OrdinalIgnoreCase) >= 0) continue;
                    PerformanceCounter d = new PerformanceCounter("Network Interface", "Bytes Received/sec", n, true);
                    PerformanceCounter u = new PerformanceCounter("Network Interface", "Bytes Sent/sec", n, true);
                    d.NextValue(); u.NextValue();
                    downs.Add(d); ups.Add(u);
                }
                if (downs.Count == 1) { _netDown = downs[0]; _netUp = ups[0]; }
                else
                {
                    // multi-NIC: pick the counter set of the busiest later per sample is overkill;
                    // sum via a lightweight wrapper sampled below
                    _netDown = null;
                    _extraDown = downs; _extraUp = ups;
                }
            }
            catch { }
            _stop = false;
            _thread = new Thread(new ThreadStart(RunLoop));
            _thread.IsBackground = true;
            _thread.Start();
        }

        private List<PerformanceCounter> _extraDown;
        private List<PerformanceCounter> _extraUp;

        private void RunLoop()
        {
            // discard first round (warm-up across counters)
            SampleOnce(true);
            while (!_stop)
            {
                SampleOnce(false);
                for (int i = 0; i < 10 && !_stop; i++) Thread.Sleep(100);
            }
        }

        private void SampleOnce(bool warmup)
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
                double g, n;
                _gpu.Sample(out g, out n);
                s.Gpu = g; s.Npu = n;

                double down = 0, up = 0;
                if (_netDown != null) { down += _netDown.NextValue(); up += _netUp.NextValue(); }
                if (_extraDown != null)
                {
                    foreach (PerformanceCounter c in _extraDown) down += c.NextValue();
                    foreach (PerformanceCounter c in _extraUp) up += c.NextValue();
                }
                s.NetDown = Math.Max(0, down);
                s.NetUp = Math.Max(0, up);

                if (_diskIdle != null)
                {
                    double idle = _diskIdle.NextValue();
                    s.DiskPct = Clamp(100 - idle);
                }
                if (!warmup) _latest = s;
                if (!_loggedEngines && !warmup)
                {
                    _loggedEngines = true;
                    Log.Write(string.Format("metrics live: gpu={0:0.0} npu={1:0.0} down={2:0} up={3:0} disk={4:0.0}",
                        s.Gpu, s.Npu, s.NetDown, s.NetUp, s.DiskPct));
                }
            }
            catch (Exception ex)
            {
                Log.Write("sample error: " + ex.Message);
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
            if (_diskIdle != null) _diskIdle.Dispose();
        }
    }
}
