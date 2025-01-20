﻿using GameBoyEmu.Exceptions;
using GameBoyEmu.CartridgeNamespace;
using GameBoyEmu.TimersNamespace;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.MemoryNamespace
{
    public class Memory
    {
        public const int ROM_MAX_ADDRESS = 0x7FFF;
        public const int MEM_MAX_ADDRESS = 0xFFFF;

        private Logger _logger = LogManager.GetCurrentClassLogger();
        private byte[] _memoryMap = new byte[0x1_0000]; //2^16 addresses (65.536)
        Cartridge _cartridge = new Cartridge();
        Timers _timers;

        public virtual byte this[ushort address]
        {
            get
            {
                switch (address)
                {
                    case Timers.DIV_ADDRESS:
                        _memoryMap[address] = _timers.Div;
                        break;
                    case Timers.TIMA_ADDRESS:
                        _memoryMap[address] = _timers.Tima;
                        break;
                    case Timers.TMA_ADDRESS:
                        _memoryMap[address] = _timers.Tma;
                        break;
                    case Timers.TAC_ADDRESS:
                        _memoryMap[address] = _timers.Tac;
                        break;
                    default:
                        break;
                }

                return _memoryMap[address];
            }
            set
            {
                switch (address)
                {
                    case Timers.DIV_ADDRESS:
                        _timers.Div = value;
                        break;
                    case Timers.TIMA_ADDRESS:
                        _timers.Tima = value;
                        break;
                    case Timers.TMA_ADDRESS:
                        _timers.Tma = value;
                        break;
                    case Timers.TAC_ADDRESS:
                        _timers.Tac = value;
                        break;
                    default:
                        if (address > ROM_MAX_ADDRESS)
                        {
                            _memoryMap[address] = value;
                        }
                        else
                        {
                            //rom writes attempts are possible but must be ignored
                            _logger.Debug($"[Trying] to write memory address 0x{address:X4} with value 0x{value:X2}");
                        }
                        break;
                }
            }
        }

        private byte[] _romDump = Array.Empty<byte>();

        protected Memory(bool skipInitialization)
        {
            _timers = null!;
        }

        public Memory()
        {
            try
            {
                _romDump = _cartridge.LoadRomFromCartridge();
                if (_romDump == null)
                {
                    throw new CartridgeException("Rom dump is null, aborting");
                }
                else
                {
                    initializeRom();
                }
            }
            catch (CartridgeException)
            {
                throw;
            }

            _timers = Timers.GetInstance();
        }


        private void initializeRom()
        {
            for (int i = 0; i < 0x7FFF && i < _romDump.Length; i++)
            {
                _memoryMap[i] = _romDump[i];
            }
        }
    }
}
