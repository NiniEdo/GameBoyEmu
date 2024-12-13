using GameBoyEmu.Exceptions;
using NLog;
using GameBoyEmu.RomNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Reflection.Emit;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Net.NetworkInformation;

namespace GameBoyEmu.CpuNamespace
{
    internal class Cpu
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        private static Cpu _cpu = new Cpu();
        private byte[] _romDump = Array.Empty<byte>();

        struct Instruction
        {
            public readonly string Name;
            public readonly Action Execute;
            public Instruction(string name, Action execute)
            {
                Name = name;
                Execute = execute;
            }

        }

        private static readonly Dictionary<byte, Instruction> _instructionSetBlockZero = new Dictionary<byte, Instruction>
        {
            //last 4 bits -> Instruction
            { 0b0000, new Instruction("NOP", () => { return; }) },
            { 0b0001, new Instruction("LD r16, imm16", () => { /* LD r16, imm16 implementation */ }) },
            //{ 0x02, new Instruction("LD (BC), A", () => { /* LD (BC), A implementation */ }) },
        };

        private static readonly Dictionary<byte, Dictionary<byte, Instruction>> _instructionSetBlocks = new Dictionary<byte, Dictionary<byte, Instruction>>
        {
            { 0b00,  _instructionSetBlockZero},
            //{ }
        };

        private Cpu()
        {
            try
            {
                _romDump = _rom.loadFromCartridge();
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

        private static byte[] _AF = new byte[2];
        private static byte[] _BC = new byte[2]; //000
        private static byte[] _DE = new byte[2]; //001
        private static byte[] _HL = new byte[2]; //010
        private static byte[] _SP = new byte[2]; //011 
        private static byte[] _PC = new byte[] { 0x00, 0x00 };

        private static readonly Dictionary<byte, byte[]> _16bitsRegistries = new Dictionary<byte, byte[]>
        {
            {0b00, _BC },
            {0b01, _DE },
            {0b10, _HL },
            {0b11, _SP },
        };

        byte _instructionRegister;

        Rom _rom = Rom.GetRom();

        byte fetch()
        {
            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);

            if (pcValue >= _romDump.Length)
            {
                logger.Fatal("Attempted to fetch beyond ROM boundaries.");
                throw new IndexOutOfRangeException("Program Counter exceeded ROM boundaries.");
            }

            byte nextByte = _romDump[pcValue];

            pcValue++;

            _PC[0] = (byte)(pcValue >> 8);
            _PC[1] = (byte)(pcValue & 0xFF);

            return nextByte;
        }

        private void decode()
        {
            Instruction instruction;
            try
            {
                //here it will bind the opcode with the parameter fetching it and executing the code
                byte block = (byte)(_instructionRegister & (0b1100_0000 >> 7)); // read the instruction block
                Dictionary<byte, Instruction> _instructionSetOfCurrentBlock = _instructionSetBlocks[block];

                byte lastFourByteOfinstruction = (byte)(_instructionRegister & (0b0011_0000 >> 3)); // read the instruction
                instruction = _instructionSetOfCurrentBlock[lastFourByteOfinstruction];
                logger.Debug($"Instruction Fetched: {instruction.Name}");
            }
            catch (Exception e)
            {
                logger.Fatal($"Error while decoding the instriction {e.Message}");
                return;
            }

            //Execute the intruction
            instruction.Execute();

        }

        public void execute()
        {
            if (_romDump.Length == 0)
            {
                logger.Fatal("ROM dump is null. Cannot fetch instructions.");
                return;
            }

            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);

            while (pcValue < _romDump.Length - 1)
            {
                byte data = fetch();
                _instructionRegister = data;
                decode();
                pcValue = (ushort)((_PC[0] << 8) | _PC[1]);
            }
        }

    }
}
