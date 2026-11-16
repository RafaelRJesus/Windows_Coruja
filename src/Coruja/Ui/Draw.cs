using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Coruja.Logic;

namespace Coruja.Ui
{
    /// <summary>
    /// Converte "unidades da coruja" (0..1, y para cima, iguais às das versões macOS e Linux)
    /// em pixels do WPF (y para baixo).
    /// </summary>
    public readonly struct Geo
    {
        public readonly double S;
        public readonly double Left;
        public readonly double Top;

        public Geo(Rect r)
        {
            S = Math.Min(r.Width, r.Height);
            Left = r.X + r.Width / 2 - S / 2;
            Top = r.Y + r.Height / 2 - S / 2;
        }

        public Point P(double x, double y) => new(Left + x * S, Top + (1 - y) * S);

        public Rect R(double x, double y, double w, double h) => new(Left + x * S, Top + (1 - y - h) * S, w * S, h * S);
    }

    /// <summary>Pincéis, canetas e formas usados no desenho.</summary>
    public static class Paint
    {
        public static SolidColorBrush Rgb(double r, double g, double b, double a = 1)
        {
            var brush = new SolidColorBrush(Color.FromArgb(Byte(a), Byte(r), Byte(g), Byte(b)));
            brush.Freeze();
            return brush;
        }

        private static byte Byte(double v) => (byte)Math.Round(Math.Clamp(v, 0, 1) * 255);

        public static readonly SolidColorBrush Body = Rgb(0.48, 0.33, 0.22);
        public static readonly SolidColorBrush Dark = Rgb(0.33, 0.21, 0.13);
        public static readonly SolidColorBrush DarkSoft = Rgb(0.33, 0.21, 0.13, 0.45);
        public static readonly SolidColorBrush Belly = Rgb(0.88, 0.76, 0.58);
        public static readonly SolidColorBrush Mask = Rgb(0.93, 0.85, 0.70);
        public static readonly SolidColorBrush Beak = Rgb(0.96, 0.64, 0.16);
        public static readonly SolidColorBrush Iris = Rgb(0.99, 0.76, 0.10);
        public static readonly SolidColorBrush Gold = Rgb(0.95, 0.78, 0.25);
        public static readonly SolidColorBrush White = Rgb(1, 1, 1);
        public static readonly SolidColorBrush Black = Rgb(0, 0, 0);

        public static Pen Stroke(Brush brush, double width) => new(brush, width)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
            LineJoin = PenLineJoin.Round,
        };

        public static void Oval(DrawingContext dc, Rect r, Brush brush) =>
            dc.DrawEllipse(brush, null, new Point(r.X + r.Width / 2, r.Y + r.Height / 2), r.Width / 2, r.Height / 2);

