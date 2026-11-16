using System;
using System.Collections.Generic;
using System.IO;
using Coruja.Logic;

// Verificação rápida da lógica: passa por todas as animações e simula 30 min acordada.
var path = Path.Combine(Path.GetTempPath(), $"coruja-check-{Guid.NewGuid()}.json");
var settings = new Settings(path);
int failures = 0;
void Check(bool ok, string what)
{
    Console.WriteLine($"{(ok ? "ok  " : "FALHOU")} {what}");
    if (!ok) failures++;
}

// Configurações
Check(settings.Mode == LimitMode.None && settings.Deadline(DateTime.Now) == null, "sem limite não tem prazo");
settings.Data.Mode = (int)LimitMode.Timer; settings.Data.TimerMinutes = 90; settings.Save();
var now = new DateTime(2026, 11, 16, 10, 0, 0);
Check(new Settings(path).Deadline(now) == now.AddMinutes(90), "temporizador salvo e relido");
settings.Data.Mode = (int)LimitMode.Clock; settings.Data.ClockMinutes = 9 * 60;
Check(settings.Deadline(now) == new DateTime(2026, 11, 17, 9, 0, 0), "horário que já passou vale para amanhã");
Check(Settings.FormatRemaining(TimeSpan.FromSeconds(3661.2)) == "01:01:02", "formato do tempo restante");
settings.Data.Mode = 0;

// Animador
var a = new Animator(settings);
double moved = 0;
a.MoveWindow = dx => { moved += dx; return true; };
a.CanWalk = () => true;
a.Wake();
foreach (Activity act in Enum.GetValues(typeof(Activity)))
{
    a.Play(act);
    for (int i = 0; i < 400; i++) a.Update(1.0 / 30);
}
Check(true, "todas as animações rodaram sem erro");

var seen = new HashSet<Activity>();
a.Play(Activity.Idle, 0);
for (int i = 0; i < 30 * 60 * 30; i++)
{
    a.Update(1.0 / 30);
    seen.Add(a.Activity);
}
Console.WriteLine("     vistas em 30 min: " + string.Join(", ", seen));
Check(seen.Count >= 6, "variedade de animações aleatórias");
Check(a.Particles.Count <= 40, "no máximo 40 partículas");

a.Sleep();
for (int i = 0; i < 200; i++) a.Update(1.0 / 30);
Check(a.Activity == Activity.Sleeping && a.Nest > 0.95, "dorme no ninho depois do pôr do sol");
Check(a.DesiredFps == 8, "dormindo usa só 8 quadros/s");

File.Delete(path);
Console.WriteLine(failures == 0 ? "Tudo certo." : $"{failures} verificação(ões) falharam.");
return failures == 0 ? 0 : 1;
