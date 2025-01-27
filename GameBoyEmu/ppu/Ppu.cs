using GameBoyEmu.MemoryNamespace;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.PpuNamespace
{
    internal class Ppu
    {
        private Memory _memory;
        public Ppu(Memory memory) {
            _memory = memory;
        }

        public void Tick(int mCycles)
        {

        }
    }
}
