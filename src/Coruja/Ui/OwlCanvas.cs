using System;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using Coruja.Logic;

namespace Coruja.Ui
{
    /// <summary>
    /// Desenha a coruja a partir do animador e trata os cliques:
    /// olhos = acordar/dormir, orelha direita (brinco) = quando dormir,
    /// orelha esquerda = animações, barriga = carinho, arrastar = mover.
    /// </summary>
    public sealed class OwlCanvas : FrameworkElement
    {
        private readonly Animator _animator;
        private Point? _down;           // onde o botão foi apertado (coordenadas do elemento)
        private Point _downScreen;      // e na tela, para medir se virou arraste
        private bool _dragging;

        public double OwlSize { get; private set; }
        public Brush Gem { get; set; } = Paint.Rgb(0.95, 0.55, 0.10);

        public event Action? Toggled;
        public event Action? RightEar;
        public event Action? LeftEar;
        public event Action<bool>? DragChanged;
        public event Action? Moved;

        public OwlCanvas(Animator animator, double owlSize)
        {
            _animator = animator;
            SetOwlSize(owlSize);
        }

        /// <summary>Tamanho da janela para uma coruja de tamanho s (sobra espaço para sol, corações e ninho).</summary>
        public static Size FrameSize(double s) => new(Math.Round(s * 1.5), Math.Round(s * 1.45));

        public void SetOwlSize(double s)
        {
            OwlSize = s;
            var size = FrameSize(s);
            Width = size.Width;
            Height = size.Height;
            InvalidateVisual();
        }

        // Geometria

        /// <summary>Quadrado da coruja parada: centralizado, perto da base.</summary>
        public Rect BaseRect => new((Width - OwlSize) / 2, Height - OwlSize * 0.12 - OwlSize, OwlSize, OwlSize);

        /// <summary>Onde a coruja está agora (com pulinhos e deslocamentos).</summary>
        public Rect OwlRect
        {
            get
            {
                var r = BaseRect;
                r.Offset(_animator.Offset.X * OwlSize, -_animator.Offset.Y * OwlSize);
                return r;
            }
        }

        public Rect RightEarRect => OwlArt.RightEarArea(OwlRect);
        public Rect LeftEarRect => OwlArt.LeftEarArea(OwlRect);

        /// <summary>Direção do olhar para o mouse (-1..1, y para cima).</summary>
        public Vec LookAtCursor()
        {
            if (PresentationSource.FromVisual(this) == null) return Vec.Zero;
            var e = OwlArt.EyeAreas(OwlRect);
            var eye = PointToScreen(new Point((e[0].X + e[0].Width / 2 + e[1].X + e[1].Width / 2) / 2,
                                              e[0].Y + e[0].Height / 2));
            var m = System.Windows.Forms.Cursor.Position;   // pixels da tela, igual ao PointToScreen
            double dx = m.X - eye.X, dy = -(m.Y - eye.Y);
            double d = Math.Max(1, Math.Sqrt(dx * dx + dy * dy));
            double k = Math.Min(1, d / 200);
            return new Vec(dx / d * k, dy / d * k);
        }

        // Desenho

        protected override void OnRender(DrawingContext dc)
        {
            var a = _animator;
            var baseGeo = new Geo(BaseRect);
            double ppd = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            // fundo quase invisível: deixa clicar/arrastar em qualquer ponto da coruja
            dc.DrawEllipse(Paint.Rgb(0, 0, 0, 0.004), null,
                new Point(BaseRect.X + OwlSize / 2, BaseRect.Y + OwlSize / 2), OwlSize * 0.45, OwlSize * 0.5);

            // sol atrás da coruja
            if (a.Sun is { } sun) EffectsArt.DrawSun(dc, baseGeo, sun.Pos, a.SunSpin, sun.Alpha);

            // coruja com inclinação e "respiração", ancorada nos pés
            var r = OwlRect;
            double ax = r.X + r.Width / 2, ay = r.Bottom - OwlSize * 0.05;
            var transform = new TransformGroup();
            transform.Children.Add(new ScaleTransform(1 - (a.Squash - 1) * 0.5, a.Squash, ax, ay));
            transform.Children.Add(new RotateTransform(a.Tilt, ax, ay));
            dc.PushTransform(transform);
            OwlArt.Draw(dc, r, a.Pose, Gem);
            if (a.Prop == Prop.PartyHat) EffectsArt.DrawProp(dc, Prop.PartyHat, new Geo(r), a.PropPhase);
            dc.Pop();

            // objetos na frente (a tigela fica no chão; caneca e biscoito acompanham a coruja)
            if (a.Prop is { } prop && prop != Prop.PartyHat)
                EffectsArt.DrawProp(dc, prop, prop == Prop.Bowl ? baseGeo : new Geo(r), a.PropPhase);

            // ninho na frente e partículas por cima
            EffectsArt.DrawNest(dc, baseGeo, a.Nest);
            foreach (var pt in a.Particles) EffectsArt.DrawParticle(dc, pt, baseGeo, ppd);
        }

        // Mouse

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            _down = e.GetPosition(this);
            _downScreen = PointToScreen(_down.Value);
            _dragging = false;
            CaptureMouse();
            e.Handled = true;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            var pos = e.GetPosition(this);
            if (_down is not { } down || e.LeftButton != MouseButtonState.Pressed)
            {
                UpdateCursor(pos);
                return;
            }
            var screen = PointToScreen(pos);
            if (!_dragging && Math.Abs(screen.X - _downScreen.X) + Math.Abs(screen.Y - _downScreen.Y) < 4) return;
            if (!_dragging)
            {
                _dragging = true;
                DragChanged?.Invoke(true);
            }
            // move a janela mantendo o ponto agarrado embaixo do mouse
            var window = Window.GetWindow(this);
            if (window != null)
            {
                window.Left += pos.X - down.X;
                window.Top += pos.Y - down.Y;
            }
        }

        protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
        {
            ReleaseMouseCapture();
            if (_down is not { } down) return;
            _down = null;
            e.Handled = true;
            if (_dragging)
            {
                _dragging = false;
                DragChanged?.Invoke(false);
                Moved?.Invoke();
                return;
            }
            var r = OwlRect;
            if (RightEarRect.Contains(down)) RightEar?.Invoke();
            else if (LeftEarRect.Contains(down)) LeftEar?.Invoke();
            else if (OwlArt.EyeAreas(r).Any(eye => eye.Contains(down))) Toggled?.Invoke();
            else if (OwlArt.BellyArea(r).Contains(down))
            {
                _animator.Pet();
                InvalidateVisual();
            }
        }

        /// <summary>Mãozinha sobre os olhos e as orelhas.</summary>
        private void UpdateCursor(Point pos)
        {
            var r = OwlRect;
            bool hot = RightEarRect.Contains(pos) || LeftEarRect.Contains(pos) || OwlArt.EyeAreas(r).Any(eye => eye.Contains(pos));
            Cursor = hot ? Cursors.Hand : Cursors.Arrow;
        }
    }
}
