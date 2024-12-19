using GameBoyEmu.Exceptions;
using NLog;
using GameBoyEmu.RomNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.FlagsHelperNamespace;
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
using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using static GameBoyEmu.CpuNamespace.Cpu;

[assembly: InternalsVisibleTo("GameBoyEmuTests")]
namespace GameBoyEmu.CpuNamespace
{
    class Cpu
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Memory _memory = Memory.GetMemory();
        private FlagsHelper _flags;

        //16 bits
        private byte[] _AF = new byte[2];
        private byte[] _BC = new byte[2];
        private byte[] _DE = new byte[2];
        private byte[] _HL = new byte[2];
        private byte[] _SP = new byte[2];
        private byte[] _PC = new byte[2] { 0x00, 0x00 };

        private byte _instructionRegister = 0x00;

        private readonly Dictionary<paramsType, List<byte[]>> _16bitsRegistries;
        private readonly Dictionary<paramsType, List<byte>> _8bitsRegistries;
        private readonly Dictionary<byte, Dictionary<byte, Instruction>> _instructionSetBlocks;
        private readonly Dictionary<byte, Instruction> _instructionSetBlockZero;

        public Cpu()
        {
            _flags = new FlagsHelper(ref _AF);

            _16bitsRegistries = new()
            {
                {paramsType.r16, new List<byte[]>{_BC, _DE, _HL, _SP} },
                {paramsType.r16stk, new List<byte[]>{_BC, _DE, _HL, _AF} },
                {paramsType.r16mem, new List<byte[]>{_BC, _DE, _HL, _HL } },
            };

            _8bitsRegistries = new()
            {
                {paramsType.r8, new List<byte>{_BC[0], _BC[1], _DE[0], _DE[1], _HL[0], _HL[1], _memory.memoryMap[BitConverter.ToUInt16(_HL, 0)], _AF[0] } },
            };

            _instructionSetBlockZero = new Dictionary<byte, Instruction>
            {
                //last 4 bits -> Instruction
                { 0b0000, new Instruction("NOP", 1, () =>
                {
                        _logger.Debug($"Instruction Fetched: {"NOP"}");
                        return;
                    })
                },
                { 0b0001, new Instruction("LD r16, imm16", 3, () =>
                    {
                        // Fetch the next two bytes for the immediate 16-bit value
                        byte lowByte = Fetch();
                        byte highByte = Fetch();


                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        _logger.Debug($"Instruction Fetched: {"LD r16, imm16"} with params: {lowByte}, {highByte} and register {registerCode}");

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            register[0] = highByte;
                            register[1] = lowByte;
                        }else {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }

                    })
                },
                { 0b0010, new Instruction("LD [r16mem], A", 4 , () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if(registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[0] << 8) | register[1]);

                            _logger.Debug($"Instruction Fetched: {"LD [r16mem], A"}, register {registerCode}");

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS-1)
                            {
                                _memory.memoryMap[memoryPointer] = _AF[0];
                            }

                            if (registerCode == 0b010)
                            {
                                ushort value = (ushort)((_HL[0] << 8) | _HL[1]);
                                value++;
                                _HL[0] = (byte)(value >> 8);
                                _HL[1] = (byte)(value & 0xFF);

                            }else if (registerCode == 0b011)
                            {
                                ushort value = (ushort)((_HL[0] << 8) | _HL[1]);
                                value--;
                                _HL[0] = (byte)(value >> 8);
                                _HL[1] = (byte)(value & 0xFF);
                            }

                        }else{
                            throw new InstructionExcecutionException("[LD [r16mem], A] an error occurred");
                        }

                    })
                },
                { 0b1010, new Instruction("LD A, [r16mem]", 2, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if(registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[0] << 8) | register[1]);

                            _logger.Debug($"Instruction Fetched: {"LD A, [r16mem]"}, register {registerCode}");

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS)
                            {
                                _AF[0] = _memory.memoryMap[memoryPointer];
                            }

                            if (registerCode == 0b010)
                            {
                                ushort value = (ushort)((_HL[0] << 8) | _HL[1]);
                                value++;
                                _HL[0] = (byte)(value >> 8);
                                _HL[1] = (byte)(value & 0xFF);

                            }else if (registerCode == 0b011)
                            {
                                ushort value = (ushort)((_HL[0] << 8) | _HL[1]);
                                value--;
                                _HL[0] = (byte)(value >> 8);
                                _HL[1] = (byte)(value & 0xFF);
                            }
                        }else{
                            throw new InstructionExcecutionException("[LD A, [r16mem]] an error occurred");
                        }
                    })
                },
                { 0b1000, new Instruction("LD [imm16], SP", 5, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        _logger.Debug($"Instruction Fetched: {"LD [imm16], SP"} with params: {lowByte}, {highByte}");

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);
                        if (memoryPointer + 1 <= Memory.MEM_MAX_ADDRESS)
                        {
                            _memory.memoryMap[memoryPointer] = _SP[0];
                            _memory.memoryMap[memoryPointer + 1] = _SP[1];
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD [imm16], SP] an error occurred");
                        }
                    })
                },
                { 0b0011, new Instruction("INC r16", 2, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        _logger.Debug($"Instruction Fetched: {"INC r16"}, register {registerCode}");

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];

                            ushort registerValue = (ushort)((register[0] << 8) | register[1]);
                            registerValue++;
                            register[0] = (byte)(registerValue >> 8);
                            register[1] = (byte)(registerValue & 0xFF);

                        }else {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }
                    })
                },
                { 0b1011, new Instruction("DEC r16", 2, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        _logger.Debug($"Instruction Fetched: {"DEC r16"}, register {registerCode}");

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];

                            ushort registerValue = (ushort)((register[0] << 8) | register[1]);
                            registerValue--;
                            register[0] = (byte)(registerValue >> 8);
                            register[1] = (byte)(registerValue & 0xFF);
                        }else {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }
                    })
                },
                { 0b1011, new Instruction("add HL, r16", 2, () =>
                    {
                        //ADD HL,r16
                        //Add the value in r16 to HL.
                        //Cycles: 2
                        //Bytes: 1
                        //Flags:
                        //N 0
                        //H Set if overflow from bit 11.
                        //C Set if overflow from bit 15.
                    })
                },
            };

            _instructionSetBlocks = new Dictionary<byte, Dictionary<byte, Instruction>>
            {
                { 0b00,  _instructionSetBlockZero},
            };
        }

        internal struct Instruction
        {
            public readonly string Name;
            public readonly int Cycles;
            public readonly Action Execute;
            public Instruction(string name, int cycles, Action execute)
            {
                Name = name;
                Cycles = cycles;
                Execute = execute;
            }
        }

        internal enum paramsType
        {
            r8,
            r16,
            r16stk,
            r16mem,
        }

        byte Fetch()
        {
            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);

            if (pcValue > Memory.ROM_MAX_ADDRESS)
            {
                _logger.Fatal("Attempted to fetch beyond ROM boundaries.");
                throw new IndexOutOfRangeException("Program Counter exceeded ROM boundaries.");
            }

            byte nextByte = _memory.memoryMap[pcValue];

            pcValue++;

            _PC[0] = (byte)(pcValue >> 8);
            _PC[1] = (byte)(pcValue & 0xFF);

            return nextByte;
        }

        private void Decode()
        {
            Instruction instruction;
            try
            {
                //here it will bind the opcode with the parameter fetching it and executing the code
                byte block = (byte)(_instructionRegister & (0b1100_0000)); // read the instruction block
                Dictionary<byte, Instruction> _instructionSetOfCurrentBlock = _instructionSetBlocks[block];

                byte lastFourByteOfinstruction = (byte)(_instructionRegister & (0b0000_1111)); // read the instruction
                instruction = _instructionSetOfCurrentBlock[lastFourByteOfinstruction];
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error while decoding the instruction {e.Message}");
                return;
            }

            //Execute the intruction
            try
            {
                instruction.Execute();
            }
            catch (InstructionExcecutionException IntrExEx)
            {
                _logger.Fatal(IntrExEx.Message);
                throw new InstructionExcecutionException();
            }
        }

        public void Execute()
        {
            ushort pcValue = (ushort)((_PC[0] << 8) | _PC[1]);
            while (pcValue <= Memory.ROM_MAX_ADDRESS)
            {
                byte data = Fetch();
                _instructionRegister = data;
                try
                {
                    Decode();
                }
                catch (InstructionExcecutionException)
                {
                    break;
                }
                pcValue = (ushort)((_PC[0] << 8) | _PC[1]);
                _logger.Debug($"Values after operation: AF: {BitConverter.ToString(_AF)}, " +
                    $"BC: {BitConverter.ToString(_BC)}, DE: {BitConverter.ToString(_DE)}, " +
                    $"HL: {BitConverter.ToString(_HL)}, SP: {BitConverter.ToString(_SP)}, " +
                    $"PC: {BitConverter.ToString(_PC)}");
            }
        }
    }
}
