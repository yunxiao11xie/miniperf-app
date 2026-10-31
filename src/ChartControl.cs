using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;

namespace MiniPerfApp
{
    // rolling 60-point series chart (code-only, no XAML)
    public class ChartControl : Canvas
    {
        private Queue<double>[] _series;
        private SolidColorBrush[] _colors;
        private string[] _names;
        private const int N = 60;
        public double HeightPx = 130;

        public ChartControl(string[] names, SolidColorBrush[] colors)
        {
            ClipToBounds = true;
            Reset(names, colors);
        }

        public void Reset(string[] names, SolidColorBrush[] colors)
        {
            _names = names;
            _colors = colors;
            _series = new Queue<double>[names.Length];
            for (int i = 0; i < names.Length; i++) _series[i] = new Queue<double>();
            Redraw();
        }

        public void Push(double[] values)
        {
            for (int i = 0; i < _series.Length; i++)
            {
                _series[i].Enqueue(values[i]);
                while (_series[i].Count > N) _series[i].Dequeue();
            }
            Redraw();
        }

        protected void Redraw()
        {
            Children.Clear();
            double w = ActualWidth; double h = ActualHeight;
            if (w < 10 || h < 10) return;

            // horizontal grid lines
            for (int g = 1; g <= 3; g++)
            {
                double y = h * g / 4.0;
                Line gl = new Line();
                gl.X1 = 0; gl.X2 = w; gl.Y1 = y; gl.Y2 = y;
                gl.Stroke = new SolidColorBrush(Color.FromArgb(0x14, 0x00, 0x00, 0x00));
                gl.StrokeThickness = 1;
                Children.Add(gl);
            }

            for (int s = 0; s < _series.Length; s++)
            {
                Queue<double> q = _series[s];
                if (q.Count < 2) continue;
                double step = w / (N - 1);
                double x0 = w - (q.Count - 1) * step;

                PointCollection pts = new PointCollection();
                double idx = 0;
                foreach (double v in q)
                {
                    double vv = v; if (vv < 0) vv = 0; if (vv > 100) vv = 100;
                    pts.Add(new Point(x0 + idx * step, h - 2 - (h - 6) * vv / 100.0));
                    idx += 1;
                }

                // area fill
                PointCollection areaPts = new PointCollection(pts);
                areaPts.Add(new Point(pts[pts.Count - 1].X, h));
                areaPts.Add(new Point(pts[0].X, h));
                Polygon area = new Polygon();
                foreach (Point pt in areaPts) area.Points.Add(pt);
                Color c = _colors[s].Color;
                area.Fill = new SolidColorBrush(Color.FromArgb(0x33, c.R, c.G, c.B));
                Children.Add(area);

                Polyline line = new Polyline();
                foreach (Point pt in pts) line.Points.Add(pt);
                line.Stroke = _colors[s];
                line.StrokeThickness = 1.6;
                line.StrokeLineJoin = PenLineJoin.Round;
                Children.Add(line);
            }
        }

        protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
        {
            base.OnRenderSizeChanged(sizeInfo);
            Redraw();
        }
    }
}
