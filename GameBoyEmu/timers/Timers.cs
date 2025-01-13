using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.InterruptsManagerNamespace;

namespace GameBoyEmu.TimersNamespace
{
    public class Timers
    {
        private Memory _memory;

        public const ushort DIV = 0xFF04;
        private const ushort TIMA = 0xFF05;
        private const ushort TMA = 0xFF06;
        private const ushort TAC = 0xFF07;

        private const ushort DIV_FREQUENCY = 16384;

        public Timers(Memory memory)
        {
            _memory = memory;
        }

        public void Tick(int incrementCycles)
        {

        }
    }
}
