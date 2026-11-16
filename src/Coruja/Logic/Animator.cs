using System;
using System.Collections.Generic;
using System.Linq;

namespace Coruja.Logic
{
    public enum Activity
    {
        Idle, Sleeping,
        Sunrise, Sunset, Dusk,
        Walking,
        Puppy, Petted,
        Hoot, Flap, Yawn, LookAround, Hop,
        Nest,
        Coffee, Lunch, Snack, Party,
    }

    /// <summary>Animação de horário: toca uma vez por dia, num minuto sorteado dentro da janela.</summary>
    public sealed record Scheduled(string Key, Activity Activity, int Start, int End, DayOfWeek? Weekday)
    {
        public int FireAt { get; init; }
    }

    /// <summary>
    /// Motor de animação: calcula cada quadro sem desenhar nada.
    /// A janela lê pose, deslocamento, inclinação, sol, ninho, objeto e partículas.
    /// </summary>
    public sealed class Animator
    {
        private static readonly Random Rng = new();

        private static readonly Scheduled[] Schedule =
        {
            new("cafe", Activity.Coffee, 8 * 60 + 30, 10 * 60, null),
            new("almoco", Activity.Lunch, 12 * 60, 13 * 60 + 30, null),
            new("lanche", Activity.Snack, 15 * 60 + 30, 16 * 60 + 30, null),
            new("sextou", Activity.Party, 17 * 60, 18 * 60, DayOfWeek.Friday),
        };

        private readonly Settings _settings;

        // Saída (lida pela janela)
        public Pose Pose { get; private set; } = Pose.Asleep;
        public Vec Offset { get; private set; } = Vec.Zero;
        public double Tilt { get; private set; }
        public double Squash { get; private set; } = 1;
        public (double Pos, double Alpha)? Sun { get; private set; }
        public double SunSpin { get; private set; }
        public double Nest { get; private set; } = 1;
        public Prop? Prop { get; private set; }
        public double PropPhase { get; private set; }
        public List<Particle> Particles { get; } = new();
        public Activity Activity { get; private set; } = Activity.Sleeping;

        // Entrada (preenchida pelo app)
        public bool Awake { get; set; }
        public Vec MouseLook { get; set; } = Vec.Zero;
        public bool Dragging { get; set; }
        public Func<double, bool>? MoveWindow { get; set; }   // false = bateu na borda
        public Func<bool>? CanWalk { get; set; }

        // Relógio interno
        private double _time;
        private double _started;
        private double _duration;
        private readonly HashSet<string> _fired = new();
        private double _nextBlink = 2;
        private double _blinkEnd;
        private double _nextRandom = 40;
        private double _nextAttention = Range(15 * 60, 30 * 60);
        private Activity? _lastRandom;
        private double _nextParticle;
        private int _lastCycle = -1;
        private double _walkDir = 1;

        // Horários
        private double _clockCheck;
        private bool _evening;
        private List<Scheduled> _today = new();
        private DateTime _scheduleDay = DateTime.MinValue;

        public Animator(Settings settings)
        {
            _settings = settings;
            _evening = settings.IsEvening(DateTime.Now);
        }

        /// <summary>Quadros por segundo de que precisa agora. Parada = poucos quadros (leve).</summary>
        public int DesiredFps
        {
            get
            {
                if (Dragging) return 30;
                return Activity switch
                {
                    Activity.Idle => Particles.Count == 0 ? 12 : 20,
                    Activity.Sleeping => 8,
                    Activity.Nest => 10,
                    _ => 30,
                };
            }
        }

        // Utilitários

        private static double Range(double a, double b) => a + Rng.NextDouble() * (b - a);
        private static double Clamp01(double x) => Math.Max(0, Math.Min(1, x));

        /// <summary>Sobe suave de 0 a 1 entre a e b.</summary>
        private static double Smooth(double a, double b, double x)
        {
            double t = Clamp01((x - a) / (b - a));
            return t * t * (3 - 2 * t);
        }

