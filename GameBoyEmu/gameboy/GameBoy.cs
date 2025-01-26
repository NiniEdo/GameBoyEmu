using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.MachineCyclesNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.TimersNamespace;
using System.Diagnostics;
using SDL2;
using NLog.LayoutRenderers.Wrappers;
using NLog;

namespace GameBoyEmu.gameboy
{
    internal class GameBoy
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();

        private const float FPS = 59.7F;
        private const int MCYCLES_PER_FRAME = (int)((4194304 / FPS) / 4);
        private const float FRAME_TIME_MS = (float)(1000 / FPS);
        Cpu _cpu;
        Memory _memory;
        MachineCycles _machineCycles = MachineCycles.GetInstance();

        public GameBoy()
        {
            _memory = new Memory();
            Interrupts.SetMemory(_memory);
            Timers.SetMemory(_memory);

            _cpu = new Cpu(_memory);
        }

        public void Start()
        {
            long frameTimer = Stopwatch.Frequency;
            Stopwatch timer = new Stopwatch();
            while (true)
            {
                timer.Restart();
                RunFrame();
                timer.Stop();

                DelayNextFrame(timer.ElapsedMilliseconds);
                _logger.Info($"Frame Time : {timer.ElapsedMilliseconds}ms");
            }
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
