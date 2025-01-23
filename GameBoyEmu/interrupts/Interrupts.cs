using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.MemoryNamespace;
namespace GameBoyEmu.InterruptNamespace
{
    public class Interrupts
    {
        private static Memory? _memory;
        private static Interrupts? _instance;

        private bool _imeFlag = false;
        protected bool _interruptFlag = false;
        protected ushort _PC = 0;
        public const ushort IE_ADDRESS = 0xFFFF;
        public const ushort IF_ADDRESS = 0xFFF0;

        protected Interrupts()
        {
            _memory = null!;
        }

        public static Interrupts GetInstance()
        {
            if (_instance == null)
            {
                _instance = new Interrupts();
            }
            return _instance;
        }

        public static void SetMemory(Memory mem)
        {
            _memory = mem;
        }

        public virtual byte IE
        {
            get => _memory != null ? _memory[IE_ADDRESS] : throw new InvalidOperationException("Memory is not set.");
            set
            {
                if (_memory != null)
                {
                    _memory[IE_ADDRESS] = value;
                }
                else
                {
                    throw new InvalidOperationException("Memory is not set.");
                }
            }
        }
        public virtual byte IF
        {
            get => _memory != null ? _memory[IF_ADDRESS] : throw new InvalidOperationException("Memory is not set.");
            set
            {
                if (_memory != null)
                {
                    _memory[IF_ADDRESS] = value;
                }
                else
                {
                    throw new InvalidOperationException("Memory is not set.");
                }
            }
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

        public void RequestTimerInterrupt()
        {
            IE = (byte)((IE & 0b1111_1011) | (0b0000_0100));
        }
    }
}
