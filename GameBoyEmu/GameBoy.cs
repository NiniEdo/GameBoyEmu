using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.MemoryNamespace;
namespace GameBoyEmu
{
    internal class GameBoy
    {
        Cpu _cpu;
        Memory _memory;
        public GameBoy()
        {
            _memory = new Memory();
            _cpu = new Cpu(_memory);
        }

        public void Start()
        {
            _cpu.Execute();
        }


    }
}
