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
namespace GameBoyEmu.gameboy
{
    internal class GameBoy
    {
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
            int elapsedCycles = 0;
            while (elapsedCycles < 0)
            {
                _cpu.Run();
                elapsedCycles += _machineCycles.LastInstructionCycles;
            }
        }
    }
}
