using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MiniPerfApp
{
    // expanded detail panel; positions itself below the mini bar
    public class PanelWindow : Window
    {
        private const double W = 376, H = 560;
        private readonly MetricsCollector _collector;
        private readonly MiniBarWindow _owner;
        private System.Windows.Threading.DispatcherTimer _timer;

        private readonly TextBlock[] _cellValues = new TextBlock[8];
        private readonly string[] _cellKeys = new string[]
        {
            "CPU", "GPU", "NPU", "\u5185\u5b58",
            "\u7f51\u2193", "\u7f51\u2191", "CPU\u6e29\u5ea6", "\u78c1\u76d8"
        };
        private readonly SolidColorBrush[] _cellColors = new SolidColorBrush[]
        {
            Palette.Cpu, Palette.Gpu, Palette.Npu, Palette.Ram,
            Palette.Down, Palette.Up, Palette.LabelGray, Frozen(0xEA, 0x58, 0x0C)
        };

        private ChartControl _chart;
        private int _chartMode = 0; // 0=cpu+ram 1=gpu 2=net
        private readonly TextBlock _chartTitle;
        private int _procTab = 0; // 0=cpu 1=mem
        private readonly StackPanel _procList;
        private readonly Button _tabCpuBtn, _tabMemBtn;
        private bool _loggedOnce = false;

        private static SolidColorBrush Frozen(byte r, byte g, byte b)
        {
            SolidColorBrush s = new SolidColorBrush(Color.FromRgb(r, g, b));
            s.Freeze();
            return s;
        }

        public PanelWindow(MetricsCollector collector, MiniBarWindow owner)
        {
            _collector = collector;
            _owner = owner;

            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            ShowInTaskbar = false;
            Topmost = true;
            Width = W; Height = H;
            Title = "MiniPerfAppPanel";
            ShowActivated = false;

            Border card = new Border();
            card.CornerRadius = new CornerRadius(14);
            card.Background = new SolidColorBrush(Color.FromArgb(0xCC, 0xF3, 0xF3, 0xF7));
            card.BorderBrush = new SolidColorBrush(Color.FromArgb(0x0F, 0x00, 0x00, 0x00));
            card.BorderThickness = new Thickness(1);
            card.Effect = new System.Windows.Media.Effects.DropShadowEffect
            {
                BlurRadius = 26, ShadowDepth = 6, Opacity = 0.26, Color = Colors.Black
            };

            DockPanel root = new DockPanel();

            // ---- top bar ----
            Grid top = new Grid();
            top.Height = 44;
            ColumnDefinition c1 = new ColumnDefinition(); c1.Width = new GridLength(1, GridUnitType.Star);
            ColumnDefinition c2 = new ColumnDefinition(); c2.Width = new GridLength(1, GridUnitType.Auto);
            ColumnDefinition c3 = new ColumnDefinition(); c3.Width = new GridLength(1, GridUnitType.Auto);
            top.ColumnDefinitions.Add(c1); top.ColumnDefinitions.Add(c2); top.ColumnDefinitions.Add(c3);
            TextBlock title = new TextBlock();
            title.Text = "MiniPerf";
            title.FontSize = 13; title.FontWeight = FontWeights.Bold;
            title.Foreground = Frozen(0x11, 0x18, 0x27);
            title.VerticalAlignment = VerticalAlignment.Center;
            title.Margin = new Thickness(16, 0, 0, 0);
            Grid.SetColumn(title, 0);
            Ellipse dot = new Ellipse();
            dot.Width = 8; dot.Height = 8; dot.Fill = Palette.Cpu;
            dot.VerticalAlignment = VerticalAlignment.Center;
            Grid.SetColumn(dot, 1);
            Button collapse = new Button();
            collapse.Content = "\u2500";
            collapse.Width = 30; collapse.Height = 24;
            collapse.FontSize = 12;
            collapse.Background = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
            collapse.BorderBrush = Brushes.Transparent;
            collapse.Click += new RoutedEventHandler(delegate(object s, RoutedEventArgs e) { HidePanel(); });
            collapse.VerticalAlignment = VerticalAlignment.Center;
            collapse.Margin = new Thickness(8, 0, 12, 0);
            Grid.SetColumn(collapse, 2);
            top.Children.Add(title); top.Children.Add(dot); top.Children.Add(collapse);

            Separator sepTop = new Separator();
            sepTop.Background = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0x00, 0x00));

            // ---- grid of 8 cells ----
            UniformGrid cells = new UniformGrid();
            cells.Rows = 2; cells.Columns = 4;
            cells.Margin = new Thickness(12, 10, 12, 8);
            for (int i = 0; i < 8; i++)
            {
                Border b = new Border();
                b.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
                b.BorderBrush = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));
                b.BorderThickness = new Thickness(1);
                b.CornerRadius = new CornerRadius(10);
                b.Margin = new Thickness(4);
                b.Padding = new Thickness(10, 8, 10, 8);
                StackPanel sp = new StackPanel();
                TextBlock k = new TextBlock();
                k.Text = _cellKeys[i];
                k.FontSize = 10; k.FontWeight = FontWeights.Medium;
                k.Foreground = Palette.LabelGray;
                TextBlock v = new TextBlock();
                v.Text = "--";
                v.FontSize = 17; v.FontWeight = FontWeights.Bold;
                v.Foreground = _cellColors[i];
                v.Margin = new Thickness(0, 2, 0, 0);
                sp.Children.Add(k); sp.Children.Add(v);
                b.Child = sp;
                cells.Children.Add(b);
                _cellValues[i] = v;
            }

            // ---- chart card ----
            Border chartCard = new Border();
            chartCard.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
            chartCard.BorderBrush = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));
            chartCard.BorderThickness = new Thickness(1);
            chartCard.CornerRadius = new CornerRadius(10);
            chartCard.Margin = new Thickness(12, 0, 12, 8);
            chartCard.Padding = new Thickness(12, 10, 12, 8);
            StackPanel chartStack = new StackPanel();
            Grid chartHead = new Grid();
            _chartTitle = new TextBlock();
            _chartTitle.Text = "CPU \u00b7 \u5185\u5b58 \uff08\u70b9\u51fb\u5207\u6362\uff09";
            _chartTitle.FontSize = 11; _chartTitle.FontWeight = FontWeights.SemiBold;
            _chartTitle.Foreground = Frozen(0x37, 0x41, 0x51);
            _chartTitle.HorizontalAlignment = HorizontalAlignment.Left;
            _chartTitle.MouseLeftButtonUp += new MouseButtonEventHandler(OnChartSwitch);
            _chartTitle.Cursor = Cursors.Hand;
            chartHead.Children.Add(_chartTitle);
            _chart = new ChartControl(
                new string[] { "CPU", "\u5185\u5b58" },
                new SolidColorBrush[] { Palette.Cpu, Palette.Ram });
            _chart.Height = 130;
            _chart.MouseLeftButtonUp += new MouseButtonEventHandler(OnChartSwitch);
            _chart.Cursor = Cursors.Hand;
            chartStack.Children.Add(chartHead);
            chartStack.Children.Add(_chart);
            chartCard.Child = chartStack;

            // ---- processes ----
            Border procCard = new Border();
            procCard.Background = new SolidColorBrush(Color.FromArgb(0x8C, 0xFF, 0xFF, 0xFF));
            procCard.BorderBrush = new SolidColorBrush(Color.FromArgb(0x0A, 0x00, 0x00, 0x00));
            procCard.BorderThickness = new Thickness(1);
            procCard.CornerRadius = new CornerRadius(10);
            procCard.Margin = new Thickness(12, 0, 12, 12);
            procCard.Padding = new Thickness(10, 10, 10, 12);
            StackPanel procStack = new StackPanel();
            StackPanel tabs = new StackPanel();
            tabs.Orientation = Orientation.Horizontal;
            tabs.Margin = new Thickness(0, 0, 0, 8);
            _tabCpuBtn = MakeTab("\u6309 CPU", true);
            _tabMemBtn = MakeTab("\u6309\u5185\u5b58", false);
            _tabCpuBtn.Click += new RoutedEventHandler(delegate { SetProcTab(0); });
            _tabMemBtn.Click += new RoutedEventHandler(delegate { SetProcTab(1); });
            tabs.Children.Add(_tabCpuBtn);
            tabs.Children.Add(_tabMemBtn);
            _procList = new StackPanel();
            procStack.Children.Add(tabs);
            procStack.Children.Add(_procList);
            procCard.Child = procStack;

            DockPanel.SetDock(top, Dock.Top);
            DockPanel.SetDock(sepTop, Dock.Top);
            DockPanel.SetDock(procCard, Dock.Bottom);
            DockPanel.SetDock(chartCard, Dock.Bottom);
            DockPanel.SetDock(cells, Dock.Top);
            root.Children.Add(top);
            root.Children.Add(sepTop);
            root.Children.Add(procCard);
            root.Children.Add(chartCard);
            root.Children.Add(cells);
            card.Child = root;
            Content = card;

            Deactivated += new EventHandler(delegate(object s, EventArgs e)
            {
                // auto collapse when losing focus (but not right after our own show)
                if (_canAutoHide) HidePanel();
            });

            _timer = new System.Windows.Threading.DispatcherTimer();
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Tick += new EventHandler(OnTick);
            _timer.Start();
        }

        private bool _canAutoHide = true;

        private Button MakeTab(string text, bool on)
        {
            Button b = new Button();
            b.Content = text;
            b.FontSize = 10.5;
            b.Padding = new Thickness(12, 3, 12, 3);
            b.Margin = new Thickness(0, 0, 6, 0);
            b.BorderBrush = Brushes.Transparent;
            b.Cursor = Cursors.Hand;
            StyleTab(b, on);
            return b;
        }

        private void StyleTab(Button b, bool on)
        {
            if (on)
            {
                b.Background = Palette.Cpu;
                b.Foreground = Brushes.White;
                b.FontWeight = FontWeights.SemiBold;
            }
            else
            {
                b.Background = new SolidColorBrush(Color.FromArgb(0x0D, 0x00, 0x00, 0x00));
                b.Foreground = Palette.LabelGray;
                b.FontWeight = FontWeights.Normal;
            }
        }

        private void SetProcTab(int i)
        {
            _procTab = i;
            StyleTab(_tabCpuBtn, i == 0);
            StyleTab(_tabMemBtn, i == 1);
            RenderProcs();
        }

        private void OnChartSwitch(object sender, MouseButtonEventArgs e)
        {
            _chartMode = (_chartMode + 1) % 3;
            if (_chartMode == 0)
            {
                _chartTitle.Text = "CPU \u00b7 \u5185\u5b58\uff08\u70b9\u51fb\u5207\u6362\uff09";
                _chart.Reset(new string[] { "CPU", "\u5185\u5b58" }, new SolidColorBrush[] { Palette.Cpu, Palette.Ram });
            }
            else if (_chartMode == 1)
            {
                _chartTitle.Text = "GPU \u00b7 NPU\uff08\u70b9\u51fb\u5207\u6362\uff09";
                _chart.Reset(new string[] { "GPU", "NPU" }, new SolidColorBrush[] { Palette.Gpu, Palette.Npu });
            }
            else
            {
                _chartTitle.Text = "\u7f51\u901f \u2193/\u2191\uff08MB/s\uff0c\u70b9\u51fb\u5207\u6362\uff09";
                _chart.Reset(new string[] { "\u2193", "\u2191" }, new SolidColorBrush[] { Palette.Down, Palette.Up });
            }
        }

        private void HidePanel()
        {
            _canAutoHide = false;
            Hide();
            _owner.FocusBar();
        }

        public void ShowBelow(double x, double y)
        {
            Left = x;
            Top = y;
            Show();
            _canAutoHide = false;
            Dispatcher.BeginInvoke(new Action(delegate { _canAutoHide = true; }), System.Windows.Threading.DispatcherPriority.Background);
        }

        public bool IsPanelVisible
        {
            get { return IsVisible; }
        }

        private void OnTick(object sender, EventArgs e)
        {
            MetricsSnapshot s = _collector.Latest;
            if (s == null) return;
            _cellValues[0].Text = Format.Pct(s.Cpu);
            _cellValues[1].Text = Format.Pct(s.Gpu);
            _cellValues[2].Text = Format.Pct(s.Npu);
            _cellValues[3].Text = Format.Pct(s.RamPct);
            _cellValues[4].Text = Format.Rate(s.NetDown);
            _cellValues[5].Text = Format.Rate(s.NetUp);
            _cellValues[6].Text = "\u2014";
            _cellValues[7].Text = Format.Pct(s.DiskPct);
            _cellValues[0].Foreground = Palette.Throttled(Palette.Cpu, s.Cpu);
            _cellValues[3].Foreground = Palette.Throttled(Palette.Ram, s.RamPct);

            if (_chartMode == 0) _chart.Push(new double[] { s.Cpu, s.RamPct });
            else if (_chartMode == 1) _chart.Push(new double[] { s.Gpu, s.Npu });
            else
            {
                double d = s.NetDown / (1024.0 * 1024.0);
                double u = s.NetUp / (1024.0 * 1024.0);
                _chart.Push(new double[] { d, u });
            }

            RenderProcs();

            if (!_loggedOnce)
            {
                _loggedOnce = true;
                Log.Write("panel first tick ok");
            }
        }

        private void RenderProcs()
        {
            List<ProcInfo> list = _procTab == 0 ? TopProcesses.ByCpu : TopProcesses.ByMem;
            _procList.Children.Clear();
            foreach (ProcInfo p in list)
            {
                Grid row = new Grid();
                ColumnDefinition n1 = new ColumnDefinition(); n1.Width = new GridLength(1, GridUnitType.Star);
                ColumnDefinition n2 = new ColumnDefinition(); n2.Width = new GridLength(90);
                ColumnDefinition n3 = new ColumnDefinition(); n3.Width = new GridLength(48);
                row.ColumnDefinitions.Add(n1); row.ColumnDefinitions.Add(n2); row.ColumnDefinitions.Add(n3);
                row.Margin = new Thickness(2, 3, 2, 3);

                TextBlock name = new TextBlock();
                name.Text = p.Name + ".exe";
                name.FontSize = 11.5;
                name.Foreground = Frozen(0x1F, 0x29, 0x37);
                name.TextTrimming = TextTrimming.CharacterEllipsis;
                Grid.SetColumn(name, 0);

                Border barBg = new Border();
                barBg.Height = 5; barBg.CornerRadius = new CornerRadius(2.5);
                barBg.Background = new SolidColorBrush(Color.FromArgb(0x12, 0x00, 0x00, 0x00));
                barBg.VerticalAlignment = VerticalAlignment.Center;
                Grid.SetColumn(barBg, 1);
                Grid barOuter = new Grid();
                barOuter.ClipToBounds = true;
                Border barFill = new Border();
                barFill.Height = 5;
                double frac = _procTab == 0 ? p.Cpu / 100.0 : Math.Min(1.0, p.MemoryMb / 4096.0);
                barFill.Width = Math.Max(2, 90 * frac);
                barFill.CornerRadius = new CornerRadius(2.5);
                barFill.Background = _procTab == 0 ? Palette.Cpu : Palette.Ram;
                barFill.HorizontalAlignment = HorizontalAlignment.Left;
                barOuter.Children.Add(barFill);
                barBg.Child = barOuter;

                TextBlock val = new TextBlock();
                val.FontSize = 10.5;
                val.FontWeight = FontWeights.SemiBold;
                val.Foreground = Frozen(0x37, 0x41, 0x51);
                val.TextAlignment = TextAlignment.Right;
                if (_procTab == 0) val.Text = p.Cpu.ToString("0.0") + "%";
                else val.Text = p.MemoryMb.ToString("0") + "M";
                Grid.SetColumn(val, 2);

                row.Children.Add(name);
                row.Children.Add(barBg);
                row.Children.Add(val);
                _procList.Children.Add(row);
            }
        }

        public void FocusBar() { }
        // placeholder removed: owner focus handled by WPF activation
    }
}
