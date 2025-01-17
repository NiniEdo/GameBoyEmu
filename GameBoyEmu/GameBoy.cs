using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.TimersNamespace;
namespace GameBoyEmu
{
    internal class GameBoy
    {
        Cpu _cpu;
        Memory _memory;
        public GameBoy()
        {
            _memory = new Memory();
            Interrupts.SetMemory(_memory);
            Timers.SetMemory(_memory);

            _cpu = new Cpu(_memory);
        }

        public void Start()
        {
            _cpu.Execute();
        }
    }
}
