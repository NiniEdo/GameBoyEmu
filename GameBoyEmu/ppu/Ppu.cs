using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.TimersNamespace;
using NLog;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.PpuNamespace
{
    public class Ppu
    {
        private Memory _memory;
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Interrupts _interrupts = Interrupts.GetInstance();

        public const ushort LCDC_ADDRESS = 0xFF40;
        public const ushort STAT_ADDRESS = 0xFF41;
        public const ushort SCY_ADDRESS = 0xFF42;
        public const ushort SCX_ADDRESS = 0xFF43;
        public const ushort LY_ADDRESS = 0xFF44;
        public const ushort LYC_ADDRESS = 0xFF45;
        public const ushort BGP_ADDRESS = 0xFF47;
        public const ushort OBP0_ADDRESS = 0xFF48;
        public const ushort OBP1_ADDRESS = 0xFF49;
        public const ushort WY_ADDRESS = 0xFF4A;
        public const ushort WX_ADDRESS = 0xFF4B;

        private byte _lcdc;
        private byte _stat;
        private byte _scy;
        private byte _scx;
        private byte _ly;
        private byte _lyc;
        private byte _bgp;
        private byte _obp0;
        private byte _obp1;
        private byte _wy;
        private byte _wx;

        private int _elapsedDots = 0;
        ushort _currentAddress = 0xFE00;

        public byte Lcdc { get => _lcdc; set => _lcdc = value; }
        public byte Stat { get => _stat; set => _stat = value; }
        public byte Scy { get => _scy; set => _scy = value; }
        public byte Scx { get => _scx; set => _scx = value; }
        public byte Ly { get => _ly; set => _ly = value; }
        public byte Lyc { get => _lyc; set => _lyc = value; }
        public byte Bgp { get => _bgp; set => _bgp = value; }
        public byte Obp0 { get => _obp0; set => _obp0 = value; }
        public byte Obp1 { get => _obp1; set => _obp1 = value; }
        public byte Wy { get => _wy; set => _wy = value; }
        public byte Wx { get => _wx; set => _wx = value; }

        private bool LcdAndPpuEnable { get => (byte)((_lcdc & 0b1000_0000) >> 7) == 1; }
        private byte WindowTileMap { get => (byte)((_lcdc & 0b0100_0000) >> 6); }
        private bool WindowEnable { get => (byte)((_lcdc & 0b0010_0000) >> 5) == 1; }
        private byte BgAndWindowTileDataArea { get => (byte)((_lcdc & 0b0001_0000) >> 4); }
        private byte BgTileMapArea { get => (byte)((_lcdc & 0b0000_1000) >> 3); }
        private byte SpriteSize { get => (byte)((_lcdc & 0b0000_0100) >> 2); }
        private bool SpriteEnable { get => (byte)((_lcdc & 0b0000_0010) >> 1) == 1; }
        private bool BgAndWindowEnable { get => (byte)((_lcdc & 0b0000_0001)) == 1; }

        private bool LycEqualsLyInterruptEnable { get => (_stat & 0b0100_0000) == 0b0100_0000; }
        private bool OamInterruptEnable { get => (_stat & 0b0010_0000) == 0b0010_0000; }
        private bool VBlankInterruptEnable { get => (_stat & 0b0001_0000) == 0b0001_0000; }
        private bool HBlankInterruptEnable { get => (_stat & 0b0000_1000) == 0b0000_1000; }
        private bool CoincidenceFlag { set => _stat = (byte)((_stat & 0b1111_1011) | (value ? 0b0000_0100 : 0)); }
        private byte PpuMode { set => _stat = (byte)((_stat & 0b1111_1100) | ((byte)value)); }

        struct Sprite
        {
            private byte _y;
            private byte _x;
            private byte _tileIndex;
            private byte _flags;

            public byte Y { get => _y; set => _y = value; }
            public byte X { get => _x; set => _x = value; }
            public byte TileIndex { get => _tileIndex; set => _tileIndex = value; }
            public byte Flags { set => _flags = value; get => _flags; }

            public byte Priority { get => (byte)((_flags & 0b1000_0000) >> 7); }
            public byte YFlip { get => (byte)((_flags & 0b0100_0000) >> 6); }
            public byte XFlip { get => (byte)((_flags & 0b0010_0000) >> 5); }
            public byte DmgPalette { get => (byte)((_flags & 0b0001_0000) >> 4); }
            public byte Bank { get => (byte)((_flags & 0b0000_1000) >> 3); }
            public byte CgbPalette { get => (byte)(_flags & 0b0000_0111); }
        }

        public Ppu(Memory memory)
        {
            _memory = memory;
        }

        public void Tick(int mCycles)
        {
            if (_ly == _lyc)
            {
                _interrupts.RequestStatInterrupt();
            }

            for (int i = 0; i < mCycles; i++)
            {
                _elapsedDots += 4;
                if (_elapsedDots == 80)
                {
                    OamScan();
                }
                else if (_elapsedDots == 172)
                {
                    DrawPixels();
                }
                else if (_elapsedDots == 456)
                {
                    HorizontalBlank();
                }
            }
        }

        private void OamScan()
        {
            PpuMode = 2;
            List<Sprite> sprites = new List<Sprite>();
            _currentAddress = 0xFE00;

            for (int i = 0; i < 40; i++)
            {
                Sprite spriteAttributes = FetchObjectAttributes(_currentAddress);

                _logger.Debug($"Fetched Object: Y={spriteAttributes.Y}, X={spriteAttributes.X}, TileIndex={spriteAttributes.TileIndex}, Flags={spriteAttributes.Flags}");

                int objectSize = SpriteSize == 0 ? 8 : 16;
                bool isSpriteVisible = spriteAttributes.X > 0 && _ly + 16 >= spriteAttributes.Y && _ly + 16 < (spriteAttributes.Y + objectSize) && sprites.Count < 10;

                if (isSpriteVisible && SpriteEnable)
                {
                    sprites.Add(spriteAttributes);
                }

                _currentAddress += 4;
            }
        }

        private void DrawPixels()
        {
            PpuMode = 3;
        }

        private void HorizontalBlank()
        {
            PpuMode = 0;
            _ly += 1;
            _elapsedDots = 0;
        }

        private Sprite FetchObjectAttributes(ushort _currentAddress)
        {
            Sprite ObjectAttributes = new Sprite();

            ObjectAttributes.Y = _memory.ReadOamDirectly(_currentAddress);
            ObjectAttributes.X = _memory.ReadOamDirectly((ushort)(_currentAddress + 1));
            ObjectAttributes.TileIndex = _memory.ReadOamDirectly((ushort)(_currentAddress + 2));
            ObjectAttributes.Flags = _memory.ReadOamDirectly((ushort)(_currentAddress + 3));

            return ObjectAttributes;
        }

    }
}
