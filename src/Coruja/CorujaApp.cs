using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using Coruja.Logic;
using Coruja.Ui;

namespace Coruja
{
    /// <summary>Liga a janela, o animador, os painéis das orelhas, o KeepAwake e o ícone da bandeja.</summary>
    public sealed class CorujaApp
    {
        private readonly Application _app;
        private readonly Window _window;
        private readonly OwlCanvas _canvas;
        private readonly SleepPanel _sleepPanel;
        private readonly AnimationPanel _animationPanel;
        private readonly DispatcherTimer _frameTimer = new(DispatcherPriority.Render);
        private readonly DispatcherTimer _tickTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private readonly System.Diagnostics.Stopwatch _clock = System.Diagnostics.Stopwatch.StartNew();
        private readonly System.Windows.Forms.NotifyIcon _tray;
        private readonly MenuItem _toggleItem = new() { Header = "Acordar a coruja" };
        private readonly System.Windows.Forms.ToolStripMenuItem _trayToggle = new("Acordar a coruja");
        private double _lastFrame;
        private int _fps;
        private DateTime? _deadline;

        public Settings Settings { get; } = new();
        public Animator Animator { get; }
        public KeepAwake Keeper { get; } = new();

        public CorujaApp(Application app)
        {
            _app = app;
            Animator = new Animator(Settings);
            _canvas = new OwlCanvas(Animator, Settings.Size.Points());

            _window = new Window
            {
                Title = "Coruja",
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false,
                Topmost = Settings.Data.AlwaysOnTop,
                SizeToContent = SizeToContent.WidthAndHeight,
                Content = _canvas,
            };

            _sleepPanel = new SleepPanel(_canvas, this);
            _animationPanel = new AnimationPanel(_canvas, this);

            _canvas.Toggled += Toggle;
            _canvas.RightEar += () => ShowPanel(_sleepPanel, _canvas.RightEarRect);
            _canvas.LeftEar += () => ShowPanel(_animationPanel, _canvas.LeftEarRect);
            _canvas.DragChanged += dragging =>
            {
                Animator.Dragging = dragging;
                if (Settings.Data.AnimationsOn) SetFps(Animator.DesiredFps);
            };
            _canvas.Moved += SavePosition;
            _canvas.ContextMenu = BuildContextMenu();

            Animator.CanWalk = () => !_sleepPanel.IsOpen && !_animationPanel.IsOpen;
            Animator.MoveWindow = MoveWindow;

            _tray = BuildTray();
            UpdateGem();

            _frameTimer.Tick += (_, _) => Frame();
            _tickTimer.Tick += (_, _) => Tick();
            _app.Exit += (_, _) =>
            {
                Keeper.Stop();
                _tray.Visible = false;
                _tray.Dispose();
            };
        }

        public void Start()
        {
            PlaceWindow();
            _window.Show();
            Animator.StaticFrame();
            StartAnimating();
            UpdateStatus();
        }

        // Menus

        private ContextMenu BuildContextMenu()
        {
            var menu = new ContextMenu();
            _toggleItem.Click += (_, _) => Toggle();
            menu.Items.Add(_toggleItem);
            menu.Items.Add(Item("Quando dormir…", () => ShowPanel(_sleepPanel, _canvas.RightEarRect)));
            menu.Items.Add(Item("Animações…", () => ShowPanel(_animationPanel, _canvas.LeftEarRect)));
            var onTop = new MenuItem { Header = "Sempre na frente", IsCheckable = true, IsChecked = Settings.Data.AlwaysOnTop };
            onTop.Click += (_, _) => SetOnTop(onTop.IsChecked);
            menu.Items.Add(onTop);
            menu.Items.Add(new Separator());
            menu.Items.Add(Item("Sair", () => _app.Shutdown()));
            return menu;
        }

        private static MenuItem Item(string text, Action action)
        {
            var item = new MenuItem { Header = text };
            item.Click += (_, _) => action();
            return item;
        }

        private System.Windows.Forms.NotifyIcon BuildTray()
        {
            var menu = new System.Windows.Forms.ContextMenuStrip();
            _trayToggle.Click += (_, _) => Toggle();
            menu.Items.Add(_trayToggle);
            menu.Items.Add("Quando dormir…", null, (_, _) => ShowPanel(_sleepPanel, _canvas.RightEarRect));
            menu.Items.Add("Animações…", null, (_, _) => ShowPanel(_animationPanel, _canvas.LeftEarRect));
            menu.Items.Add(new System.Windows.Forms.ToolStripSeparator());
            menu.Items.Add("Sair", null, (_, _) => _app.Shutdown());

            var tray = new System.Windows.Forms.NotifyIcon
            {
                Text = "Coruja",
                ContextMenuStrip = menu,
                Visible = true,
            };
            try
            {
                tray.Icon = System.Drawing.Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? "");
            }
            catch (Exception)
            {
                tray.Icon = System.Drawing.SystemIcons.Application;
            }
            tray.MouseClick += (_, e) =>
            {
                if (e.Button == System.Windows.Forms.MouseButtons.Left) Toggle();
            };
            return tray;
        }

        // Janela

        private void PlaceWindow()
        {
            var area = SystemParameters.WorkArea;
            var size = OwlCanvas.FrameSize(Settings.Size.Points());
            double left = Settings.Data.Left ?? area.Right - size.Width - 30;
            double top = Settings.Data.Top ?? area.Bottom - size.Height - 10;
            // se a posição salva ficou fora da tela (monitor desconectado), volta para o canto
            if (left < SystemParameters.VirtualScreenLeft || top < SystemParameters.VirtualScreenTop ||
                left > SystemParameters.VirtualScreenLeft + SystemParameters.VirtualScreenWidth - 20 ||
                top > SystemParameters.VirtualScreenTop + SystemParameters.VirtualScreenHeight - 20)
            {
                left = area.Right - size.Width - 30;
                top = area.Bottom - size.Height - 10;
            }
            _window.Left = left;
            _window.Top = top;
        }

