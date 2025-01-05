using GameBoyEmu.CpuNamespace;
using GameBoyEmu.Exceptions;
using GameBoyEmu.CartridgeNamespace;
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
        public byte[] memoryMap
        {
            get
            {
                _logger.Info("accessed memory map"); // debug pourposes only
                return _memoryMap;
            }
            set
            {
                _memoryMap = value;
            }
        }

        private byte[] _romDump = Array.Empty<byte>();


        public Memory()
        {
            try
            {
                _romDump = _cartridge.loadRomFromCartridge();
            }
            catch (CartridgeException CAex)
            {
                _logger.Fatal("Error: " + CAex.Message);
            }
            initializeRom();
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
