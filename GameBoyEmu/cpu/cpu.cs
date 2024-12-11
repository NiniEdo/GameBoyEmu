using GameBoyEmu.Exceptions;
using NLog;
using GameBoyEmu.RomNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.CpuNamespace
{
    internal class Cpu
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        private static Cpu _cpu = new Cpu();
        byte[]? romDump;
        private Cpu()
        {
            try
            {
                romDump = _rom.loadFromCartridge();
            }
            catch (CartridgeException CAex)
            {
                logger.Fatal("Error: " + CAex.Message);
            }
        }

        public static Cpu GetCpu()
        {
            return _cpu;
        }

        byte[] AF = new byte[2];
        byte[] BC = new byte[2]; //000
        byte[] DE = new byte[2]; //001
        byte[] HL = new byte[2]; //010
        byte[] SP = new byte[2]; //011 
        byte[] PC = new byte[] { 0x00, 0x00 };

        byte instructionRegister;

        Rom _rom = Rom.GetRom();

        public void fetch()
        {
            if (romDump == null)
            {
                logger.Fatal("ROM dump is null. Cannot fetch instructions.");
                return;
            }

            ushort pcValue = (ushort)((PC[0] << 8) | PC[1]);
            while (pcValue < romDump.Length)
            {
                instructionRegister = romDump[pcValue];
                logger.Debug($"Current instruction fetched {pcValue}: {instructionRegister}");
                decode(ref pcValue);
            }

            PC[0] = (byte)(pcValue >> 8);
            PC[1] = (byte)(pcValue & 0xFF);
        }

        void decode(ref ushort pcValue)
        {
            pcValue++;
        }

    }
}
