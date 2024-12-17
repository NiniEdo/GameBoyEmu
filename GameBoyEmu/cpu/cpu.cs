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

namespace GameBoyEmu.CpuNamespace
{
    public class Cpu
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Memory _memory = Memory.GetMemory();
        private FlagsHelper _flags;

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
                { 0b0000, new Instruction("NOP", () =>
                    {
                        return;
                    })
                },
                { 0b0001, new Instruction("LD r16, imm16", () =>
                    {
                        // Fetch the next two bytes for the immediate 16-bit value
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];
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
                { 0b0010, new Instruction("LD [r16mem], A", () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if(registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[0] << 8) | register[1]);
                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS-1)
                            {
                                _memory.memoryMap[memoryPointer] = _AF[0];
                            }
                        }else{
                            throw new InstructionExcecutionException("[LD [r16mem], A] an error occurred");
                        }
                        CorrectForHlPlusOrMinus(registerCode);
                    })
                },
                { 0b1010, new Instruction("LD A, [r16mem]", () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if(registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[0] << 8) | register[1]);
                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS)
                            {
                                _AF[0] = _memory.memoryMap[memoryPointer];
                            }
                        }else{
                            throw new InstructionExcecutionException("[LD A, [r16mem]] an error occurred");
                        }

                        CorrectForHlPlusOrMinus(registerCode);
                    })
                },
                { 0b1000, new Instruction("LD [imm16], SP", () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((lowByte << 8) | highByte);
                        if (memoryPointer+1 <= Memory.MEM_MAX_ADDRESS)
                        {
                            _memory.memoryMap[memoryPointer] = lowByte;
                            _memory.memoryMap[memoryPointer+1] = highByte;
                        }else{
                            throw new InstructionExcecutionException("[LD [imm16], SP] an error occurred");
                        }
                    })
                },
            };

            _instructionSetBlocks = new Dictionary<byte, Dictionary<byte, Instruction>>
            {
                { 0b00,  _instructionSetBlockZero},
            };
        }

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

        //16 bits
        private byte[] _AF = new byte[2];
        private byte[] _BC = new byte[2];
        private byte[] _DE = new byte[2];
        private byte[] _HL = new byte[2];
        private byte[] _SP = new byte[2];
        private byte[] _PC = new byte[2] { 0x00, 0x00 }; //TODO RESET TO 0000 0000


        private byte _instructionRegister = 0x00;


        enum paramsType
        {
            r8,
            r16,
            r16stk,
            r16mem,
        }

        private void CorrectForHlPlusOrMinus(byte registerCode)
        {
            int hlValue = (ushort)((_HL[0] << 8) | _HL[1]);
            int originalHlValue = hlValue;

            if (registerCode == 0b010)
            {
                hlValue++;
                _HL[0] = (byte)(hlValue >> 8);
                _HL[1] = (byte)(hlValue & 0xFF);
                _flags.setSubtractionFlag(false);
                _flags.setHalfCarryFlag((byte)(originalHlValue & 0xFF), 1, true);
            }
            else if (registerCode == 0b011)
            {
                hlValue--;
                _HL[0] = (byte)(hlValue >> 8);
                _HL[1] = (byte)(hlValue & 0xFF);
                _flags.setSubtractionFlag(true);
                _flags.setHalfCarryFlag((byte)(originalHlValue & 0xFF), 1, false);
            }

            _flags.setCarryFlag(hlValue, true, false);
            _flags.setZeroFlag((uint)hlValue);
        }

        public void Reset()
        {
            _AF = new byte[2];
            _BC = new byte[2];
            _DE = new byte[2];
            _HL = new byte[2];
            _SP = new byte[2];
            _PC = new byte[2] { 0x00, 0x00 };
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

                byte lastFourByteOfinstruction = (byte)(_instructionRegister & (0b0011_0000 >> 4)); // read the instruction
                instruction = _instructionSetOfCurrentBlock[lastFourByteOfinstruction];
                _logger.Debug($"Instruction Fetched: {instruction.Name}");
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
            }
        }
    }
}
