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
            new SegDef("down", "\u2193", 0x05, 0x96, 0x69),
            new SegDef("up", "\u2191", 0x10, 0xB9, 0x81)
        };
        private System.Windows.Threading.DispatcherTimer _timer;

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

            MouseLeftButtonDown += new MouseButtonEventHandler(OnDrag);
            MouseWheel += new MouseWheelEventHandler(OnWheel);
            Closing += new System.ComponentModel.CancelEventHandler(OnClosing);

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += new EventHandler(OnTick);
            _timer.Start();
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
            _values[5].Text = Format.Rate(s.NetUp);
            _values[0].Foreground = Palette.Throttled(Palette.Cpu, s.Cpu);
            _values[1].Foreground = Palette.Throttled(Palette.Gpu, s.Gpu);
            _values[2].Foreground = Palette.Throttled(Palette.Npu, s.Npu);
            _values[3].Foreground = Palette.Throttled(Palette.Ram, s.RamPct);
        }

        private void OnClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _settings.BarX = Left;
            _settings.BarY = Top;
        }

        private void EnsureOnScreen()
        {
            Rect wa = SystemParameters.WorkArea;
            if (Left < wa.Left) Left = wa.Left;
            if (Top < wa.Top) Top = wa.Top;
            if (Left + Width > wa.Right) Left = wa.Right - Width;
            if (Top + Height > wa.Bottom) Top = wa.Bottom - Height;
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
