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
    internal class GameBoy
    {
        private const float FPS = 59.7F;
        private const int MCYCLES_PER_FRAME = (int)((4194304 / FPS) / 4);
        private const float FRAME_TIME_MS = (float)(1000 / FPS);

        private Cpu _cpu;
        private Ppu _ppu;
        private Memory _memory;
        private MachineCycles _machineCycles = MachineCycles.GetInstance();
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Screen _screen = new Screen();

        public GameBoy()
        {
            _memory = new Memory();
            Interrupts.SetMemory(_memory);
            Timers.SetMemory(_memory);

            _cpu = new Cpu(_memory);
            _ppu = new Ppu(_memory);

            _machineCycles.SetPpu(_ppu);
            _screen.InitScreen();
        }

        public void Start()
        {
            Stopwatch timer = new Stopwatch();
            bool running = true;

            while (running)
            {
                timer.Restart();

                _screen.ListenForEvents(ref running);
                RunFrame();
                _screen.RenderScreen();

                timer.Stop();

                DelayNextFrame(timer.ElapsedMilliseconds);
                _logger.Info($"Frame Time : {timer.ElapsedMilliseconds}ms");
            }

            _screen.CloseScreen();
        }

        private void RunFrame()
        {
            int elapsedMCycles = 0;
            while (elapsedMCycles < MCYCLES_PER_FRAME)
            {
                _cpu.Run();
                elapsedMCycles += _machineCycles.LastInstructionCycles;
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
                Thread.Sleep((int)remainingTime);
            }
        }
    }
}