using System;
using System.Threading;
using System.Windows;

namespace Coruja
{
    public static class Program
    {
        [STAThread]
        public static void Main()
        {
            // só uma coruja por vez
            using var single = new Mutex(true, "Coruja.Instancia.Unica", out bool first);
            if (!first) return;

            var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
            var coruja = new CorujaApp(app);
            coruja.Start();
            app.Run();
        }
    }
}
