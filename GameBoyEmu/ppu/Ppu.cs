using GameBoyEmu.interfaces;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.ppu;
using GameBoyEmu.TimersNamespace;
using NLog;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using GameBoyEmu.Utils;
using GameBoyEmu.ScreenNameSpace;

namespace GameBoyEmu.PpuNamespace
{
    public class Ppu : ITickable
    {
        private enum Mode
        {
            HBlank,
            VBlank,
            OAM,
            LCDTransfer,
        }

        private Memory _memory;
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Interrupts _interrupts = Interrupts.GetInstance();
        private Dma _dmaHandler = new Dma();
        private Screen _screen = Screen.GetInstance();

        public const ushort LCDC_ADDRESS = 0xFF40;
        public const ushort STAT_ADDRESS = 0xFF41;
        public const ushort SCY_ADDRESS = 0xFF42;
        public const ushort SCX_ADDRESS = 0xFF43;
        public const ushort LY_ADDRESS = 0xFF44;
        public const ushort LYC_ADDRESS = 0xFF45;
        public const ushort DMA_ADDRESS = 0xFF46;
        public const ushort BGP_ADDRESS = 0xFF47;
        public const ushort OBP0_ADDRESS = 0xFF48;
        public const ushort OBP1_ADDRESS = 0xFF49;
        public const ushort WY_ADDRESS = 0xFF4A;
        public const ushort WX_ADDRESS = 0xFF4B;

        private const int DOTS_PER_FRAME = 70224;

        private byte _lcdc;
        private byte _stat;
        private byte _scy;
        private byte _scx;
        private byte _ly;
        private byte _lyc;
        private byte _dma;
        private byte _bgp;
        private byte _obp0;
        private byte _obp1;
        private byte _wy;
        private byte _wx;

        private int _winY = 0;
        private int _windowsLineCounter = 0;
        private int _elapsedDots = 0;
        private int _totalElapsedDots = 0;

        FixedSizeQueue<byte> _backgroudFifo = new FixedSizeQueue<byte>(8);
        List<Sprite> _spritesBuffer = new List<Sprite>();
        Color[] _pixelBuffer = new Color[160];

        private static readonly Color[] Colors = {
            Color.FromArgb(0xE0, 0xF8, 0xD0),
            Color.FromArgb(0x88, 0xC0, 0x70),
            Color.FromArgb(0x34, 0x68, 0x56),
            Color.FromArgb(0x08, 0x18, 0x20)
        };


        public byte Lcdc { get => _lcdc; set => _lcdc = value; }
        public byte Stat
        {
            get => _stat;
            set => _stat = (byte)(_stat | (value & 0b1111_1000));
        }
        public byte Scy { get => _scy; set => _scy = value; }
        public byte Scx { get => _scx; set => _scx = value; }
        public byte Ly { get => _ly; }
        public byte Lyc { get => _lyc; set => _lyc = value; }
        public byte Dma { get => _dma; set => _dma = value; }
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

        private bool LycEqualsLyInterruptEnable { get => (_stat & 0b0100_0000) >> 6 != 0; }
        private bool OamInterruptEnable { get => (_stat & 0b0010_0000) >> 5 != 0; }
        private bool VBlankInterruptEnable { get => (_stat & 0b0001_0000) >> 4 != 0; }
        private bool HBlankInterruptEnable { get => (_stat & 0b0000_1000) >> 3 != 0; }
        private bool CoincidenceFlag { set => _stat = (byte)((_stat & 0b1111_1011) | (value ? 0b0000_0100 : 0)); }
        public byte PpuMode { set => _stat = (byte)((_stat & 0b1111_1100) | value); get => (byte)(_stat & 0b0000_0011); }

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

