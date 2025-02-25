using System;
using System.Diagnostics;
using System.Threading;
using NLog;
using SDL2;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.MachineCyclesNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.PpuNamespace;
using GameBoyEmu.TimersNamespace;
using GameBoyEmu.ScreenNameSpace;

namespace GameBoyEmu.gameboy
{
    public class GameBoy
    {
        private const float FPS = 59.7F;
        private const int MCYCLES_PER_FRAME = (int)((4194304 / FPS) / 4);
        private const float FRAME_TIME_MS = (float)(1000 / FPS);

        private Cpu _cpu;
        private Ppu _ppu;
        private Memory _memory;
        private MachineCycles _machineCycles = MachineCycles.GetInstance();
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Screen _screen = Screen.GetInstance();

        private static bool isRunning = true;
        public static ref bool IsRunning => ref isRunning; // ref return
        public GameBoy(string[] cartridgePath)
        {
            _memory = new Memory(cartridgePath);
            Interrupts.GetInstance().SetMemory(_memory);
            Timers.GetInstance().SetMemory(_memory);

            _cpu = new Cpu(_memory);
            _ppu = new Ppu(_memory);

            _memory.SetPpu(_ppu);
            _machineCycles.SetPpu(_ppu);

            _screen.InitScreen();
        }

        public void Start()
        {
            Stopwatch timer = new Stopwatch();

            while (IsRunning)
            {
                timer.Restart();

                Screen.ListenForEvents(ref IsRunning);
                RunFrame();
                _screen.PresentScreen();

                timer.Stop();

                DelayNextFrame(timer.ElapsedMilliseconds);
                _logger.Info($"Frame Time : {timer.ElapsedMilliseconds}ms");
            }

            _screen.CloseScreen();
        }

        private void RunFrame()
        {
            _machineCycles.TickCounter = 0;
            while (_machineCycles.TickCounter < MCYCLES_PER_FRAME)
            {
                _cpu.Run();
            }
        }

        private void DelayNextFrame(long elapsedTime)
        {
            if (elapsedTime > FRAME_TIME_MS)
            {
                return;
            }

            long remainingTime = (long)FRAME_TIME_MS - elapsedTime;

            if (remainingTime > 0)
            {
                Thread.SpinWait((int)remainingTime);
            }
        }
    }
}