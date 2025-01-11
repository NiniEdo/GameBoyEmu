using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.InterruptsManagerNamespace;
using GameBoyEmu.MemoryNamespace;
namespace GameBoyEmu
{
    internal class GameBoy
    {
        Cpu _cpu;
        Memory _memory;
        InterruptsManager _interruptsManager;
        public GameBoy()
        {
            _memory = new Memory();
            _interruptsManager = new InterruptsManager(_memory);
            _cpu = new Cpu(_memory, _interruptsManager);
        }

        public void Start()
        {
            _cpu.Execute();
        }
    }
}
