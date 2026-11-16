using System;
using System.Runtime.InteropServices;
using System.Windows.Threading;

namespace Coruja
{
    /// <summary>
    /// Mantém o Windows acordado enquanto a coruja está de olhos abertos:
    /// 1. SetThreadExecutionState impede que a tela apague e que o PC suspenda.
    /// 2. A cada 4 minutos envia a tecla F15 (não existe no teclado e não faz nada),
    ///    que zera o tempo ocioso usado por apps de chat para marcar "ausente".
    /// </summary>
    public sealed class KeepAwake
    {
        private const uint EsContinuous = 0x80000000;
        private const uint EsSystemRequired = 0x00000001;
        private const uint EsDisplayRequired = 0x00000002;
        private const uint InputKeyboard = 1;
        private const uint KeyEventKeyUp = 0x0002;
        private const ushort VkF15 = 0x7E;

        [DllImport("kernel32.dll")]
        private static extern uint SetThreadExecutionState(uint esFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern uint SendInput(uint count, Input[] inputs, int size);

        [StructLayout(LayoutKind.Sequential)]
        private struct Input
        {
            public uint Type;
            public InputUnion U;
        }

        // A união precisa do MOUSEINPUT (o maior membro) para ter o tamanho certo
        [StructLayout(LayoutKind.Explicit)]
        private struct InputUnion
        {
            [FieldOffset(0)] public MouseInput Mouse;
            [FieldOffset(0)] public KeybdInput Keyboard;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MouseInput
        {
            public int Dx;
            public int Dy;
            public uint MouseData;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct KeybdInput
        {
            public ushort Vk;
            public ushort Scan;
            public uint Flags;
            public uint Time;
            public IntPtr ExtraInfo;
        }

        private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromMinutes(4) };

        public bool Active { get; private set; }

        public KeepAwake()
        {
            _timer.Tick += (_, _) => Ping();
        }

        public void Start()
        {
            if (Active) return;
            Active = true;
            Ping();
            _timer.Start();
        }

        public void Stop()
        {
            if (!Active) return;
            Active = false;
            _timer.Stop();
            SetThreadExecutionState(EsContinuous);   // devolve o controle de energia ao normal
        }

        private void Ping()
        {
            SetThreadExecutionState(EsContinuous | EsSystemRequired | EsDisplayRequired);
            var inputs = new[]
            {
                new Input { Type = InputKeyboard, U = new InputUnion { Keyboard = new KeybdInput { Vk = VkF15 } } },
                new Input { Type = InputKeyboard, U = new InputUnion { Keyboard = new KeybdInput { Vk = VkF15, Flags = KeyEventKeyUp } } },
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<Input>());
        }
    }
}