        public void Tick()
        {
            //if (!LcdAndPpuEnable)
            //{
            //    return;
            //}

            if (_ly == _lyc && LycEqualsLyInterruptEnable && _ly != 0)
            {
                CoincidenceFlag = true;
                _interrupts.RequestStatInterrupt();
            }
            else
            {
                CoincidenceFlag = false;
            }

            _elapsedDots += 4;
            _totalElapsedDots += 4;

            if (_ly >= 144) //VBlank
            {
                PpuMode = 1;
                if (VBlankInterruptEnable)
                    _interrupts.RequestStatInterrupt();
                VerticalBlank();
            }
            else if (_elapsedDots == 80) // OamScan
            {
                PpuMode = 2;
                if (OamInterruptEnable)
                    _interrupts.RequestStatInterrupt();
            }
            else if (_elapsedDots == 252) //DrawScanline
            {
                RenderScanline();
                PpuMode = 3;
            }
            else if (_elapsedDots == 456) //HBlank
            {
                PpuMode = 0;
                if (HBlankInterruptEnable)
                    _interrupts.RequestStatInterrupt();
                HorizontalBlank();
            }
        }

        public void StartDma()
        {
            _dmaHandler.Start();
        }

        public void RenderScanline()
        {
            RenderBackground();
            //RenderSprites();
            _screen.RenderScanline(_pixelBuffer, _ly);
        }

        private void RenderBackground()
        {
            if (!BgAndWindowEnable) //NOTE: Errore
            {
                //NOTE: Draw White?
                return;
            }

            ushort tileDataAddress = (ushort)(BgAndWindowTileDataArea == 0 ? 0x9000 : 0x8000);
            ushort backgroundTileMap = (ushort)(BgTileMapArea == 0 ? 0x9800 : 0x9C00);
            ushort windowTileMap = (ushort)(WindowTileMap == 0 ? 0x9800 : 0x9C00); // NOTE: Errore
            int windowX = _wx - 7;

            bool windowDrawn = false;
            bool canRenderWindow = _wy <= _ly && WindowEnable;

            byte tileX = _scx;
            byte tileY = (byte)(_ly + _scy); // NOTE: Errore
            byte offsetY = (byte)(tileY & 7); // NOTE: Errore
            ushort tilemap = backgroundTileMap;

            for (int x = 0; x < _pixelBuffer.Length; x++)
            {
                if (!windowDrawn && (canRenderWindow && windowX <= x))
                {
                    windowDrawn = true;
                    tileX = (byte)(x - windowX);
                    tileY = (byte)_winY;
                    offsetY = (byte)(tileY & 7);
                    tilemap = windowTileMap;
                }

                byte tileIndex = _memory.ReadVramDirectly((ushort)(tilemap + (tileY / 8 * 32) + (tileX / 8)));
                ushort offset = (ushort)((offsetY * 2) + (ushort)(BgAndWindowTileDataArea == 0 ? (sbyte)tileIndex * 16 : tileIndex * 16)); // NOTE: Errore
                ushort tileDataLow = _memory.ReadVramDirectly((ushort)(tileDataAddress + offset));
                ushort tileDataHigh = _memory.ReadVramDirectly((ushort)(tileDataAddress + offset + 1));

                byte offsetX = (byte)((tileX & 7) ^ 7);

                int color = ((tileDataHigh >> offsetX) & 1) << 1 | ((tileDataLow >> offsetX) & 1);

                _pixelBuffer[x] = Colors[color];
                tileX++;
            }

            _winY += windowDrawn ? 1 : 0;
        }

        private void HorizontalBlank()
        {
            if (WindowEnable && _ly >= _wy)
            {
                _windowsLineCounter++;
            }
            _ly += 1;
            _elapsedDots = 0;
        }

        private void VerticalBlank()
        {
            if (_ly == 144)
            {
                _windowsLineCounter = 0;
                _interrupts.RequestVblankInterrupt();
            }

            if (_totalElapsedDots >= DOTS_PER_FRAME)
            {
                _ly = 0;
                _totalElapsedDots = 0;
                _elapsedDots = 0;
            }
            else if (_elapsedDots == 456)
            {
                _ly += 1;
                _elapsedDots = 0;
            }

            _winY = 0;
        }

        private void SendToLcd()
        {
        }
    }
}