        /// <summary>Sobe e desce (0 → 1 → 0) entre a e b.</summary>
        private static double Bump(double x, double a, double b) =>
            x <= a || x >= b ? 0 : Math.Sin((x - a) / (b - a) * Math.PI);

        // Comandos

        public void Wake()
        {
            Awake = true;
            Play(Activity.Sunrise);
        }

        public void Sleep()
        {
            Awake = false;
            Play(Activity.Sunset);
        }

        public void Pet()
        {
            if (Awake) Play(Activity.Petted);
            else SpawnHeart();   // dormindo: só um coraçãozinho, sem acordar
        }

        public void Play(Activity a, double? duration = null)
        {
            Activity = a;
            _started = _time;
            _fired.Clear();
            _lastCycle = -1;
            _duration = duration ?? a switch
            {
                Activity.Sunrise or Activity.Sunset => 4.5,
                Activity.Dusk => 5,
                Activity.Walking => Range(3, 8),
                Activity.Puppy => 4.5,
                Activity.Petted => 1.8,
                Activity.Hoot => 2,
                Activity.Flap => 1.4,
                Activity.Yawn => 2.6,
                Activity.LookAround => 3,
                Activity.Hop => 1.2,
                Activity.Nest => Range(60, 150),
                Activity.Coffee => 8,
                Activity.Lunch => 9,
                Activity.Snack => 8,
                Activity.Party => 6,
                _ => 0,
            };
            if (a == Activity.Walking)
            {
                _walkDir = Rng.Next(2) == 0 ? -1 : 1;
                if (CanWalk != null && !CanWalk()) Finish();
            }
        }

        /// <summary>Quadro parado (animações desligadas).</summary>
        public void StaticFrame()
        {
            Activity = Awake ? Activity.Idle : Activity.Sleeping;
            Pose = Awake ? new Pose { Lid = _evening ? 0.3 : 0 } : Pose.Asleep;
            Offset = new Vec(0, Awake ? 0 : -0.05);
            Tilt = 0;
            Squash = 1;
            Sun = null;
            Prop = null;
            Nest = Awake ? 0 : 1;
            Particles.Clear();
        }

        private void Finish()
        {
            if (Awake)
            {
                Activity = Activity.Idle;
                _nextRandom = _time + Range(20, 90);
            }
            else
            {
                Activity = Activity.Sleeping;
            }
            _started = _time;
            _duration = 0;
        }

        // Quadro a quadro

