using GameBoyEmu.Exceptions;
using GameBoyEmu.CartridgeNamespace;
using GameBoyEmu.TimersNamespace;
using NLog;
using GameBoyEmu.PpuNamespace;
using GameBoyEmu.SerialTransferNamespace;
using System.Runtime.CompilerServices;

namespace GameBoyEmu.MemoryNamespace
{
    public class Memory
    {
        public const int ROM_MAX_ADDRESS = 0x7FFF;
        public const int MEM_MAX_ADDRESS = 0xFFFF;

        private byte[] _memoryMap = new byte[0x1_0000]; //2^16 addresses (65.536)
        private Cartridge _cartridge = new Cartridge();
        private SerialTransfer _serialTranfer = SerialTransfer.GetInstance();
        private Timers? _timers;
        private Ppu? _ppu;

        private Logger _logger = LogManager.GetCurrentClassLogger();
        public virtual byte this[ushort address]
        {
            get
            {
                if (_timers != null && _ppu != null)
                {
                    switch (address)
                    {
                        case SerialTransfer.SB_ADDRESS:
                            _memoryMap[address] = _serialTranfer.Sb;
                            break;
                        case SerialTransfer.SC_ADDRESS:
                            _memoryMap[address] = _serialTranfer.Sc;
                            break;
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
                        case Ppu.LCDC_ADDRESS:
                            _memoryMap[address] = _ppu.Lcdc;
                            break;
                        case Ppu.STAT_ADDRESS:
                            _memoryMap[address] = _ppu.Stat;
                            break;
                        case Ppu.SCY_ADDRESS:
                            _memoryMap[address] = _ppu.Scy;
                            break;
                        case Ppu.SCX_ADDRESS:
                            _memoryMap[address] = _ppu.Scx;
                            break;
                        case Ppu.LY_ADDRESS:
                            _memoryMap[address] = _ppu.Ly;
                            break;
                        case Ppu.LYC_ADDRESS:
                            _memoryMap[address] = _ppu.Lyc;
                            break;
                        case Ppu.DMA_ADDRESS:
                            _memoryMap[address] = _ppu.Dma;
                            break;
                        case Ppu.BGP_ADDRESS:
                            _memoryMap[address] = _ppu.Bgp;
                            break;
                        case Ppu.OBP0_ADDRESS:
                            _memoryMap[address] = _ppu.Obp0;
                            break;
                        case Ppu.OBP1_ADDRESS:
                            _memoryMap[address] = _ppu.Obp1;
                            break;
                        case Ppu.WY_ADDRESS:
                            _memoryMap[address] = _ppu.Wy;
                            break;
                        case Ppu.WX_ADDRESS:
                            _memoryMap[address] = _ppu.Wx;
                            break;
                        case ushort n when (n >= 0xFE00 && n <= 0xFE9F): //OAM accessibiliy period
                            if (_ppu.PpuMode > 1)
                            {
                                return 0xFF;
                            }
                            break;
                        case ushort n when (n >= 0x8000 && n <= 0x9FFF): //VRAM accessibiliy period
                            if (_ppu.PpuMode > 2)
                            {
                                return 0xFF;
                            }
                            break;
                        default:
                            break;
                    }
                }
                else
                {
                    throw new Exception("PPU or Timers not specified");
                }

                return _memoryMap[address];
            }
            set
            {
                if (_timers != null && _ppu != null)
                {
                    switch (address)
                    {
                        case SerialTransfer.SB_ADDRESS:
                            _serialTranfer.Sb = value;
                            break;
                        case SerialTransfer.SC_ADDRESS:
                            _serialTranfer.Sc = value;
                            break;
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
                        case Ppu.LCDC_ADDRESS:
                            _ppu.Lcdc = value;
                            break;
                        case Ppu.STAT_ADDRESS:
                            _ppu.Stat = value;
                            break;
                        case Ppu.SCY_ADDRESS:
                            _ppu.Scy = value;
                            break;
                        case Ppu.SCX_ADDRESS:
                            _ppu.Scx = value;
                            break;
                        case Ppu.LY_ADDRESS:
                            _ppu.Ly = value;
                            break;
                        case Ppu.LYC_ADDRESS:
                            _ppu.Lyc = value;
                            break;
                        case Ppu.DMA_ADDRESS:
                            _ppu.Dma = value;
                            _ppu.StartDma();
                            break;
                        case Ppu.BGP_ADDRESS:
                            _ppu.Bgp = value;
                            break;
                        case Ppu.OBP0_ADDRESS:
                            _ppu.Obp0 = value;
                            break;
                        case Ppu.OBP1_ADDRESS:
                            _ppu.Obp1 = value;
                            break;
                        case Ppu.WY_ADDRESS:
                            _ppu.Wy = value;
                            break;
                        case Ppu.WX_ADDRESS:
                            _ppu.Wx = value;
                            break;
                        case ushort n when (n >= 0xFE00 && n <= 0xFE9F): //OAM accessibiliy period
                            if (_ppu.PpuMode <= 1)
                            {
                                _ppu.PpuMode = value;
                            }
                            break;
                        case ushort n when (n >= 0x8000 && n <= 0x9FFF): //VRAM accessibiliy period
                            if (_ppu.PpuMode <= 2)
                            {
                                _memoryMap[address] = value;
                            }
                            break;
                        default:
                            if (address > ROM_MAX_ADDRESS) //rom writes attempts are possible but must be ignored
                            {
                                _memoryMap[address] = value;
                            }
                            break;
                    }
                }
                else
                {
                    throw new Exception("PPU or Timers not specified");
                }
            }

        }

        private byte[] _romDump = Array.Empty<byte>();
        public Memory(string[] cartridgePath)
        {
            try
            {
                _romDump = _cartridge.LoadRomFromCartridge(cartridgePath);
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
        public void SetPpu(Ppu ppu)
        {
            _ppu = ppu;
        }

        private void initializeRom()
        {
            for (int i = 0; i < 0x7FFF && i < _romDump.Length; i++)
            {
                _memoryMap[i] = _romDump[i];
            }
        }

        //this methods are for the PPU to read directly to VRAM
        public byte ReadVramDirectly(ushort address)
        {
            return _memoryMap[address];
        }

        //this methods are for the PPU to read directly to OAM
        public byte ReadOamDirectly(ushort address)
        {
            return _memoryMap[address];
        }
    }
}
