using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace MiniPerfApp
{
    // metric segment definition (label + theme RGB per DESIGN.md)
    public class SegDef
    {
        public string Key;
        public string Label;
        public byte R, G, B;
        public SegDef(string k, string l, byte r, byte g, byte b) { Key = k; Label = l; R = r; G = g; B = b; }
        public SolidColorBrush Brush()
        {
            SolidColorBrush s = new SolidColorBrush(Color.FromRgb(R, G, B));
            s.Freeze();
            return s;
        }
    }

    public static class Palette
    {
        public static readonly SolidColorBrush Cpu = Frozen(0x25, 0x63, 0xEB);
        public static readonly SolidColorBrush Gpu = Frozen(0x7C, 0x3A, 0xED);
        public static readonly SolidColorBrush Npu = Frozen(0x0D, 0x94, 0x88);
        public static readonly SolidColorBrush Ram = Frozen(0xE1, 0x1D, 0x48);
        public static readonly SolidColorBrush Down = Frozen(0x05, 0x96, 0x69);
        public static readonly SolidColorBrush Up = Frozen(0x10, 0xB9, 0x81);
        public static readonly SolidColorBrush Warn = Frozen(0xD9, 0x77, 0x06);
        public static readonly SolidColorBrush Danger = Frozen(0xDC, 0x26, 0x26);
        public static readonly SolidColorBrush LabelGray = Frozen(0x6B, 0x72, 0x80);

        public static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            SolidColorBrush s = new SolidColorBrush(Color.FromRgb(r, g, b));
            s.Freeze();
            return s;
        }

        public static SolidColorBrush Throttled(SolidColorBrush normal, double v)
        {
            if (v >= 85) return Danger;
            if (v >= 55) return Warn;
            return normal;
        }
    }

    public class MiniBarWindow : Window
    {
        private const double W = 288, H = 34;
        private readonly MetricsCollector _collector;
        private readonly AppSettings _settings;
        private readonly TextBlock[] _values = new TextBlock[6];
        private readonly SegDef[] _segs = new SegDef[]
        {
            new SegDef("cpu", "CPU", 0x25, 0x63, 0xEB),
            new SegDef("gpu", "GPU", 0x7C, 0x3A, 0xED),
            new SegDef("npu", "NPU", 0x0D, 0x94, 0x88),
            new SegDef("ram", "\u5185\u5b58", 0xE1, 0x1D, 0x48),
            new SegDef("down", "\u7f51\u901f", 0x05, 0x96, 0x69),
            new SegDef("up", "\u6e29\u5ea6", 0xEA, 0x58, 0x0C)
        };
        private System.Windows.Threading.DispatcherTimer _timer;
        private PanelWindow _panel;
        private TrayService _tray;
        private Point _downPos;
        private bool _moved;

        public PanelWindow Panel
        {
            get { return _panel; }
        }

        public MetricsCollector Collector
        {
            get { return _collector; }
        }

        public MiniBarWindow(MetricsCollector collector, AppSettings settings)
        {
            _collector = collector;
            _settings = settings;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = settings.Topmost;
            Width = W; Height = H;
            Title = "MiniPerfApp";
            Opacity = (settings.Opacity < 0.4 || settings.Opacity > 1) ? 0.9 : settings.Opacity;

            Border card = new Border();
            card.CornerRadius = new CornerRadius(9);
            card.Background = new SolidColorBrush(Color.FromArgb(0xB8, 0xF9, 0xF9, 0xFB));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x0F, 0x00, 0x00, 0x00));
            card.BorderThickness = new Thickness(1);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 18, ShadowDepth = 3, Opacity = 0.22, Color = Colors.Black
            };
            card.Padding = new Thickness(8, 0, 8, 0);

            Grid grid = new Grid();
            for (int i = 0; i < 6; i++) grid.ColumnDefinitions.Add(new ColumnDefinition());
            for (int i = 0; i < 6; i++)
            {
                StackPanel sp = new StackPanel();
                sp.VerticalAlignment = VerticalAlignment.Center;
                TextBlock lbl = new TextBlock();
                lbl.Text = _segs[i].Label;
                lbl.FontSize = 9;
                lbl.FontWeight = FontWeights.Medium;
                lbl.Foreground = Palette.LabelGray;
                lbl.HorizontalAlignment = HorizontalAlignment.Center;
                TextBlock val = new TextBlock();
                val.Text = "--";
                val.FontSize = 12;
                val.FontWeight = FontWeights.SemiBold;
                val.Foreground = _segs[i].Brush();
                val.HorizontalAlignment = HorizontalAlignment.Center;
                sp.Children.Add(lbl);
                sp.Children.Add(val);
                Grid.SetColumn(sp, i);
                grid.Children.Add(sp);
                _values[i] = val;
            }
            card.Child = grid;
            Content = card;

            if (settings.BarX >= 0 && settings.BarY >= 0)
            {
                Left = settings.BarX; Top = settings.BarY;
            }
            else
            {
                double sw = SystemParameters.WorkArea.Width;
                Left = sw - W - 24;
                Top = SystemParameters.WorkArea.Top + 12;
            }
            EnsureOnScreen();

            MouseLeftButtonDown += new MouseButtonEventHandler(OnDragDown);
            MouseLeftButtonUp += new MouseButtonEventHandler(OnDragUp);
            MouseWheel += new MouseWheelEventHandler(OnWheel);
            Closing += new System.ComponentModel.CancelEventHandler(OnClosing);

            _panel = new PanelWindow(collector, this);
            _panel.Hide();
            _tray = new TrayService(this, _panel);

            ToolTip = "\u62d6\u52a8\u79fb\u52a8 \u00b7 \u6eda\u8f6e\u8c03\u900f\u660e\u5ea6 \u00b7 \u53f3\u952e\u83dc\u5355";
            ContextMenu = BuildMenu();

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += new EventHandler(OnTick);
            _timer.Start();
        }

        private ContextMenu BuildMenu()
        {
            ContextMenu m = new ContextMenu();
            m.FontSize = 12;
            MenuItem exit = new MenuItem();
            exit.Header = "\u9000\u51fa MiniPerf";
            exit.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e2) { Close(); });
            m.Items.Add(exit);
            return m;
        }

        private void OnDragDown(object sender, MouseButtonEventArgs e)
        {
            _downPos = e.GetPosition(this);
            _moved = false;
        }

        private void OnDragUp(object sender, MouseButtonEventArgs e)
        {
            if (!_moved)
            {
                // treat as click: toggle panel below the bar
                if (_panel.IsPanelVisible) _panel.Hide();
                else
                {
                    _panel.ShowBelow(Left, Top + Height + 6);
                    Log.Write("panel shown");
                }
            }
        }

        private void OnDrag(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); } catch { }
        }

        private void OnWheel(object sender, MouseWheelEventArgs e)
        {
            double o = Opacity + (e.Delta > 0 ? 0.04 : -0.04);
            if (o < 0.4) o = 0.4;
            if (o > 1.0) o = 1.0;
            Opacity = o;
            _settings.Opacity = o;
        }

        protected override void OnMouseMove(System.Windows.Input.MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point p = e.GetPosition(this);
                if (Math.Abs(p.X - _downPos.X) > 4 || Math.Abs(p.Y - _downPos.Y) > 4)
                {
                    _moved = true;
                    try { DragMove(); } catch { }
                }
            }
        }

        private bool _loggedOnce = false;
        private void OnTick(object sender, EventArgs e)
        {
            MetricsSnapshot s = _collector.Latest;
            if (!_loggedOnce)
            {
                _loggedOnce = true;
                Log.Write("first tick: latest=" + (s == null ? "null" : ("cpu=" + s.Cpu.ToString("0.0") + " ram=" + s.RamPct.ToString("0.0"))));
            }
            if (s == null) return;
            _values[0].Text = Format.Pct(s.Cpu);
            _values[1].Text = Format.Pct(s.Gpu);
            _values[2].Text = Format.Pct(s.Npu);
            _values[3].Text = Format.Pct(s.RamPct);
            _values[4].Text = Format.Rate(s.NetDown);
            _values[5].Text = double.IsNaN(s.CpuTemp) ? "\u2014" : ((int)s.CpuTemp).ToString() + "\u00b0";
            _values[0].Foreground = Palette.Throttled(Palette.Cpu, s.Cpu);
            _values[1].Foreground = Palette.Throttled(Palette.Gpu, s.Gpu);
            _values[2].Foreground = Palette.Throttled(Palette.Npu, s.Npu);
            _values[3].Foreground = Palette.Throttled(Palette.Ram, s.RamPct);
            if (!double.IsNaN(s.CpuTemp))
            {
                _values[5].Foreground = s.CpuTemp >= 90 ? Palette.Danger : (s.CpuTemp >= 75 ? Palette.Warn : _segs[5].Brush());
            }

            if (_tray != null) _tray.Update(s.Cpu);
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settings.BarX = Left;
            _settings.BarY = Top;
            if (_tray != null) _tray.Dispose();
        }

        private void EnsureOnScreen()
        {
            Rect wa = SystemParameters.WorkArea;
            if (Left < wa.Left) Left = wa.Left;
            if (Top < wa.Top) Top = wa.Top;
            if (Left + Width > wa.Right) Left = wa.Right - Width;
            if (Top + Height > wa.Bottom) Top = wa.Bottom - Height;
        }

        public void FocusBar()
        {
            try { Activate(); } catch { }
        }
    }

    public static class Format
    {
        public static string Pct(double v)
        {
            int i = (int)Math.Round(v);
            if (i < 0) i = 0;
            if (i > 100) i = 100;
            return i.ToString() + "%";
        }
        public static string Rate(double bytesPerSec)
        {
            if (bytesPerSec >= 1024 * 1024) return (bytesPerSec / 1024 / 1024).ToString("0.0") + "M";
            if (bytesPerSec >= 1024) return (bytesPerSec / 1024).ToString("0") + "K";
            return ((int)bytesPerSec).ToString();
        }
    }
}