        public void Update(double dt)
        {
            _time += dt;
            CheckClock();
            if (Awake && Activity == Activity.Idle && _time >= _nextRandom) PickRandom();

            double t = _time - _started;
            double p = _duration > 0 ? Clamp01(t / _duration) : 0;
            double baseLid = _evening ? 0.3 : 0;

            var pose = new Pose { Lid = Awake ? baseLid : 1, Look = Awake ? MouseLook : Vec.Zero };
            double offY = 0, tilt = 0;
            double squash = 1 + 0.012 * Math.Sin(_time * 2.0);   // respiração
            (double, double)? sun = null;
            double nestTarget = Awake ? 0 : 1;
            Prop? prop = null;
            double propPhase = 0;

            switch (Activity)
            {
                case Activity.Sleeping:
                    pose.Lid = 1;
                    nestTarget = 1;
                    offY = -0.05;
                    squash = 1 + 0.025 * Math.Sin(_time * 1.2);
                    if (Every(1.7)) Spawn(ParticleKind.Zzz, 0.70, 0.82, 0.06, 0.10, 2.8, 0.10 + Range(0, 0.04));
                    break;

                case Activity.Sunrise:   // o sol nasce e ela se espreguiça
                {
                    sun = (Smooth(0, 0.7, p), 1 - Smooth(0.8, 1, p));
                    pose.Lid = 1 - Smooth(0.25, 0.6, p) * (1 - baseLid);
                    double stretch = Bump(p, 0.35, 0.9);
                    squash += 0.12 * stretch;
                    pose.Wing = stretch;
                    pose.BeakOpen = 0.8 * Bump(p, 0.4, 0.75);
                    nestTarget = 1 - Smooth(0.6, 0.95, p);
                    offY = -0.05 * nestTarget;
                    break;
                }

                case Activity.Sunset:    // o sol se põe, os olhos fecham e ela se aninha
                    sun = (1 - Smooth(0.1, 1, p), Smooth(0, 0.15, p) * (1 - Smooth(0.85, 1, p)));
                    pose.Lid = baseLid + (1 - baseLid) * Smooth(0.3, 0.95, p);
                    nestTarget = Smooth(0.45, 0.9, p);
                    offY = -0.05 * nestTarget;
                    break;

                case Activity.Dusk:      // anoitecer acordada: olhos um pouco mais baixos
                    sun = (1 - Smooth(0.1, 1, p), Smooth(0, 0.15, p) * (1 - Smooth(0.85, 1, p)));
                    pose.Lid = 0.3 * Smooth(0.3, 1, p);
                    break;

                case Activity.Walking:
                {
                    double phase = _time * 9;
                    tilt = 7 * Math.Sin(phase);
                    offY = 0.03 * Math.Abs(Math.Sin(phase));
                    pose.Step = Math.Sin(phase);
                    pose.Look = new Vec(_walkDir * 0.7, -0.1);
                    if (MoveWindow != null && !MoveWindow(_walkDir * 28 * dt)) _duration = t;
                    break;
                }

                case Activity.Puppy:
                {
                    double k = Smooth(0, 0.12, p) * (1 - Smooth(0.85, 1, p));
                    pose.Puppy = k;
                    pose.Lid = 0;
                    pose.Look = new Vec(0, 0.45 * k);
                    tilt = 10 * k;
                    squash += 0.03 * k;
                    if (p < 0.3) offY = 0.07 * Math.Abs(Math.Sin(p / 0.3 * 2 * Math.PI));
                    if (Once("heart", p > 0.35)) SpawnHeart();
                    if (Every(0.45) && k > 0.5) SpawnSparkle();
                    break;
                }

                case Activity.Petted:
                    pose.Happy = true;
                    offY = 0.04 * Math.Abs(Math.Sin(p * 2 * Math.PI));
                    if (Once("h0", p > 0.1)) SpawnHeart();
                    if (Once("h1", p > 0.4)) SpawnHeart();
                    if (Once("h2", p > 0.7)) SpawnHeart();
                    break;

                case Activity.Hoot:
                    pose.BeakOpen = Bump(p, 0.1, 0.5) + Bump(p, 0.5, 0.9);
                    squash += 0.05 * pose.BeakOpen;
                    if (Once("n0", p > 0.2)) Spawn(ParticleKind.Note, 0.62, 0.55, 0.14, 0.18, 1.6, 0.15);
                    if (Once("n1", p > 0.6)) Spawn(ParticleKind.Note, 0.62, 0.55, 0.14, 0.18, 1.6, 0.15);
                    break;

                case Activity.Flap:
                    pose.Wing = Math.Abs(Math.Sin(p * Math.PI * 4)) * (1 - Smooth(0.85, 1, p));
                    offY = 0.03 * pose.Wing;
                    break;

                case Activity.Yawn:
                {
                    double b = Bump(p, 0.1, 0.85);
                    pose.BeakOpen = b;
                    pose.Lid = Math.Max(baseLid, 0.75 * b);
                    squash += 0.06 * b;
                    tilt = -4 * b;
                    break;
                }

                case Activity.LookAround:
                    pose.Look = new Vec(0.9 * Math.Sin(p * 2 * Math.PI), 0.2 * Math.Sin(p * 4 * Math.PI));
                    tilt = 6 * Math.Sin(p * 2 * Math.PI);
                    break;

                case Activity.Hop:
                    offY = 0.08 * Math.Abs(Math.Sin(p * 2 * Math.PI));
                    squash -= 0.05 * Bump(p, 0.45, 0.55) + 0.05 * Bump(p, 0.9, 1);
                    break;

                case Activity.Nest:
                    nestTarget = 1 - Smooth(0.95, 1, p);
                    pose.Lid = Math.Max(baseLid, 0.5);
                    offY = -0.05 * Nest;
                    break;

                case Activity.Coffee:
                {
                    prop = Logic.Prop.Mug;
                    var (x, _) = Cycle(2.5, t);
                    propPhase = Bump(x, 0.3, 0.8);
                    pose.Lid = Math.Max(baseLid, 0.6 * propPhase);   // fecha os olhos no gole
                    if (Every(0.5))
                        Spawn(ParticleKind.Steam, 0.17 + Range(-0.02, 0.02), 0.40 + propPhase * 0.08, 0.01, 0.12, 1.2, 0.04);
                    break;
                }

                case Activity.Lunch:
                {
                    prop = Logic.Prop.Bowl;
                    var (x, isNew) = Cycle(0.8, t);
                    double peck = Bump(x, 0, 1);
                    offY = -0.06 * peck;
                    pose.Look = new Vec(0, -0.7);
                    pose.BeakOpen = peck > 0.6 ? 0.5 : 0;
                    if (isNew && t > 0.5) SpawnCrumbs(0.5, 0.14);
                    if (p > 0.9) pose.Happy = true;
                    break;
                }

                case Activity.Snack:
                {
                    prop = Logic.Prop.Cookie;
                    propPhase = p;
                    var (x, isNew) = Cycle(1.2, t);
                    pose.BeakOpen = 0.6 * Bump(x, 0.2, 0.6);
                    pose.Look = new Vec(0.7, -0.3);
                    if (isNew && t > 0.8) SpawnCrumbs(0.80, 0.26);
                    break;
                }

                case Activity.Party:
                    prop = Logic.Prop.PartyHat;
                    pose.Happy = p > 0.2 && p < 0.8;
                    offY = 0.06 * Math.Abs(Math.Sin(p * 4 * Math.PI));
                    pose.Wing = 0.5 * Math.Abs(Math.Sin(p * 4 * Math.PI));
                    if (Every(0.12) && p < 0.8) SpawnConfetti();
                    break;
            }

            // piscadas (às vezes duplas)
            if (Awake && !pose.Happy && pose.Lid < 0.9)
            {
                if (_time >= _nextBlink)
                {
                    _blinkEnd = _time + 0.13;
                    _nextBlink = _time + (Rng.NextDouble() < 0.15 ? 0.35 : Range(2.5, 6.5));
                }
                if (_time < _blinkEnd) pose.Lid = 1;
            }

            // sendo arrastada: bate as asas
            if (Dragging) pose.Wing = Math.Max(pose.Wing, 0.4 + 0.4 * Math.Abs(Math.Sin(_time * 16)));

            if (_duration > 0 && t >= _duration) Finish();

            Nest += (nestTarget - Nest) * Math.Min(1, dt * 4);
            SunSpin += dt * 0.4;
            Pose = pose;
            Offset = new Vec(0, offY);
            Tilt = tilt;
            Squash = squash;
            Sun = sun;
            Prop = prop;
            PropPhase = propPhase;
            UpdateParticles(dt);
        }

