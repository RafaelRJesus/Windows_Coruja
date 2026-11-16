using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace Coruja.Logic
{
    public enum LimitMode
    {
        None = 0,   // fica acordada até eu fechar os olhos
        Timer = 1,  // contagem regressiva
        Clock = 2,  // horário fixo
    }

    public enum OwlSize
    {
        Small = 0,
        Medium = 1,
        Large = 2,
    }

    public static class OwlSizeExtensions
    {
        /// <summary>Tamanho da coruja em pontos (a janela é um pouco maior para os efeitos).</summary>
        public static double Points(this OwlSize s) => s switch { OwlSize.Medium => 95, OwlSize.Large => 125, _ => 70 };

        public static string Title(this OwlSize s) => s switch { OwlSize.Medium => "Média", OwlSize.Large => "Grande", _ => "Pequena" };
    }

    /// <summary>Dados salvos em %APPDATA%\Coruja\settings.json.</summary>
    public sealed class SettingsData
    {
        public int Mode { get; set; }
        public int TimerMinutes { get; set; } = 60;
        public int ClockMinutes { get; set; } = 18 * 60;
        public bool AnimationsOn { get; set; } = true;
        public bool WalkingOn { get; set; } = true;
        public bool AttentionOn { get; set; } = true;
        public bool ScheduleOn { get; set; } = true;
        public int Size { get; set; }
        public int Sunrise { get; set; } = 6 * 60;
        public int Sunset { get; set; } = 18 * 60 + 30;
        public bool AlwaysOnTop { get; set; } = true;
        public double? Left { get; set; }
        public double? Top { get; set; }
        public Dictionary<string, string> Played { get; set; } = new();
    }

    /// <summary>Lê e grava as configurações. Cada alteração chama Save().</summary>
    public sealed class Settings
    {
        public static string DefaultPath => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Coruja", "settings.json");

        private readonly string _path;
        public SettingsData Data { get; private set; } = new();

        public Settings(string? path = null)
        {
            _path = path ?? DefaultPath;
            try
            {
                if (File.Exists(_path))
                    Data = JsonSerializer.Deserialize<SettingsData>(File.ReadAllText(_path)) ?? new SettingsData();
            }
            catch (Exception)
            {
                Data = new SettingsData();   // arquivo estragado: começa do zero
            }
        }

        public void Save()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
                File.WriteAllText(_path, JsonSerializer.Serialize(Data, new JsonSerializerOptions { WriteIndented = true }));
            }
            catch (Exception)
            {
                // sem conseguir salvar, segue funcionando com o que está na memória
            }
        }

        public LimitMode Mode => Enum.IsDefined(typeof(LimitMode), Data.Mode) ? (LimitMode)Data.Mode : LimitMode.None;

        public OwlSize Size => Enum.IsDefined(typeof(OwlSize), Data.Size) ? (OwlSize)Data.Size : OwlSize.Small;

        /// <summary>Cor da pedra do brinco para cada configuração (r, g, b de 0 a 1).</summary>
        public static (double R, double G, double B) GemColor(LimitMode mode) => mode switch
        {
            LimitMode.Timer => (0.25, 0.55, 0.95),  // azul
            LimitMode.Clock => (0.20, 0.75, 0.45),  // verde
            _ => (0.95, 0.55, 0.10),                // laranja
        };

        /// <summary>Quando a coruja deve dormir a partir de agora (null = sem limite).</summary>
        public DateTime? Deadline(DateTime now)
        {
            switch (Mode)
            {
                case LimitMode.Timer:
                    return now.AddMinutes(Math.Max(1, Data.TimerMinutes));
                case LimitMode.Clock:
                    var today = now.Date.AddMinutes(Data.ClockMinutes);
                    return today > now ? today : today.AddDays(1);   // já passou hoje: vale para amanhã
                default:
                    return null;
            }
        }

        public static int MinutesNow(DateTime now) => now.Hour * 60 + now.Minute;

        /// <summary>Noite: entre o pôr e o nascer do sol (olhos um pouco mais baixos).</summary>
        public bool IsEvening(DateTime now)
        {
            int m = MinutesNow(now);
            return m >= Data.Sunset || m < Data.Sunrise;
        }

        /// <summary>Animações de horário tocam só uma vez por dia.</summary>
        public bool PlayedToday(string key, DateTime now) =>
            Data.Played.TryGetValue(key, out var day) && day == now.ToString("yyyy-MM-dd");

        public void MarkPlayedToday(string key, DateTime now)
        {
            Data.Played[key] = now.ToString("yyyy-MM-dd");
            Save();
        }

        public static string FormatRemaining(TimeSpan t)
        {
            int s = Math.Max(0, (int)Math.Ceiling(t.TotalSeconds));
            return $"{s / 3600:00}:{s % 3600 / 60:00}:{s % 60:00}";
        }
    }
}
