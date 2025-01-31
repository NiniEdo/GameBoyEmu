using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.TimersNamespace;
using NLog.LayoutRenderers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.PpuNamespace
{
    internal class Ppu
    {
        private Memory _memory;
        private Timers _timers = Timers.GetInstance();

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

        public Ppu(Memory memory)
        {
            _memory = memory;
        }

        public void Tick(int mCycles)
        {

        }
    }
}
