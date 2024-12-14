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
using System.Collections;

namespace GameBoyEmu.CpuNamespace
{
    internal class Cpu
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private static Cpu _cpu = new Cpu();

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

        struct Flags
        {
            private static byte _F = 0b0000_0000;

            public static void setZeroFlag(bool value)
            {
                int intValue = value ? 1 : 0;
                _F = (byte)((_F & 0b0111_1111) | (intValue << 7));
            }
            public static void setSubtractionFlag(bool value)
            {
                int intValue = value ? 1 : 0;
                _F = (byte)((_F & 0b1011_1111) | (intValue << 6));
            }
            public static void setHalfCarryFlag(bool value)
            {
                int intValue = value ? 1 : 0;
                _F = (byte)((_F & 0b1101_1111) | (intValue << 5));
            }
            public static void setCarryFlag(bool value)
            {
                int intValue = value ? 1 : 0;
                _F = (byte)((_F & 0b1110_1111) | (intValue << 4));
            }

            public static byte getZeroFlag()
            {
                return (byte)((_F & 0b1000_0000) >> 7);
            }
            public static byte getSubtractionFlag()
            {
                return (byte)((_F & 0b0100_0000) >> 6);
            }
            public static byte getHalfCarryFlag()
            {
                return (byte)((_F & 0b0010_0000) >> 5);
            }
            public static byte getCarryFlag()
            {
                return (byte)((_F & 0b0001_0000) >> 4);
            }
        }


        //8 bits
        private static byte _A;
        //16 bits
        private static byte[] _BC = new byte[2]; //000
        private static byte[] _DE = new byte[2]; //001
        private static byte[] _HL = new byte[2]; //010
        private static byte[] _SP = new byte[2]; //011 
        private static byte[] _PC = new byte[2] { 0x00, 0x00 };

        private static readonly Dictionary<byte, byte[]> _16bitsRegistries = new Dictionary<byte, byte[]>
        {
            {0b00, _BC },
            {0b01, _DE },
            {0b10, _HL },
            {0b11, _SP },
        };

        byte _instructionRegister;


        private static readonly Dictionary<byte, Instruction> _instructionSetBlockZero = new Dictionary<byte, Instruction>
        {
            //last 4 bits -> Instruction
            { 0b0000, new Instruction("NOP", () => { return; }) },
            { 0b0001, new Instruction("LD r16, imm16", () =>
                {
                    // Fetch the next two bytes for the immediate 16-bit value
                    byte lowByte = _cpu.fetch();
                    byte highByte = _cpu.fetch();

                    byte registerCode = (byte)((_cpu._instructionRegister & 0b0011_0000) >> 4);
                    if (_16bitsRegistries.TryGetValue(registerCode, out byte[]? register))
                    {
                        register[0] = highByte;
                        register[1] = lowByte;
                    }
                    else
                    {
                        _cpu._logger.Fatal("Invalid 16-bit register code.");
                    }
                })
            },
            //{ 0x02, new Instruction("LD (BC), A", () => { /* LD (BC), A implementation */ }) },
        };

        private static readonly Dictionary<byte, Dictionary<byte, Instruction>> _instructionSetBlocks = new Dictionary<byte, Dictionary<byte, Instruction>>
        {
            { 0b00,  _instructionSetBlockZero},
        };

        private Cpu()
        {}

        public static Cpu GetCpu()
        {
            return _cpu;
        }

        byte fetch()
        {
            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);

            if (pcValue >= _romDump.Length)
            {
                _logger.Fatal("Attempted to fetch beyond ROM boundaries.");
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
                byte block = (byte)(_instructionRegister & (0b1100_0000)); // read the instruction block
                Dictionary<byte, Instruction> _instructionSetOfCurrentBlock = _instructionSetBlocks[block];

                byte lastFourByteOfinstruction = (byte)(_instructionRegister & (0b0011_0000 >> 4)); // read the instruction
                instruction = _instructionSetOfCurrentBlock[lastFourByteOfinstruction];
                _logger.Debug($"Instruction Fetched: {instruction.Name}");
            }
            catch (Exception e)
            {
                _logger.Error($"Error while decoding the instriction {e.Message}");
                return;
            }

            //Execute the intruction
            instruction.Execute();
        }

        public void execute()
        {
            if (_romDump.Length == 0)
            {
                _logger.Fatal("ROM dump is null. Cannot fetch instructions.");
                return;
            }

            //_romDump = new byte[] { 0x01, 0x34, 0x12, 0xFF, 0xA0, 0x5B, 0x9C, 0x00 };

            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);
            while (pcValue < _romDump.Length)
            {
                byte data = fetch();
                _instructionRegister = data;
                decode();
                pcValue = (ushort)((_PC[0] << 8) | _PC[1]);
            }
        }

    }
}
