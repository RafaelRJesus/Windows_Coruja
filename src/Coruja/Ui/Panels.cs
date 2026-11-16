using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using Coruja.Logic;

namespace Coruja.Ui
{
    /// <summary>Caixinha que abre numa orelha e fecha sozinha ao clicar fora.</summary>
    public abstract class EarPanel
    {
        public Popup Popup { get; }
        protected StackPanel Body { get; } = new() { Margin = new Thickness(14) };

        protected EarPanel(UIElement target)
        {
            var border = new Border
            {
                Child = Body,
                Background = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0xCC, 0xCC, 0xCC)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                MinWidth = 300,
            };
            Popup = new Popup
            {
                Child = border,
                PlacementTarget = target,
                Placement = PlacementMode.Top,
                StaysOpen = false,
                AllowsTransparency = true,
                PopupAnimation = PopupAnimation.Fade,
            };
            Popup.Opened += (_, _) => OnOpened();
            Popup.Closed += (_, _) => OnClosed();
        }

        public bool IsOpen => Popup.IsOpen;

        /// <summary>Abre acima da orelha (ou fecha, se já estiver aberto).</summary>
        public void Toggle(Rect ear)
        {
            if (Popup.IsOpen)
            {
                Popup.IsOpen = false;
                return;
            }
            Popup.PlacementRectangle = ear;
            Popup.IsOpen = true;
        }

        public void Close() => Popup.IsOpen = false;

        protected virtual void OnOpened() { }
        protected virtual void OnClosed() { }

        protected TextBlock Title(string text)
        {
            var t = new TextBlock { Text = text, FontWeight = FontWeights.Bold, Margin = new Thickness(0, 0, 0, 10) };
            Body.Children.Add(t);
            return t;
        }