        // Escolha aleatória (imprevisível, sem repetir a última)

        private void PickRandom()
        {
            var d = _settings.Data;
            if (d.AttentionOn && _time >= _nextAttention)
            {
                _nextAttention = _time + Range(20 * 60, 45 * 60);
                Play(Activity.Puppy);
                return;
            }
            var options = new List<(Activity A, double W)>
            {
                (Activity.LookAround, 3), (Activity.Hoot, 2), (Activity.Flap, 2), (Activity.Hop, 1.5),
                (Activity.Nest, 1), (Activity.Yawn, _evening ? 2 : 0.3),
            };
            if (d.WalkingOn && CanWalk != null && CanWalk()) options.Add((Activity.Walking, 2.5));
            options.RemoveAll(o => o.A == _lastRandom);

            double roll = Rng.NextDouble() * options.Sum(o => o.W);
            foreach (var (a, w) in options)
            {
                roll -= w;
                if (roll < 0)
                {
                    _lastRandom = a;
                    Play(a);
                    return;
                }
            }
            Finish();
        }

        // Horários (conferidos a cada 20 s, não a cada quadro)

        private void CheckClock()
        {
            if (_time < _clockCheck) return;
            _clockCheck = _time + 20;
            var now = DateTime.Now;

            if (now.Date != _scheduleDay)
            {
                _scheduleDay = now.Date;
                _today = Schedule
                    .Where(s => s.Weekday == null || s.Weekday == now.DayOfWeek)
                    .Select(s => s with { FireAt = Rng.Next(s.Start, Math.Max(s.Start, s.End - 10) + 1) })
                    .ToList();
            }

            var d = _settings.Data;
            bool ev = _settings.IsEvening(now);
            if (ev != _evening)
            {
                _evening = ev;
                if (d.ScheduleOn && Awake && (Activity == Activity.Idle || Activity == Activity.Nest))
                    Play(ev ? Activity.Dusk : Activity.Sunrise);
            }

            if (!(d.ScheduleOn && Awake && Activity == Activity.Idle)) return;
            int m = Settings.MinutesNow(now);
            foreach (var item in _today)
            {
                if (m >= item.FireAt && m <= item.End && !_settings.PlayedToday(item.Key, now))
                {
                    _settings.MarkPlayedToday(item.Key, now);
                    Play(item.Activity);
                    return;
                }
            }
        }

