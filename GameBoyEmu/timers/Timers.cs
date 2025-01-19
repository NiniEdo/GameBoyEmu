using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.InterruptNamespace;
using System.Net;

namespace GameBoyEmu.TimersNamespace
{
    public class Timers
    {
        //TODO: Timer obscure behavour
        private Interrupts _interrupts;
        private static Timers? _instance;
        private static Memory? _memory;

        public const ushort DIV_ADDRESS = 0xFF04;
        public const ushort TIMA_ADDRESS = 0xFF05;
        public const ushort TMA_ADDRESS = 0xFF06;
        public const ushort TAC_ADDRESS = 0xFF07;

        private ushort _timaTickCounter = 0;

        private ushort _div = 0;
        private byte _tima;
        private byte _tma;
        private byte _tac;

        public byte Div
        {
            get => (byte)(_div >> 8);
            set => _div = 0x00;
        }
        public byte Tima { get => _tima; set => _tima = value; }
        public byte Tma { get => _tma; set => _tma = value; }
        public byte Tac { get => _tac; set => _tac = value; }

        private Timers()
        {
            _interrupts = Interrupts.GetInstance();
        }

        public static Timers GetInstance()
        {
            if (_instance == null)
            {
                _instance = new Timers();
            }
            return _instance;
        }
        public static void SetMemory(Memory mem)
        {
            _memory = mem;
        }

        private Dictionary<byte, int> _selectClockFrequency = new Dictionary<byte, int>()
        {
            {00, 256},
            {01, 4},
            {10, 16},
            {11, 64}
        };

        public void Tick(ushort mCycleCount)
        {
            _div += 4;

            if (((_tac >> 2) & 0b0000_0001) == 1)
            {
                _timaTickCounter += mCycleCount;
                ushort timaValue = _tima;
                int timaFrequency = _selectClockFrequency[(byte)(_tac & 0b0000_0011)];

                if (_timaTickCounter >= timaFrequency)
                {
                    _tima += 1;
                    timaValue += 1;
                    _timaTickCounter = (ushort)(_timaTickCounter % timaFrequency);
                }

                if (timaValue > 0xFF) // check tima overflow
                {
                    _tima = _tma;
                    _interrupts.RequestTimerInterrupt();
                }
            }

        }
    }
}
