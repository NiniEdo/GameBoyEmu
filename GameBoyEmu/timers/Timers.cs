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
        private Interrupts _interrupts;
        private static Timers? _instance;
        private Memory? _memory;

        public const ushort DIV_ADDRESS = 0xFF04;
        public const ushort TIMA_ADDRESS = 0xFF05;
        public const ushort TMA_ADDRESS = 0xFF06;
        public const ushort TAC_ADDRESS = 0xFF07;

        private ushort _div;
        private byte _tima;
        private byte _tma;
        private byte _tac;

        private byte timerEnable = 0;
        private byte previousAndResult = 0;
        private bool _isTimaReloaded = false;
        private int _tCycleCount = 0;
        public byte Div { get => (byte)(_div >> 8); set => _div = 0x00; }
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
        public void SetMemory(Memory mem)
        {
            _memory = mem;
        }

        public void Tick(int mCycleCount)
        {
            int tCycles = (4 * mCycleCount);

            _div += (ushort)tCycles;

            for (int i = 0; i < tCycles; i++)
            {
                DetectFallingEdge();
            }
        }

        private void DetectFallingEdge()
        {
            timerEnable = (byte)((_tac & 0b0000_0100) >> 2);

            byte andResult = (byte)(timerEnable & GetDivBit());
            if (_isTimaReloaded)
            {
                HandleTimaReload();
            }
            else
            {
                if (previousAndResult == 1 && andResult == 0)
                {
                    if (_tima == 0xFF)
                    {
                        _isTimaReloaded = true;
                        _tima = 0x00;
                    }
                    else
                    {
                        _tima += 1;
                    }
                }
            }
            previousAndResult = andResult;
        }

        /*TODO: check if this causes problems:
         * If TIMA is written to on the same T-cycle on which the reload from TMA occurs the write is ignored 
         * and the value in TMA will be loaded into TIMA. However, if TMA is written to on the same T-cycle on which the reload occurs, 
         * TMA is updated before its value is loaded into TIMA, meaning the reload will be carried out with the new value.
         */
        private void HandleTimaReload()
        {
            _tCycleCount += 1;
            if (_tCycleCount == 4)
            {
                if (_tima != 0x00)
                {
                    _tima = _tma;
                    _isTimaReloaded = false;
                    _tCycleCount = 0;
                    _interrupts.RequestTimerInterrupt();
                }
            }
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
