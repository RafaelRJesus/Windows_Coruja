namespace Coruja.Logic
{
    /// <summary>Vetor simples (unidades da coruja, y para cima).</summary>
    public readonly struct Vec
    {
        public readonly double X;
        public readonly double Y;

        public Vec(double x, double y)
        {
            X = x;
            Y = y;
        }

        public static readonly Vec Zero = new(0, 0);
    }

    /// <summary>O "rosto" e o corpo da coruja num quadro da animação.</summary>
    public struct Pose
    {
        public double Lid;       // pálpebra: 0 = aberto, 1 = fechado
        public Vec Look;         // direção do olhar (-1..1), y para cima
        public double Puppy;     // carinha de filhote
        public bool Happy;       // olhinhos ^^
        public double Wing;      // asas levantadas
        public double Step;      // passinho (-1..1)
        public double BeakOpen;  // bico aberto

        public static Pose Awake => new();
        public static Pose Asleep => new() { Lid = 1 };
    }

    public enum ParticleKind { Heart, Zzz, Note, Sparkle, Steam, Crumb, Confetti }

    /// <summary>Pequenos efeitos que sobem e somem.</summary>
    public sealed class Particle
    {
        public ParticleKind Kind;
        public double X, Y;       // unidades da coruja, y para cima
        public double VX, VY;     // unidades por segundo
        public double Life;       // segundos
        public double Size;       // unidades da coruja
        public double Age;
        public double Hue;        // cor do confete (0..1)
    }

    /// <summary>Objetos que aparecem nas animações.</summary>
    public enum Prop { Mug, Bowl, Cookie, PartyHat }
}