        protected StackPanel Row(params object[] items)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 10) };
            foreach (var item in items)
            {
                var element = item as UIElement ?? new TextBlock { Text = item.ToString(), VerticalAlignment = VerticalAlignment.Center };
                if (element is FrameworkElement fe) fe.Margin = new Thickness(0, 0, 8, 0);
                row.Children.Add(element);
            }
            Body.Children.Add(row);
            return row;
        }

        protected static ComboBox Combo(params string[] items)
        {
            var c = new ComboBox { MinWidth = 70 };
            foreach (var i in items) c.Items.Add(i);
            return c;
        }
    }

    /// <summary>Orelha direita (brinco): quando a coruja deve dormir.</summary>
    public sealed class SleepPanel : EarPanel
    {
        private readonly CorujaApp _app;
        private readonly RadioButton[] _modes;
        private readonly ComboBox _hours = Combo(Enumerable.Range(0, 13).Select(h => $"{h} h").ToArray());
        private readonly ComboBox _minutes = Combo(Enumerable.Range(0, 12).Select(m => $"{m * 5} min").ToArray());
        private readonly ComboBox _clockHour = Combo(Enumerable.Range(0, 24).Select(h => $"{h:00}").ToArray());
        private readonly ComboBox _clockMinute = Combo(Enumerable.Range(0, 12).Select(m => $"{m * 5:00}").ToArray());
        private readonly StackPanel _timerRow;
        private readonly StackPanel _clockRow;
        private readonly TextBlock _status = new() { Foreground = Brushes.Gray, Margin = new Thickness(0, 0, 0, 10) };
        private readonly Button _apply = new() { Padding = new Thickness(12, 4, 12, 4), IsDefault = true };
        private readonly DispatcherTimer _live = new() { Interval = TimeSpan.FromSeconds(1) };
        private bool _loading;

        public SleepPanel(UIElement target, CorujaApp app) : base(target)
        {
            _app = app;
            Title("Quando a coruja deve dormir?");

            _modes = new[] { "Sem limite", "Temporizador", "Horário" }
                .Select((text, i) => new RadioButton { Content = text, GroupName = "modo", Tag = i })
                .ToArray();
            foreach (var r in _modes) r.Checked += (_, _) => ModeChanged();
            Row(_modes.Cast<object>().ToArray());

            _timerRow = Row("Dormir em", _hours, _minutes);
            _clockRow = Row("Dormir às", _clockHour, ":", _clockMinute);
            foreach (var c in new[] { _hours, _minutes, _clockHour, _clockMinute })
                c.SelectionChanged += (_, _) => ValuesChanged();

            Body.Children.Add(_status);
            _apply.Click += (_, _) => Apply();
            Body.Children.Add(_apply);

            _live.Tick += (_, _) => Refresh();
        }

        protected override void OnOpened()
        {
            var d = _app.Settings.Data;
            _loading = true;
            _modes[(int)_app.Settings.Mode].IsChecked = true;
            _hours.SelectedIndex = Math.Min(12, d.TimerMinutes / 60);
            _minutes.SelectedIndex = d.TimerMinutes % 60 / 5;
            _clockHour.SelectedIndex = d.ClockMinutes / 60;
            _clockMinute.SelectedIndex = d.ClockMinutes % 60 / 5;
            _loading = false;
            Refresh();
            _live.Start();
        }

        protected override void OnClosed() => _live.Stop();

        private void ModeChanged()
        {
            if (_loading) return;
            var mode = _modes.First(r => r.IsChecked == true);
            _app.Settings.Data.Mode = (int)mode.Tag;
            _app.Settings.Save();
            _app.UpdateGem();
            Refresh();
        }

        private void ValuesChanged()
        {
            if (_loading) return;
            var d = _app.Settings.Data;
            d.TimerMinutes = Math.Max(5, Math.Max(0, _hours.SelectedIndex) * 60 + Math.Max(0, _minutes.SelectedIndex) * 5);
            d.ClockMinutes = Math.Max(0, _clockHour.SelectedIndex) * 60 + Math.Max(0, _clockMinute.SelectedIndex) * 5;
            _app.Settings.Save();
            Refresh();
        }

        /// <summary>Salva e acorda a coruja com o limite escolhido (ou reinicia a contagem).</summary>
        private void Apply()
        {
            ValuesChanged();
            _app.Wake();
            Close();
        }

        private void Refresh()
        {
            var mode = _app.Settings.Mode;
            _timerRow.Visibility = mode == LimitMode.Timer ? Visibility.Visible : Visibility.Collapsed;
            _clockRow.Visibility = mode == LimitMode.Clock ? Visibility.Visible : Visibility.Collapsed;
            _apply.Content = _app.Keeper.Active ? "Aplicar" : "Acordar a coruja";
            _status.Text = _app.StatusText;
        }
    }

    /// <summary>Orelha esquerda: liga/desliga as animações e escolhe as opções e o tamanho.</summary>
    public sealed class AnimationPanel : EarPanel
    {
        private static readonly (string Name, Activity Activity)[] Playable =
        {
            ("Carinha de filhote", Activity.Puppy), ("Piar", Activity.Hoot), ("Bater asas", Activity.Flap),
            ("Pulinho", Activity.Hop), ("Olhar em volta", Activity.LookAround), ("Bocejar", Activity.Yawn),
            ("Descansar no ninho", Activity.Nest), ("Passear", Activity.Walking), ("Café", Activity.Coffee),
            ("Almoço", Activity.Lunch), ("Lanche", Activity.Snack), ("Festa de sexta", Activity.Party),
            ("Nascer do sol", Activity.Sunrise), ("Pôr do sol", Activity.Dusk),
        };

        private readonly CorujaApp _app;
        private readonly CheckBox _master = new() { Content = "Animações ligadas", Margin = new Thickness(0, 0, 0, 8) };
        private readonly CheckBox _walking = new() { Content = "Passear pela tela", Margin = new Thickness(0, 0, 0, 8) };
        private readonly CheckBox _attention = new() { Content = "Chamar atenção de vez em quando", Margin = new Thickness(0, 0, 0, 8) };
        private readonly CheckBox _schedule = new() { Content = "Animações por horário (café, almoço, lanche, sol)", Margin = new Thickness(0, 0, 0, 10) };
        private readonly ComboBox _size = Combo(Enum.GetValues(typeof(OwlSize)).Cast<OwlSize>().Select(s => s.Title()).ToArray());
        private readonly ComboBox _play = Combo(new[] { "Ver uma animação agora…" }.Concat(Playable.Select(p => p.Name)).ToArray());
        private bool _loading;

        public AnimationPanel(UIElement target, CorujaApp app) : base(target)
        {
            _app = app;
            Title("Animações");
            foreach (var box in new[] { _master, _walking, _attention, _schedule })
            {
                Body.Children.Add(box);
                box.Checked += (_, _) => OptionsChanged();
                box.Unchecked += (_, _) => OptionsChanged();
            }
            Row("Tamanho", _size);
            _size.SelectionChanged += (_, _) => SizeChanged();

            _play.Margin = new Thickness(0, 0, 0, 10);
            _play.SelectionChanged += (_, _) => PlayChosen();
            Body.Children.Add(_play);

            Body.Children.Add(new TextBlock
            {
                Text = "Desligadas, a coruja fica parada e não usa processamento.",
                Foreground = Brushes.Gray,
                FontSize = 11,
                TextWrapping = TextWrapping.Wrap,
                MaxWidth = 300,
            });
        }

        protected override void OnOpened()
        {
            var d = _app.Settings.Data;
            _loading = true;
            _master.IsChecked = d.AnimationsOn;
            _walking.IsChecked = d.WalkingOn;
            _attention.IsChecked = d.AttentionOn;
            _schedule.IsChecked = d.ScheduleOn;
            _size.SelectedIndex = (int)_app.Settings.Size;
            _play.SelectedIndex = 0;
            _loading = false;
            RefreshEnabled();
        }

        private void RefreshEnabled()
        {
            bool on = _master.IsChecked == true;
            foreach (var c in new Control[] { _walking, _attention, _schedule, _play }) c.IsEnabled = on;
        }

        private void OptionsChanged()
        {
            if (_loading) return;
            var d = _app.Settings.Data;
            bool wasOn = d.AnimationsOn;
            d.AnimationsOn = _master.IsChecked == true;
            d.WalkingOn = _walking.IsChecked == true;
            d.AttentionOn = _attention.IsChecked == true;
            d.ScheduleOn = _schedule.IsChecked == true;
            _app.Settings.Save();
            RefreshEnabled();
            if (wasOn != d.AnimationsOn) _app.ApplyAnimationSettings();
        }

        private void SizeChanged()
        {
            if (_loading || _size.SelectedIndex < 0) return;
            _app.Settings.Data.Size = _size.SelectedIndex;
            _app.Settings.Save();
            _app.ApplySize();
        }

        private void PlayChosen()
        {
            if (_loading || _play.SelectedIndex <= 0) return;
            var activity = Playable[_play.SelectedIndex - 1].Activity;
            _loading = true;
            _play.SelectedIndex = 0;
            _loading = false;
            Close();
            _app.PlayNow(activity);
        }
    }
}
