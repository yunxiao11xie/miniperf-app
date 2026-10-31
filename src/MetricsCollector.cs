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
        public double CpuTemp = double.NaN; // NaN = unavailable
        public double CpuFreq;              // GHz
        public DateTime At;

        public MetricsSnapshot()
        {
            At = DateTime.Now;
        }
    }

    // tracks dynamic "GPU Engine" counter instances, groups by engine type.
    // instance sync is expensive (hundreds of PDH queries) -> every 5th cycle only.
    internal class GpuEngineSampler
    {
        private const string Category = "GPU Engine";
        private const string Counter = "Utilization Percentage";
        private readonly Dictionary<string, PerformanceCounter> _counters =
            new Dictionary<string, PerformanceCounter>();
        private int _cycle = 99;

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
                _cycle++;
                if (_cycle >= 5)
                {
                    _cycle = 0;
                    SyncInstances();
                }
                Dictionary<string, double> byType = new Dictionary<string, double>();
                foreach (KeyValuePair<string, PerformanceCounter> kv in _counters)
                {
                    string t = EngineTypeOf(kv.Key);
                    if (t.Length == 0) continue;
                    double v = kv.Value.NextValue();
                    if (v < 0) v = 0;
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

        private void SyncInstances()
        {
            string[] names = new PerformanceCounterCategory(Category).GetInstanceNames();
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
                if (_counters.ContainsKey(n)) continue;
                if (EngineTypeOf(n).Length == 0) continue;
                try
                {
                    PerformanceCounter pc = new PerformanceCounter(Category, Counter, n, true);
                    pc.NextValue();
                    _counters[n] = pc;
                }
                catch { }
            }
        }
    }

    public class MetricsCollector : IDisposable
    {
        private Thread _thread;
        private volatile bool _stop;
        private volatile MetricsSnapshot _latest;
        private PerformanceCounter _cpu;
        private PerformanceCounter _perf;     // % Processor Performance (freq)
        private double _baseFreqMhz = 0;      // MaxClockSpeed from WMI
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
            try
            {
                _perf = new PerformanceCounter("Processor Information", "% Processor Performance", "_Total", true);
                _perf.NextValue();
                using (System.Management.ManagementObjectSearcher ms =
                    new System.Management.ManagementObjectSearcher("SELECT MaxClockSpeed FROM Win32_Processor"))
                {
                    foreach (System.Management.ManagementBaseObject o in ms.Get())
                    {
                        _baseFreqMhz = Convert.ToDouble(o["MaxClockSpeed"]);
                        break;
                    }
                }
                Log.Write("base freq: " + _baseFreqMhz.ToString("0") + " MHz");
            }
            catch { }
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
            SampleOnce(true, double.NaN);
            double cachedTemp = double.NaN;
            int tempCycle = 99;
            Stopwatch beat = new Stopwatch();
            while (!_stop)
            {
                beat.Reset();
                beat.Start();
                if (tempCycle >= 5)
                {
                    cachedTemp = QueryCpuTemp();
                    tempCycle = 0;
                }
                tempCycle++;
                SampleOnce(false, cachedTemp);
                long elapsed = beat.ElapsedMilliseconds;
                int remain = 1000 - (int)elapsed;
                if (remain > 0)
                {
                    for (int i = 0; i < remain && !_stop; i += 100) Thread.Sleep(Math.Min(100, remain - i > 0 ? 100 : 0));
                }
            }
        }

        private static double QueryCpuTemp()
        {
            try
            {
                using (System.Management.ManagementObjectSearcher s =
                    new System.Management.ManagementObjectSearcher("root\\WMI", "SELECT * FROM MSAcpi_ThermalZoneTemperature"))
                {
                    foreach (System.Management.ManagementBaseObject o in s.Get())
                    {
                        try
                        {
                            double raw = Convert.ToDouble(o["CurrentTemperature"]);
                            double c = raw / 10.0 - 273.15;
                            if (c > 0 && c < 120) return Math.Round(c);
                        }
                        catch { }
                        break;
                    }
                }
            }
            catch { }
            return double.NaN;
        }

        private void SampleOnce(bool warmup)
        {
            SampleOnce(warmup, double.NaN);
        }

        private void SampleOnce(bool warmup, double cpuTemp)
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
                s.CpuTemp = cpuTemp;
                if (_perf != null && _baseFreqMhz > 0)
                {
                    double perf = _perf.NextValue();
                    if (perf > 0 && perf < 400)
                    {
                        s.CpuFreq = _baseFreqMhz * perf / 100.0 / 1000.0;
                    }
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
