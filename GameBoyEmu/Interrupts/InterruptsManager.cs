using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.MemoryNamespace;
namespace GameBoyEmu.InterruptsManagerNamespace
{
    public class InterruptsManager
    {
        private Memory _memory;
        private bool _imeFlag = false;
        protected bool _interruptFlag = false;
        protected ushort _PC = 0;
        public InterruptsManager(Memory mem)
        {
            _memory = mem;
        }

        public virtual byte IE
        {
            get => _memory[0xFFFF];
            set => _memory[0xFFFF] = value;
        }
        public virtual byte IF
        {
            get => _memory[0xFF0F];
            set => _memory[0xFF0F] = value;
        }
        public bool AreEnabled() { return _imeFlag; }
        public void EnableInterrupts() { _imeFlag = true; }
        public void DisableInterrupts() { _imeFlag = false; }
        public void EI(byte[] currentPC)
        {
            _interruptFlag = true;
            _PC = (ushort)((currentPC[1] << 8) | currentPC[0]);
        }

        public virtual void HandleEiIfNeeded(ushort currentPC)
        {
            if (_interruptFlag)
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
