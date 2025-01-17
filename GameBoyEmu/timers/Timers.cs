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
        private const ushort TIMA_ADDRESS = 0xFF05;
        private const ushort TMA_ADDRESS = 0xFF06;
        private const ushort TAC_ADDRES = 0xFF07;

        private ushort _timaTickCounter = 0;

        private ushort _divCounter = 0;
        private byte _div;
        private byte _tima;
        private byte _tma;
        private byte _tac;

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

        public byte Div
        {
            get => (byte)(_divCounter >> 8);
            set => _divCounter = 0x00;
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
            //_divCounter += 4;

            //byte tac = _memory[TAC_ADDRES];
            //if (((tac >> 2) & 0b0000_0001) == 1)
            //{
            //    ushort timaValue = _memory[TIMA_ADDRESS];
            //    _timaTickCounter += mCycleCount;
            //    int timaFrequency = _selectClockFrequency[(byte)(tac & 0b0000_0011)];

            //    if (_timaTickCounter >= timaFrequency)
            //    {
            //        _memory[TIMA_ADDRESS] += 1;
            //        timaValue += 1;
            //        _timaTickCounter = (ushort)(_timaTickCounter % timaFrequency);
            //    }

            //    if (timaValue > 0xFF) // check tima overflow
            //    {
            //        _memory[TIMA_ADDRESS] = _memory[TMA_ADDRESS];
            //        _interruptsManager.RequestTimerInterrupt();
            //    }
            //}

        }

        public void ResetDiv()
        {
            _divCounter = 0;
        }
    }
}