        public static StreamGeometry Polygon(params Point[] pts)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(pts[0], true, true);
                for (int i = 1; i < pts.Length; i++) c.LineTo(pts[i], true, false);
            }
            g.Freeze();
            return g;
        }

        /// <summary>Curva aberta (para contornos).</summary>
        public static StreamGeometry Curve(Point start, Point c1, Point c2, Point end)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(start, false, false);
                c.BezierTo(c1, c2, end, true, false);
            }
            g.Freeze();
            return g;
        }

        /// <summary>Forma fechada: uma curva e o fechamento em linha reta.</summary>
        public static StreamGeometry ClosedCurve(Point start, Point c1, Point c2, Point end)
        {
            var g = new StreamGeometry();
            using (var c = g.Open())
            {
                c.BeginFigure(start, true, true);
                c.BezierTo(c1, c2, end, true, false);
            }
            g.Freeze();
            return g;
        }

        public static Brush Hsv(double h, double s, double v)
        {
            double r = v, g = v, b = v;
            if (s > 0)
            {
                double hh = (h % 1) * 6;
                int i = (int)Math.Floor(hh);
                double f = hh - i, p = v * (1 - s), q = v * (1 - s * f), t = v * (1 - s * (1 - f));
                (r, g, b) = i switch
                {
                    0 => (v, t, p),
                    1 => (q, v, p),
                    2 => (p, v, t),
                    3 => (p, q, v),
                    4 => (t, p, v),
                    _ => (v, p, q),
                };
            }
            return Rgb(r, g, b);
        }
    }

    /// <summary>Desenho da coruja.</summary>
    public static class OwlArt
    {
        // Áreas clicáveis

        public static Rect[] EyeAreas(Rect r)
        {
            var g = new Geo(r);
            return new[] { g.R(0.21, 0.45, 0.30, 0.30), g.R(0.49, 0.45, 0.30, 0.30) };
        }

        /// <summary>Orelha direita (com brinco): quando dormir.</summary>
        public static Rect RightEarArea(Rect r)
        {
            var g = new Geo(r);
            return Rect.Union(g.R(0.56, 0.76, 0.28, 0.19), g.R(0.75, 0.70, 0.11, 0.14));
        }

        /// <summary>Orelha esquerda (sem brinco): animações.</summary>
        public static Rect LeftEarArea(Rect r) => new Geo(r).R(0.16, 0.76, 0.28, 0.19);

        /// <summary>Barriga: carinho.</summary>
        public static Rect BellyArea(Rect r) => new Geo(r).R(0.32, 0.10, 0.36, 0.30);

        // Coruja completa

        public static void Draw(DrawingContext dc, Rect rect, Pose pose, Brush gem)
        {
            var g = new Geo(rect);

            // tufos das orelhas
            dc.DrawGeometry(Paint.Dark, null, Paint.Polygon(g.P(0.24, 0.68), g.P(0.19, 0.93), g.P(0.42, 0.80)));
            dc.DrawGeometry(Paint.Dark, null, Paint.Polygon(g.P(0.76, 0.68), g.P(0.81, 0.93), g.P(0.58, 0.80)));

            // asas
            Wing(dc, g, left: true, lift: pose.Wing);
            Wing(dc, g, left: false, lift: pose.Wing);

            // corpo, barriga e penas
            Paint.Oval(dc, g.R(0.18, 0.07, 0.64, 0.80), Paint.Body);
            Paint.Oval(dc, g.R(0.30, 0.09, 0.40, 0.42), Paint.Belly);
            var feather = Paint.Stroke(Paint.DarkSoft, 0.012 * g.S);
            foreach (var (x, y) in new[] { (0.42, 0.33), (0.58, 0.33), (0.50, 0.25), (0.42, 0.17), (0.58, 0.17) })
            {
                dc.DrawLine(feather, g.P(x - 0.03, y + 0.02), g.P(x, y));
                dc.DrawLine(feather, g.P(x, y), g.P(x + 0.03, y + 0.02));
            }

            // pés (um sobe a cada passinho)
            Paint.Oval(dc, g.R(0.36, 0.045 + Math.Max(0, pose.Step) * 0.025, 0.11, 0.05), Paint.Beak);
            Paint.Oval(dc, g.R(0.53, 0.045 + Math.Max(0, -pose.Step) * 0.025, 0.11, 0.05), Paint.Beak);

            // olhos
            var eyes = EyeAreas(rect);
            foreach (var e in eyes) Paint.Oval(dc, e, Paint.Mask);
            foreach (var e in eyes)
            {
                var inner = e;
                inner.Inflate(-0.03 * g.S, -0.03 * g.S);
                Eye(dc, inner, g, pose);
            }

            Beak(dc, g, pose.BeakOpen);
            Earring(dc, g, gem);
        }

        private static void Wing(DrawingContext dc, Geo g, bool left, double lift)
        {
            var rect = left ? g.R(0.12, 0.18, 0.20, 0.42) : g.R(0.68, 0.18, 0.20, 0.42);
            var pivot = left ? g.P(0.25, 0.55) : g.P(0.75, 0.55);
            var wing = new EllipseGeometry(rect);
            if (lift > 0.01)
            {
                // no WPF, ângulo positivo gira no sentido horário da tela
                wing.Transform = new RotateTransform((left ? 1 : -1) * lift * 60, pivot.X, pivot.Y);
            }
            dc.DrawGeometry(Paint.Dark, null, wing);
        }

        private static void Eye(DrawingContext dc, Rect inner, Geo g, Pose pose)
        {
            double w = inner.Width, h = inner.Height;

            // olhinhos de alegria: ^^
            if (pose.Happy)
            {
                Paint.Oval(dc, inner, Paint.Body);
                double y = inner.Y + h / 2 + h * 0.08;
                dc.DrawGeometry(null, Paint.Stroke(Paint.Dark, 0.022 * g.S), Paint.Curve(
                    new Point(inner.Left + w * 0.18, y),
                    new Point(inner.Left + w * 0.30, y - h * 0.38),
                    new Point(inner.Right - w * 0.30, y - h * 0.38),
                    new Point(inner.Right - w * 0.18, y)));
                return;
            }

            // olho fechado: curvinha para baixo
            if (pose.Lid >= 0.97)
            {
                Paint.Oval(dc, inner, Paint.Body);
                double y = inner.Y + h / 2;
                dc.DrawGeometry(null, Paint.Stroke(Paint.Dark, 0.02 * g.S), Paint.Curve(
                    new Point(inner.Left + w * 0.12, y),
                    new Point(inner.Left + w * 0.30, y + h * 0.30),
                    new Point(inner.Right - w * 0.30, y + h * 0.30),
                    new Point(inner.Right - w * 0.12, y)));
                return;
            }

            // olho aberto (íris e pupila maiores na carinha de filhote)
            Paint.Oval(dc, inner, Paint.White);
            double ins = 0.18 - pose.Puppy * 0.07;
            double shift = w * 0.11;
            var iris = new Rect(inner.X + w * ins + pose.Look.X * shift, inner.Y + h * ins - pose.Look.Y * shift,
                                w * (1 - 2 * ins), h * (1 - 2 * ins));
            Paint.Oval(dc, iris, Paint.Iris);

            double pin = 0.25 - pose.Puppy * 0.13;
            var pupil = new Rect(iris.X + iris.Width * pin, iris.Y + iris.Height * pin,
                                 iris.Width * (1 - 2 * pin), iris.Height * (1 - 2 * pin));
            Paint.Oval(dc, pupil, Paint.Black);

            double hs = pupil.Width * (0.35 + pose.Puppy * 0.1);
            Paint.Oval(dc, new Rect(pupil.X + pupil.Width / 2, pupil.Y + pupil.Height / 2 - hs, hs, hs), Paint.White);
            if (pose.Puppy > 0.2)
            {
                double hs2 = pupil.Width * 0.18;
                Paint.Oval(dc, new Rect(pupil.X + pupil.Width * 0.2, pupil.Bottom - pupil.Height * 0.22 - hs2, hs2, hs2),
                           Paint.Rgb(1, 1, 1, pose.Puppy));
            }

            // pálpebra descendo de cima
            if (pose.Lid > 0.01)
            {
                dc.PushClip(new EllipseGeometry(inner));
                double lidH = h * pose.Lid;
                dc.DrawRectangle(Paint.Body, null, new Rect(inner.X, inner.Y, w, lidH));
                dc.DrawLine(new Pen(Paint.Dark, 0.014 * g.S), new Point(inner.Left, inner.Y + lidH),
                            new Point(inner.Right, inner.Y + lidH));
                dc.Pop();
            }
        }

        private static void Beak(DrawingContext dc, Geo g, double open)
        {
            if (open <= 0.05)
            {
                dc.DrawGeometry(Paint.Beak, null, Paint.Polygon(g.P(0.46, 0.47), g.P(0.54, 0.47), g.P(0.50, 0.37)));
                return;
            }
            double gap = open * 0.05;
            Paint.Oval(dc, g.R(0.465, 0.375 - gap, 0.07, 0.05 + gap), Paint.Dark);
            dc.DrawGeometry(Paint.Beak, null, Paint.Polygon(g.P(0.46, 0.47), g.P(0.54, 0.47), g.P(0.50, 0.40)));
            dc.DrawGeometry(Paint.Beak, null,
                Paint.Polygon(g.P(0.475, 0.395 - gap), g.P(0.525, 0.395 - gap), g.P(0.50, 0.36 - gap)));
        }

        /// <summary>Argolinha dourada com uma pedra redonda. A cor da pedra mostra a configuração ativa.</summary>
        private static void Earring(DrawingContext dc, Geo g, Brush gem)
        {
            var ring = g.R(0.765, 0.785, 0.045, 0.045);
            dc.DrawEllipse(null, new Pen(Paint.Gold, 0.012 * g.S),
                           new Point(ring.X + ring.Width / 2, ring.Y + ring.Height / 2), ring.Width / 2, ring.Height / 2);
            var ball = g.R(0.762, 0.715, 0.06, 0.06);
            var halo = ball;
            halo.Inflate(0.008 * g.S, 0.008 * g.S);
            Paint.Oval(dc, halo, Paint.Gold);
            Paint.Oval(dc, ball, gem);
            double bw = ball.Width;
            Paint.Oval(dc, new Rect(ball.X + bw * 0.22, ball.Y + bw * 0.17, bw * 0.28, bw * 0.28), Paint.Rgb(1, 1, 1, 0.8));
        }
    }

    /// <summary>Efeitos em volta da coruja: partículas, sol, ninho e objetos.</summary>
    public static class EffectsArt
    {
        private static readonly Brush Pink = Paint.Rgb(0.98, 0.42, 0.58);
        private static readonly Brush NestBrush = Paint.Rgb(0.55, 0.40, 0.22);
        private static readonly Brush NestDark = Paint.Rgb(0.40, 0.28, 0.14);

        private static readonly Typeface Bold = new(new FontFamily("Segoe UI Symbol"), FontStyles.Normal,
                                                    FontWeights.Bold, FontStretches.Normal);

        public static void DrawParticle(DrawingContext dc, Particle pt, Geo g, double pixelsPerDip)
        {
            double fade = Math.Max(0, 1 - pt.Age / pt.Life);
            if (fade <= 0.01) return;
            var c = g.P(pt.X, pt.Y);
            double s = pt.Size * g.S;
            dc.PushOpacity(fade);
            switch (pt.Kind)
            {
                case ParticleKind.Heart:
                    dc.DrawGeometry(Pink, null, Heart(c, s));
                    break;
                case ParticleKind.Zzz:
                    Text(dc, "z", c, s, Paint.Rgb(0.95, 0.95, 0.95), pixelsPerDip);
                    break;
                case ParticleKind.Note:
                    Text(dc, "♪", c, s, Paint.Rgb(0.55, 0.75, 1), pixelsPerDip);
                    break;
                case ParticleKind.Sparkle:
                    dc.DrawGeometry(Paint.Rgb(1, 0.95, 0.6), null, Star(c, s));
                    break;
                case ParticleKind.Steam:
                    dc.DrawEllipse(Paint.Rgb(1, 1, 1, 0.6), null, c, s / 2, s / 2);
                    break;
                case ParticleKind.Crumb:
                    dc.DrawEllipse(Paint.Rgb(0.85, 0.65, 0.35), null, c, s / 2, s / 2);
                    break;
                case ParticleKind.Confetti:
                    dc.DrawRectangle(Paint.Hsv(pt.Hue, 0.7, 1), null, new Rect(c.X, c.Y - s / 2, s, s / 2));
                    break;
            }
            dc.Pop();
        }

        private static void Text(DrawingContext dc, string text, Point c, double size, Brush brush, double pixelsPerDip)
        {
            var ft = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Bold,
                                       Math.Max(6, size), brush, pixelsPerDip);
            dc.DrawText(ft, new Point(c.X - ft.Width / 2, c.Y - ft.Height / 2));
        }

        private static Geometry Heart(Point c, double s)
        {
            Point Q(double dx, double dy) => new(c.X + dx * s, c.Y - dy * s);   // y para cima, em relação ao centro
            var geo = new StreamGeometry();
            using (var ctx = geo.Open())
            {
                ctx.BeginFigure(Q(0, -0.5), true, true);
                ctx.BezierTo(Q(-0.15, -0.25), Q(-0.5, -0.1), Q(-0.5, 0.15), true, false);
                ctx.BezierTo(Q(-0.5, 0.5), Q(-0.05, 0.5), Q(0, 0.2), true, false);
                ctx.BezierTo(Q(0.05, 0.5), Q(0.5, 0.5), Q(0.5, 0.15), true, false);
                ctx.BezierTo(Q(0.5, -0.1), Q(0.15, -0.25), Q(0, -0.5), true, false);
            }
            geo.Freeze();
            return geo;
        }

        private static Geometry Star(Point c, double s)
        {
            double r = s / 2, k = r * 0.3;
            return Paint.Polygon(
                new Point(c.X, c.Y - r), new Point(c.X + k, c.Y - k), new Point(c.X + r, c.Y), new Point(c.X + k, c.Y + k),
                new Point(c.X, c.Y + r), new Point(c.X - k, c.Y + k), new Point(c.X - r, c.Y), new Point(c.X - k, c.Y - k));
        }

        /// <summary>Sol do lado esquerdo da coruja: 0 = no horizonte, 1 = alto.</summary>
        public static void DrawSun(DrawingContext dc, Geo g, double progress, double spin, double alpha)
        {
            if (alpha <= 0.01) return;
            double p = Math.Clamp(progress, 0, 1);
            var center = g.P(0.04, 0.10 + 0.72 * p);
            double r = 0.11 * g.S;
            var color = Paint.Rgb(1, 0.55 + 0.35 * p, 0.15 + 0.2 * p);
            dc.PushOpacity(alpha);
            dc.DrawEllipse(Paint.Rgb(1, 0.55 + 0.35 * p, 0.15 + 0.2 * p, 0.35 * p + 0.15), null, center, r * 1.7, r * 1.7);
            var rayPen = Paint.Stroke(color, 0.02 * g.S);
            for (int i = 0; i < 8; i++)
            {
                double a = i / 8.0 * 2 * Math.PI + spin;
                dc.DrawLine(rayPen,
                    new Point(center.X + Math.Cos(a) * r * 1.3, center.Y + Math.Sin(a) * r * 1.3),
                    new Point(center.X + Math.Cos(a) * r * 1.7, center.Y + Math.Sin(a) * r * 1.7));
            }
            dc.DrawEllipse(color, null, center, r, r);
            dc.Pop();
        }

        /// <summary>Ninho na frente da coruja, cobrindo pés e barriga; sobe de baixo ao aparecer.</summary>
        public static void DrawNest(DrawingContext dc, Geo g, double amount)
        {
            if (amount <= 0.01) return;
            double dy = (1 - amount) * 0.25;
            dc.PushOpacity(Math.Min(1, amount));
            dc.DrawGeometry(NestBrush, null,
                Paint.ClosedCurve(g.P(0.08, 0.20 - dy), g.P(0.10, -0.08 - dy), g.P(0.90, -0.08 - dy), g.P(0.92, 0.20 - dy)));
            Paint.Oval(dc, g.R(0.07, 0.15 - dy, 0.86, 0.09), NestDark);
            var twig = Paint.Stroke(NestDark, 0.012 * g.S);
            foreach (var (x1, y1, x2, y2) in new[]
                     {
                         (0.12, 0.14, 0.40, 0.04), (0.30, 0.17, 0.62, 0.06), (0.55, 0.15, 0.88, 0.12),
                         (0.20, 0.06, 0.48, 0.13), (0.50, 0.03, 0.80, 0.14), (0.05, 0.19, 0.22, 0.22),
                         (0.78, 0.22, 0.95, 0.18),
                     })
            {
                dc.DrawLine(twig, g.P(x1, y1 - dy), g.P(x2, y2 - dy));
            }
            dc.Pop();
        }

        public static void DrawProp(DrawingContext dc, Prop prop, Geo g, double phase)
        {
            switch (prop)
            {
                case Logic.Prop.Mug:
                {
                    // canequinha na asa esquerda; sobe um pouco no gole
                    var cream = Paint.Rgb(0.95, 0.93, 0.88);
                    var cup = g.R(0.10, 0.22 + phase * 0.08, 0.15, 0.14);
                    dc.DrawRoundedRectangle(cream, null, cup, 0.02 * g.S, 0.02 * g.S);
                    dc.DrawRectangle(Paint.Rgb(0.45, 0.28, 0.15), null,
                        new Rect(cup.X + 0.015 * g.S, cup.Y + 0.01 * g.S, cup.Width - 0.03 * g.S, 0.02 * g.S));
                    dc.DrawEllipse(null, new Pen(cream, 0.015 * g.S),
                        new Point(cup.X - 0.015 * g.S, cup.Y + cup.Height / 2), 0.035 * g.S, 0.035 * g.S);
                    break;
                }
                case Logic.Prop.Bowl:
                    dc.DrawGeometry(Paint.Rgb(0.30, 0.55, 0.85), null,
                        Paint.ClosedCurve(g.P(0.30, 0.10), g.P(0.32, -0.02), g.P(0.68, -0.02), g.P(0.70, 0.10)));
                    Paint.Oval(dc, g.R(0.32, 0.075, 0.36, 0.05), Paint.Rgb(0.90, 0.75, 0.40));
                    break;
                case Logic.Prop.Cookie:
                {
                    // biscoito na asa direita; a mordida cresce com a fase
                    Geometry cookie = new EllipseGeometry(g.R(0.76, 0.24, 0.15, 0.15));
                    if (phase > 0.3)
                    {
                        double b = 0.07 + phase * 0.03;
                        cookie = Geometry.Combine(cookie, new EllipseGeometry(g.R(0.86, 0.33, b, b)),
                                                  GeometryCombineMode.Exclude, null);
                    }
                    dc.DrawGeometry(Paint.Rgb(0.82, 0.60, 0.32), null, cookie);
                    dc.PushClip(cookie);
                    foreach (var (x, y) in new[] { (0.80, 0.33), (0.85, 0.28), (0.79, 0.26) })
                        Paint.Oval(dc, g.R(x, y, 0.02, 0.02), Paint.Rgb(0.35, 0.20, 0.10));
                    dc.Pop();
                    break;
                }
                case Logic.Prop.PartyHat:
                    dc.DrawGeometry(Paint.Rgb(0.55, 0.35, 0.95), null,
                        Paint.Polygon(g.P(0.40, 0.83), g.P(0.60, 0.83), g.P(0.52, 1.05)));
                    Paint.Oval(dc, g.R(0.495, 1.02, 0.05, 0.05), Paint.Rgb(1, 0.85, 0.3));
                    break;
            }
        }
    }
}
