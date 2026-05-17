using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Fluxo.Controls
{
    /// <summary>
    /// A lightweight FrameworkElement that renders an animated rising-particle
    /// background matching the Fluxo installer aesthetic.
    ///
    /// Usage in XAML:
    ///   xmlns:controls="clr-namespace:Fluxo.Controls"
    ///
    ///   <Border Background="#1A1D26">
    ///       <controls:RisingFlowBackground />
    ///   </Border>
    ///
    /// The control hooks into CompositionTarget.Rendering so it runs at the
    /// display refresh rate with no Timer overhead. Attach/detach is handled
    /// automatically via Loaded / Unloaded.
    /// </summary>
    public class RisingFlowBackground : FrameworkElement
    {
        // ── Configuration ────────────────────────────────────────────────────

        private const int    ParticleCount = 60;
        private const int    TrailCount    = 4;
        private const double TrailAlpha    = 0.12; // max opacity of upward trail lines
        private static readonly Color AccentColor = Color.FromRgb(0x1A, 0xE5, 0xA0);

        // ── State ─────────────────────────────────────────────────────────────

        private readonly List<Particle> _particles = new(ParticleCount);
        private readonly Pen[]          _trailPens = new Pen[TrailCount];
        private readonly Random         _rng       = new();
        private TimeSpan                _lastRender;

        // ── Dependency Properties ─────────────────────────────────────────────

        public static readonly DependencyProperty AccentBrushProperty =
            DependencyProperty.Register(
                nameof(AccentBrush),
                typeof(Color),
                typeof(RisingFlowBackground),
                new FrameworkPropertyMetadata(
                    AccentColor,
                    FrameworkPropertyMetadataOptions.AffectsRender,
                    OnAccentChanged));

        /// <summary>Override the particle/trail colour. Defaults to Fluxo teal.</summary>
        public Color AccentBrush
        {
            get => (Color)GetValue(AccentBrushProperty);
            set => SetValue(AccentBrushProperty, value);
        }

        private static void OnAccentChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var ctrl = (RisingFlowBackground)d;
            ctrl.RebuildResources();
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        public RisingFlowBackground()
        {
            Loaded   += (_, _) => Attach();
            Unloaded += (_, _) => Detach();
            IsHitTestVisible = false; // purely decorative — don't block input
        }

        private void Attach()
        {
            RebuildResources();
            CompositionTarget.Rendering += OnRendering;
        }

        private void Detach()
        {
            CompositionTarget.Rendering -= OnRendering;
        }

        // ── Resource initialisation ───────────────────────────────────────────

        private void RebuildResources()
        {
            Color accent = AccentBrush;

            // Particles — brushes are frozen once and reused every frame
            _particles.Clear();
            for (int i = 0; i < ParticleCount; i++)
                _particles.Add(new Particle(_rng, accent));

            // Trail pens — gradient from transparent at bottom to faint at top
            for (int i = 0; i < TrailCount; i++)
            {
                var gradient = new LinearGradientBrush
                {
                    StartPoint = new Point(0, 1), // bottom (transparent)
                    EndPoint   = new Point(0, 0), // top (visible)
                    GradientStops =
                    {
                        new GradientStop(Color.FromArgb(0,   accent.R, accent.G, accent.B), 0.0),
                        new GradientStop(Color.FromArgb((byte)(TrailAlpha * 255),
                                                        accent.R, accent.G, accent.B), 1.0),
                    }
                };
                gradient.Freeze();

                _trailPens[i] = new Pen(gradient, 1.5);
                _trailPens[i].Freeze();
            }
        }

        // ── Render loop ───────────────────────────────────────────────────────

        private void OnRendering(object? sender, EventArgs e)
        {
            // Guard against duplicate calls within the same frame
            var args = (RenderingEventArgs)e;
            if (args.RenderingTime == _lastRender) return;
            _lastRender = args.RenderingTime;

            if (ActualWidth <= 0 || ActualHeight <= 0) return;

            // Advance simulation
            foreach (var p in _particles)
            {
                p.Y -= p.Speed;
                p.X += p.Drift;

                if (p.Y < -0.02)
                {
                    p.Y = 1.02;
                    p.X = _rng.NextDouble();
                }

                if (p.X is < 0 or > 1)
                    p.Drift = -p.Drift;
            }

            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth;
            double h = ActualHeight;

            // Particles
            foreach (var p in _particles)
            {
                dc.DrawEllipse(
                    p.Brush, null,
                    new Point(p.X * w, p.Y * h),
                    p.Size, p.Size);
            }

            // Upward trails — evenly spaced, angled slightly left to match the
            // arrow direction in the logo
            for (int i = 0; i < TrailCount; i++)
            {
                double bx = (0.15 + i * 0.25) * w;
                double by = h * 0.85;
                double ex = bx - w * 0.04;
                double ey = by - h * 0.35;

                dc.DrawLine(_trailPens[i], new Point(bx, by), new Point(ex, ey));
            }
        }

        // ── Particle data ─────────────────────────────────────────────────────

        private sealed class Particle
        {
            public double           X, Y;
            public readonly double  Size;
            public readonly double  Speed;
            public double           Drift;
            public readonly SolidColorBrush Brush;

            public Particle(Random rng, Color accent)
            {
                X     = rng.NextDouble();
                Y     = rng.NextDouble();
                Size  = 1.0 + rng.NextDouble() * 2.5;
                Speed = 0.0003 + rng.NextDouble() * 0.0008;
                Drift = (rng.NextDouble() - 0.5) * 0.0002;

                double opacity = 0.1 + rng.NextDouble() * 0.5;
                Brush = new SolidColorBrush(
                    Color.FromArgb((byte)(opacity * 255), accent.R, accent.G, accent.B));
                Brush.Freeze();
            }
        }
    }
}
