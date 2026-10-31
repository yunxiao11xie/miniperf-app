using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace MiniPerfApp
{
    // tray icon (design option C): rounded chip + big number + bottom progress bar
    public class TrayService : IDisposable
    {
        private NotifyIcon _ni;
        private Icon _icon;
        private IntPtr _hIcon = IntPtr.Zero;
        private MiniBarWindow _bar;
        private PanelWindow _panel;

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool DestroyIcon(IntPtr hIcon);

        public TrayService(MiniBarWindow bar, PanelWindow panel)
        {
            _bar = bar;
            _panel = panel;
            _ni = new NotifyIcon();
            _ni.Text = "MiniPerf";
            _ni.Visible = true;
            _ni.Icon = DrawIcon(0);

            MenuItem showItem = new MenuItem("\u663e\u793a\u9762\u677f", new EventHandler(OnShow));
            MenuItem barItem = new MenuItem("\u663e\u793a/\u9690\u85cf\u8ff7\u4f60\u6761", new EventHandler(OnToggleBar));
            MenuItem exitItem = new MenuItem("\u9000\u51fa", new EventHandler(OnExit));
            _ni.ContextMenu = new ContextMenu(new MenuItem[] { showItem, barItem, exitItem });
            _ni.DoubleClick += new EventHandler(OnShow);
        }

        private void OnShow(object sender, EventArgs e)
        {
            _bar.FocusBar();
            _bar.Show();
            if (!_panel.IsPanelVisible) _panel.ShowBelow(_bar.Left, _bar.Top + _bar.Height + 6);
            Log.Write("tray: show requested");
        }

        private void OnToggleBar(object sender, EventArgs e)
        {
            if (_bar.IsVisible) _bar.Hide(); else { _bar.Show(); _bar.FocusBar(); }
        }

        private void OnExit(object sender, EventArgs e)
        {
            Log.Write("tray exit");
            _bar.Close(); // triggers settings save + app shutdown (main window close)
        }

        public void Update(double cpuPct)
        {
            Icon old = _icon;
            IntPtr oldH = _hIcon;
            _icon = DrawIcon(cpuPct);
            _ni.Icon = _icon;
            if (old != null) old.Dispose();
            if (oldH != IntPtr.Zero) DestroyIcon(oldH);
        }

        private Icon DrawIcon(double pct)
        {
            using (Bitmap bmp = new Bitmap(16, 16))
            {
                using (Graphics g = Graphics.FromImage(bmp))
                {
                    g.SmoothingMode = SmoothingMode.AntiAlias;
                    g.Clear(Color.Transparent);

                    // rounded chip background
                    using (GraphicsPath chip = RoundedRect(0.5f, 0.5f, 15, 15, 3.5f))
                    {
                        bool hot = pct >= 85;
                        Color bg = hot ? Color.FromArgb(220, 252, 165, 165) : Color.FromArgb(220, 255, 255, 255);
                        using (SolidBrush b = new SolidBrush(bg)) g.FillPath(b, chip);
                        using (Pen p = new Pen(hot ? Color.FromArgb(220, 38, 38) : Color.FromArgb(70, 255, 255, 255), 1f)) g.DrawPath(p, chip);
                    }

                    // number
                    int v = (int)Math.Round(pct);
                    if (v > 100) v = 100;
                    string txt = v.ToString();
                    using (Font f = new Font("Segoe UI", txt.Length > 1 ? 7.5f : 8.5f, FontStyle.Bold, GraphicsUnit.Pixel))
                    using (SolidBrush tb = new SolidBrush(pct >= 85 ? Color.FromArgb(153, 27, 27) : Color.FromArgb(17, 24, 39)))
                    {
                        SizeF sz = g.MeasureString(txt, f);
                        g.DrawString(txt, f, tb, (16 - sz.Width) / 2f, (12 - sz.Height) / 2f + 0.5f);
                    }

                    // bottom progress bar
                    double frac = Math.Max(0, Math.Min(1, pct / 100.0));
                    int bw = (int)Math.Round(12 * frac);
                    if (bw < 1) bw = 1;
                    using (SolidBrush bb = new SolidBrush(pct >= 85 ? Color.FromArgb(220, 38, 38) : Color.FromArgb(37, 99, 235)))
                    {
                        g.FillRectangle(bb, 2 + (12 - bw), 13, bw, 2);
                    }
                }
                _hIcon = bmp.GetHicon();
                return Icon.FromHandle(_hIcon);
            }
        }

        private static GraphicsPath RoundedRect(float x, float y, float w, float h, float r)
        {
            GraphicsPath p = new GraphicsPath();
            p.AddArc(x, y, r * 2, r * 2, 180, 90);
            p.AddArc(x + w - r * 2, y, r * 2, r * 2, 270, 90);
            p.AddArc(x + w - r * 2, y + h - r * 2, r * 2, r * 2, 0, 90);
            p.AddArc(x, y + h - r * 2, r * 2, r * 2, 90, 90);
            p.CloseFigure();
            return p;
        }

        public void Dispose()
        {
            if (_ni != null)
            {
                _ni.Visible = false;
                _ni.Dispose();
            }
            if (_hIcon != IntPtr.Zero) DestroyIcon(_hIcon);
        }
    }
}
