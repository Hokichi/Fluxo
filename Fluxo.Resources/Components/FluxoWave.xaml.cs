using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Fluxo.Controls
{
    /// <summary>
    /// Infinitely looping layered sine wave lines, centred on the middle of the
    /// control and oscillating both above and below that axis.
    ///
    /// XAML usage:
    ///   &lt;controls:FluxoWave Width="300" Height="120"/&gt;
    ///
    /// Each wave line is randomised on construction: amplitude, frequency,
    /// speed, phase, opacity, and thickness are all independent.
    /// </summary>
    public partial class FluxoWave : UserControl
    {
        // ── Palette ──────────────────────────────────────────────────────────────
        private static readonly string[] Colors =
        {
            "#1D9E75", "#5DCAA5", "#9FE1CB", "#0F6E56", "#E1F5EE",
        };

        private readonly List<WaveState> _waves = new();
        private readonly List<Path> _paths = new();
        private DispatcherTimer _timer;
        private double _elapsed;

        private const int WaveCount = 5;

        public FluxoWave()
        {
            InitializeComponent();
            Loaded += OnLoaded;
            Unloaded += OnUnloaded;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────────
        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            var rng = new Random();
            WaveCanvas.Children.Clear();
            _waves.Clear();
            _paths.Clear();

            for (int i = 0; i < WaveCount; i++)
            {
                var wave = new WaveState(
                    Amp1: 0.15 + rng.NextDouble() * 0.55,   // primary amplitude (fraction of half-height)
                    Amp2: 0.05 + rng.NextDouble() * 0.22,   // harmonic amplitude
                    Freq1: 0.018 + rng.NextDouble() * 0.040, // spatial frequency 1
                    Freq2: 0.030 + rng.NextDouble() * 0.055, // spatial frequency 2
                    Speed1: 0.8 + rng.NextDouble() * 2.4,    // animation speed 1 (rad/s)
                    Speed2: 0.5 + rng.NextDouble() * 1.8,    // animation speed 2 (rad/s)
                    Phase1: rng.NextDouble() * Math.PI * 2,   // random start phase
                    Phase2: rng.NextDouble() * Math.PI * 2,
                    Color: Colors[i % Colors.Length],
                    Alpha: 0.35 + rng.NextDouble() * 0.65,
                    Thickness: 1.0 + rng.NextDouble() * 1.0
                );

                _waves.Add(wave);

                var path = new Path
                {
                    Fill = Brushes.Transparent,
                    Stroke = ColorBrush(wave.Color, wave.Alpha),
                    StrokeThickness = wave.Thickness,
                    StrokeStartLineCap = PenLineCap.Round,
                    StrokeEndLineCap = PenLineCap.Round,
                    StrokeLineJoin = PenLineJoin.Round,
                };
                WaveCanvas.Children.Add(path);
                _paths.Add(path);
            }

            _timer = new DispatcherTimer(DispatcherPriority.Render)
            {
                Interval = TimeSpan.FromSeconds(1.0 / 60)
            };
            _timer.Tick += OnTick;
            _timer.Start();
        }

        private void OnUnloaded(object sender, RoutedEventArgs e) => _timer?.Stop();

        // ── Frame loop ───────────────────────────────────────────────────────────
        private void OnTick(object sender, EventArgs e)
        {
            _elapsed += 1.0 / 60;

            double w = ActualWidth > 0 ? ActualWidth : Width;
            double h = ActualHeight > 0 ? ActualHeight : Height;
            double cy = h / 2.0;

            for (int i = 0; i < _waves.Count; i++)
                _paths[i].Data = BuildLine(_waves[i], _elapsed, w, cy, h);
        }

        // ── Geometry ─────────────────────────────────────────────────────────────
        private static StreamGeometry BuildLine(
            WaveState w, double t, double canvasW, double cy, double canvasH)
        {
            // Max swing from centre — small margin so lines don't clip edges
            double halfH = canvasH * 0.5 * 0.90;

            const int step = 3;
            var geo = new StreamGeometry();

            using (var ctx = geo.Open())
            {
                bool first = true;
                for (double x = 0; x <= canvasW; x += step)
                {
                    // Two superimposed sines oscillate symmetrically above and below cy
                    double y = cy
                        + Math.Sin(x * w.Freq1 + t * w.Speed1 + w.Phase1) * (halfH * w.Amp1)
                        + Math.Sin(x * w.Freq2 + t * w.Speed2 + w.Phase2) * (halfH * w.Amp2);

                    if (first)
                    {
                        ctx.BeginFigure(new Point(x, y), isFilled: false, isClosed: false);
                        first = false;
                    }
                    else
                    {
                        ctx.LineTo(new Point(x, y), isStroked: true, isSmoothJoin: true);
                    }
                }
            }

            geo.Freeze();
            return geo;
        }

        // ── Helpers ──────────────────────────────────────────────────────────────
        private static SolidColorBrush ColorBrush(string hex, double alpha)
        {
            var c = (Color)ColorConverter.ConvertFromString(hex);
            c.A = (byte)(Math.Clamp(alpha, 0, 1) * 255);
            return new SolidColorBrush(c);
        }

        private record WaveState(
            double Amp1, double Amp2,
            double Freq1, double Freq2,
            double Speed1, double Speed2,
            double Phase1, double Phase2,
            string Color, double Alpha,
            double Thickness);
    }
}