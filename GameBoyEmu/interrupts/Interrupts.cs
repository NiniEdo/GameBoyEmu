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
        private Memory? _memory;
        private static Interrupts? _instance;

        private bool _imeFlag = false;
        protected bool _interruptFlag = false;
        protected ushort _counter = 0;
        public const ushort IE_ADDRESS = 0xFFFF;
        public const ushort IF_ADDRESS = 0xFF0F;

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

        public void SetMemory(Memory mem)
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
        public void EI()
        {
            _interruptFlag = true;
            _counter = 0;
        }

        public virtual void HandleEiIfNeeded()
        {
            if (_interruptFlag)
            {
                _counter++;
                if (_counter == 2)
                {
                    _interruptFlag = false;
                    _imeFlag = true;
                }
            }
        }

        public void RequestVblankInterrupt()
        {
            IF = (byte)((IF & 0b1111_1110) | (0b0000_0001));
        }
        public void DisableVblankInterrupt()
        {
            IF = (byte)(IF & 0b1111_1110);
        }

        public void RequestStatInterrupt()
        {
            IF = (byte)((IF & 0b1111_1101) | (0b0000_0010));
        }
        public void DisableStatInterrupt()
        {
            IF = (byte)(IF & 0b1111_1101);
        }

        public void RequestTimerInterrupt()
        {
            IF = (byte)((IF & 0b1111_1011) | (0b0000_0100));
        }
        public void DisableTimerInterrupt()
        {
            IF = (byte)(IF & 0b1111_1011);
        }

        public void RequestSerialInterrupt()
        {
            IF = (byte)((IF & 0b1111_0111) | (0b0000_1000));
        }
        public void DisableSerialInterrupt()
        {
            IF = (byte)(IF & 0b1111_0111);
        }

        public void RequestJoypadInterrupt()
        {
            IF = (byte)((IF & 0b1110_1111) | (0b0001_0000));
        }
        public void DisableJoypadInterrupt()
        {
            IF = (byte)(IF & 0b1110_1111);
        }
    }
}
