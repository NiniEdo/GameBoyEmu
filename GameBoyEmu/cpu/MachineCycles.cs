using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.TimersNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.MachineCyclesNamespace
{
    internal class MachineCycles
    {
        private static MachineCycles? _instance;
        private Timers _timers = Timers.GetInstance();
        private int lastInstructionCycles = 0;

        private MachineCycles()
        {
        }

        public int LastInstructionCycles { get => lastInstructionCycles; }

        public static MachineCycles GetInstance()
        {
            if (_instance == null)
            {
                _instance = new MachineCycles();
            }
            return _instance;
        }

        public void Tick(int cycles)
        {
            lastInstructionCycles = cycles;

            TickComponents();
        }

        private void TickComponents()
        {
            _timers.Tick(lastInstructionCycles);
            
        }
    }
}
