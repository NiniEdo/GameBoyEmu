using GameBoyEmu.interfaces;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.PpuNamespace;
using GameBoyEmu.TimersNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.MachineCyclesNamespace
{
    internal class MachineCycles : ITickable
    {
        private static MachineCycles? _instance;
        private Timers _timers = Timers.GetInstance();
        private Ppu? _ppu;

        private MachineCycles()
        { }

        public void SetPpu(Ppu ppu)
        {
            _ppu = ppu;
        }
        public static MachineCycles GetInstance()
        {
            if (_instance == null)
            {
                _instance = new MachineCycles();
            }
            return _instance;
        }

        public void Tick()
        {
            TickComponents();
        }

        private void TickComponents()
        {
            _timers.Tick();
            _ppu!.Tick();
        }
    }
}
    