        // Tempo

        /// <summary>Verdadeiro uma única vez por animação, quando a condição vira verdadeira.</summary>
        private bool Once(string key, bool cond) => cond && _fired.Add(key);

        /// <summary>Verdadeiro a cada `interval` segundos.</summary>
        private bool Every(double interval)
        {
            if (_time < _nextParticle) return false;
            _nextParticle = _time + interval;
            return true;
        }

        /// <summary>Posição (0..1) dentro de um ciclo e se um ciclo novo acabou de começar.</summary>
        private (double X, bool IsNew) Cycle(double period, double t)
        {
            int n = (int)(t / period);
            bool isNew = n != _lastCycle;
            _lastCycle = n;
            return (t % period / period, isNew);
        }

        // Partículas

        private void Spawn(ParticleKind kind, double x, double y, double vx, double vy, double life, double size, double hue = 0)
        {
            if (Particles.Count >= 40) return;   // limite para ficar leve
            Particles.Add(new Particle { Kind = kind, X = x, Y = y, VX = vx, VY = vy, Life = life, Size = size, Hue = hue });
        }

        private void SpawnHeart() =>
            Spawn(ParticleKind.Heart, Range(0.35, 0.65), 0.92, Range(-0.05, 0.05), 0.22, 1.6, 0.12);

        private void SpawnSparkle()
        {
            double x = Rng.Next(2) == 0 ? Range(0.15, 0.30) : Range(0.70, 0.85);
            Spawn(ParticleKind.Sparkle, x, Range(0.55, 0.85), 0, 0.03, 0.7, 0.08);
        }

        private void SpawnCrumbs(double x, double y)
        {
            for (int i = 0; i < 3; i++)
                Spawn(ParticleKind.Crumb, x, y, Range(-0.15, 0.15), Range(0.05, 0.15), 0.6, 0.025);
        }

        private void SpawnConfetti() =>
            Spawn(ParticleKind.Confetti, Range(0.1, 0.9), 1.1, Range(-0.08, 0.08), -0.35, 1.8, 0.04, Rng.NextDouble());

        private void UpdateParticles(double dt)
        {
            if (Particles.Count == 0) return;
            foreach (var pt in Particles)
            {
                pt.X += pt.VX * dt;
                pt.Y += pt.VY * dt;
                pt.Age += dt;
                if (pt.Kind == ParticleKind.Crumb) pt.VY -= 0.6 * dt;   // migalha cai
            }
            Particles.RemoveAll(pt => pt.Age >= pt.Life);
        }
    }
}
