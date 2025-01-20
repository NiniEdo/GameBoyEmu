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

        private ushort _div = 0;
        private byte _tima;
        private byte _tma;
        private byte _tac;


        private byte timerEnable = 0;
        private byte previousAndResult = 0;

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

        public void Tick(ushort mCycleCount)
        {
            byte tCycles = (byte)(4 * mCycleCount);

            _div += tCycles;

            for (int i = 0; i < tCycles; i++)
            {
                DetectFallingEdge();
            }
        }

        private void DetectFallingEdge()
        {
            timerEnable = (byte)((_tac & 0b0000_0100) >> 2);

            byte andResult = (byte)(timerEnable & GetDivBit()); 
            if (previousAndResult == 1 && andResult == 0)
            {
                _tima += 1;
            }
            previousAndResult = andResult;
        }

        private byte GetDivBit()
        {
            byte clockSelect = (byte)(_tac & 0b0000_0011);
            byte bitPosition = clockSelect switch
            {
                0b00 => 9,
                0b01 => 3,
                0b10 => 5,
                0b11 => 7,
                _ => 9
            };
            byte divBit = (byte)((_div >> bitPosition) & 0b0000_0001);
            return divBit;
        }
    }
}
