using GameBoyEmu.Exceptions;
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
        public virtual byte this[ushort address]
        {
            get
            {
                _logger.Debug($"Read memory address 0x{address:X4}");
                return _memoryMap[address];
            }
            set
            {

                if (address > ROM_MAX_ADDRESS)
                {
                    _logger.Info($"Wrote memory address 0x{address:X4} with value 0x{value:X2}");
                    _memoryMap[address] = value;
                }
                else if (address == Timers.DIV)
                {
                    _memoryMap[address] = 0x00;
                }
                else
                {
                    _logger.Info($"[Trying] to write memory address 0x{address:X4} with value 0x{value:X2}");
                }
            }
        }

        private byte[] _romDump = Array.Empty<byte>();

        protected Memory(bool skipInitialization)
        {
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
