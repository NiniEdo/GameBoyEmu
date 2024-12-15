﻿using GameBoyEmu.CpuNamespace;
using GameBoyEmu.Exceptions;
using GameBoyEmu.RomNamespace;
using NLog;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.MemoryNamespace
{
    internal class Memory
    {
        const int ROM_MAX_ADDRESS = 0x7FFF;
        public double romMaxAddress
        {
            get => ROM_MAX_ADDRESS;
        }

        private Logger _logger = LogManager.GetCurrentClassLogger();
        private static Memory _memory = new Memory();
        private byte[] _memoryMap = new byte[0xFFFF];
        public byte[] memoryMap { get => _memoryMap; set => _memoryMap = value; }

        private byte[] _romDump = Array.Empty<byte>();
        Rom _rom = Rom.GetRom();

        public static Memory GetMemory()
        {
            return _memory;
        }

        private Memory()
        {
            try
            {
                _romDump = _rom.loadFromCartridge();
            }
            catch (CartridgeException CAex)
            {
                _logger.Fatal("Error: " + CAex.Message);
            }
            initializeRom();
        }


        private void initializeRom()
        {
            for (int i = 0; i < 0x7FFF; i++)
            {
                _memoryMap[i] = _romDump[i];
            }
        }
    }
}
