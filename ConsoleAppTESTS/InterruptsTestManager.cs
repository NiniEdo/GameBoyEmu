using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ConsoleAppTESTS;
using GameBoyEmu.MemoryNamespace;
namespace GameBoyEmu.InterruptsManagerNamespace
{
    internal class InterruptsTestManager : InterruptsManager
    {
        private bool _imeFlag = false;
        private byte _ie;
        private byte _if;
        public InterruptsTestManager(TestMemory testmemory) : base(testmemory)
        {
        }

        public override byte IE
        {
            get => (byte)(_ie == 0b1111_1111 ? 1 : 0);
            set => _ie = (byte)(value == 1 ? 0b1111_1111 : 0b0000_0000);
        }
        public override byte IF
        {
            get => (byte)0b0000_0000;
        }

        public bool InterruptFlag
        {
            get { return base._interruptFlag; }
            set { _interruptFlag = value; }
        }

        public override void HandleEiIfNeeded(ushort currentPC)
        {
            if (_interruptFlag && _PC != 0)
            {
                if (currentPC == _PC + 2)
                {
                    _interruptFlag = false;
                    _imeFlag = true;
                    _PC = 0;
                }
            }

        }
    }
}