        private void SavePosition()
        {
            Settings.Data.Left = _window.Left;
            Settings.Data.Top = _window.Top;
            Settings.Save();
        }

        /// <summary>Passeio: move a janela de lado sem sair da área de trabalho.</summary>
        private bool MoveWindow(double dx)
        {
            var area = SystemParameters.WorkArea;
            double wanted = _window.Left + dx;
            double x = Math.Max(area.Left, Math.Min(wanted, area.Right - _window.ActualWidth));
            _window.Left = x;
            if (Math.Abs(x - wanted) > 0.01)
            {
                SavePosition();
                return false;
            }
            return true;
        }

        private void SetOnTop(bool onTop)
        {
            Settings.Data.AlwaysOnTop = onTop;
            Settings.Save();
            _window.Topmost = onTop;
        }

        private void ShowPanel(EarPanel panel, Rect ear)
        {
            foreach (var other in new EarPanel[] { _sleepPanel, _animationPanel })
                if (other != panel) other.Close();
            panel.Toggle(ear);
        }

        public void UpdateGem()
        {
            var (r, g, b) = Settings.GemColor(Settings.Mode);
            _canvas.Gem = Paint.Rgb(r, g, b);
            _canvas.InvalidateVisual();
        }

        public void ApplySize()
        {
            double centerX = _window.Left + _window.ActualWidth / 2;
            double bottom = _window.Top + _window.ActualHeight;
            _canvas.SetOwlSize(Settings.Size.Points());
            var size = OwlCanvas.FrameSize(Settings.Size.Points());
            _window.Left = centerX - size.Width / 2;
            _window.Top = bottom - size.Height;
            SavePosition();
        }

        // Animação (a taxa de quadros se adapta ao que está acontecendo)

        private void StartAnimating()
        {
            if (!Settings.Data.AnimationsOn)
            {
                StopAnimating();
                return;
            }
            SetFps(Animator.DesiredFps);
        }

        private void StopAnimating()
        {
            _frameTimer.Stop();
            _fps = 0;
            Animator.StaticFrame();
            _canvas.InvalidateVisual();
        }

        private void SetFps(int fps)
        {
            if (fps == _fps && _frameTimer.IsEnabled) return;
            _fps = fps;
            _frameTimer.Interval = TimeSpan.FromMilliseconds(1000.0 / fps);
            _lastFrame = _clock.Elapsed.TotalSeconds;
            _frameTimer.Start();
        }

        private void Frame()
        {
            double now = _clock.Elapsed.TotalSeconds;
            double dt = Math.Min(0.1, now - _lastFrame);
            _lastFrame = now;
            // minimizada ou com a sessão bloqueada não há o que desenhar
            if (!_window.IsVisible || _window.WindowState == WindowState.Minimized) return;
            Animator.MouseLook = _canvas.LookAtCursor();
            Animator.Update(dt);
            _canvas.InvalidateVisual();
            if (Animator.DesiredFps != _fps) SetFps(Animator.DesiredFps);
        }

        public void ApplyAnimationSettings()
        {
            if (Settings.Data.AnimationsOn) StartAnimating();
            else StopAnimating();
        }

        public void PlayNow(Activity activity)
        {
            if (!Settings.Data.AnimationsOn) return;
            Animator.Play(activity);
            StartAnimating();
        }

        // Acordar e dormir

        public void Toggle()
        {
            if (Keeper.Active) Sleep();
            else Wake();
        }

        /// <summary>Abre os olhos e, se houver limite configurado, começa a contar.</summary>
        public void Wake()
        {
            if (!Keeper.Active)
            {
                Keeper.Start();
                SetToggleText("Pôr a coruja para dormir");
                if (Settings.Data.AnimationsOn)
                {
                    Animator.Wake();
                    StartAnimating();
                }
                else
                {
                    Animator.Awake = true;
                    Animator.StaticFrame();
                    _canvas.InvalidateVisual();
                }
            }
            _deadline = Settings.Deadline(DateTime.Now);
            if (_deadline != null) _tickTimer.Start();
            else _tickTimer.Stop();
            UpdateStatus();
        }

        /// <summary>Fecha os olhos e devolve o computador ao comportamento normal.</summary>
        public void Sleep()
        {
            Keeper.Stop();
            SetToggleText("Acordar a coruja");
            _deadline = null;
            _tickTimer.Stop();
            if (Settings.Data.AnimationsOn)
            {
                Animator.Sleep();
                StartAnimating();
            }
            else
            {
                Animator.Awake = false;
                Animator.StaticFrame();
                _canvas.InvalidateVisual();
            }
            UpdateStatus();
        }

        private void SetToggleText(string text)
        {
            _toggleItem.Header = text;
            _trayToggle.Text = text;
        }

        private void Tick()
        {
            if (_deadline is { } d && DateTime.Now >= d) Sleep();
            else UpdateStatus();
        }

        public string StatusText
        {
            get
            {
                if (!Keeper.Active) return "Dormindo";
                if (_deadline is not { } d) return "Acordada, sem limite";
                return $"Dorme em {Settings.FormatRemaining(d - DateTime.Now)} (às {d:HH:mm})";
            }
        }

        private void UpdateStatus()
        {
            var text = StatusText;
            _canvas.ToolTip = text;
            var tip = $"Coruja: {text}";
            _tray.Text = tip.Length > 63 ? tip[..63] : tip;   // o Windows aceita até 63 caracteres
        }
    }
}
