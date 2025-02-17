using GameBoyEmu.Exceptions;
using NLog;
using GameBoyEmu.CartridgeNamespace;
using GameBoyEmu.MemoryNamespace;
using GameBoyEmu.FlagsHelperNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Reflection.Emit;
using static System.Runtime.InteropServices.JavaScript.JSType;
using System.Runtime.Intrinsics.X86;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Win32;
using GameBoyEmu.InterruptNamespace;
using GameBoyEmu.TimersNamespace;
using System.Reflection.PortableExecutable;
using GameBoyEmu.MachineCyclesNamespace;
using GameBoyEmu.ScreenNameSpace;
using GameBoyEmu.gameboy;


namespace GameBoyEmu.CpuNamespace
{
    class ByteRegister
    {
        private byte[] _array;
        private int _index;

        public ByteRegister(byte[] array, int index)
        {
            _array = array;
            _index = index;
        }

        public byte Value
        {
            get => _array[_index];
            set => _array[_index] = value;
        }
    }
    public class Cpu
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Memory _memory;
        private FlagsHelper _flags;
        private Interrupts _interruptsManager;
        private MachineCycles _machineCycles;

        //16 bits
        private byte[] _AF = new byte[2] { 0x80, 0x01 };
        private byte[] _BC = new byte[2] { 0x13, 0x00 };
        private byte[] _DE = new byte[2] { 0xD8, 0x00 };
        private byte[] _HL = new byte[2] { 0x4D, 0x01 };
        private byte[] _SP = new byte[2] { 0xFE, 0xFF };
        private byte[] _PC = new byte[2] { 0x00, 0x01 };

        private byte _instructionRegister = 0x00;

        private ushort _mCycleCounter = 0;
        private bool keepRunning = true;

        private readonly Dictionary<paramsType, List<byte[]>> _16bitsRegistries;
        private readonly Dictionary<paramsType, List<ByteRegister?>> _8bitsRegistries;

        public byte[] AF { get => _AF; set => _AF = value; }
        public byte[] BC { get => _BC; set => _BC = value; }
        public byte[] DE { get => _DE; set => _DE = value; }
        public byte[] HL { get => _HL; set => _HL = value; }
        public byte[] SP { get => _SP; set => _SP = value; }
        public byte[] PC { get => _PC; set => _PC = value; }
        public byte InstructionRegister { get => _instructionRegister; set => _instructionRegister = value; }
        public bool KeepRunning { get => keepRunning; set => keepRunning = value; }

        public Cpu(Memory memory)
        {
            _flags = new FlagsHelper(ref _AF);
            _interruptsManager = Interrupts.GetInstance();
            _machineCycles = MachineCycles.GetInstance();
            _memory = memory;
            _16bitsRegistries = new()
            {
                {paramsType.r16, new List<byte[]>{_BC, _DE, _HL, _SP} },
                {paramsType.r16stk, new List<byte[]>{_BC, _DE, _HL, _AF} },
                {paramsType.r16mem, new List<byte[]>{_BC, _DE, _HL, _HL } },
            };


            _8bitsRegistries = new()
            {
                {
                    paramsType.r8, new List<ByteRegister?>
                    {
                        new ByteRegister(_BC, 1),
                        new ByteRegister(_BC, 0),
                        new ByteRegister(_DE, 1),
                        new ByteRegister(_DE, 0),
                        new ByteRegister(_HL, 1),
                        new ByteRegister(_HL, 0),
                        null, //null because it's [hl] and will be handled separatly
                        new ByteRegister(_AF, 1),
                    }
                }
            };
        }

        private void Push(byte highByte, byte lowByte)
        {
            ushort sp = (ushort)((_SP[1] << 8) | _SP[0]);

            sp--;
            _memory[sp] = highByte;

            sp--;
            _memory[sp] = lowByte;

            _SP[0] = (byte)(sp & 0xFF);
            _SP[1] = (byte)(sp >> 8);
        }


        private void Pop(out byte highByte, out byte lowByte)
        {
            ushort sp = (ushort)((_SP[1] << 8) | _SP[0]);

            lowByte = _memory[sp];
            sp++;

            highByte = _memory[sp];
            sp++;

            _SP[0] = (byte)(sp & 0xFF);
            _SP[1] = (byte)(sp >> 8);
        }

        private bool getCc(byte cond)
        {
            switch (cond)
            {
                case 0b00: // NZ
                    return _flags.GetZeroFlagZ() == 0;
                case 0b01: // Z
                    return _flags.GetZeroFlagZ() == 1;
                case 0b10: // NC
                    return _flags.GetCarryFlagC() == 0;
                case 0b11: // C
                    return _flags.GetCarryFlagC() == 1;
                default:
                    return false;
            }
        }

        private Instruction? LookUpBlockZero(byte opcode)
        {
            // find by 8 bits identifier
            switch (opcode)
            {
                case 0b0000_0000:
                    return new Instruction("NOP", 1, () =>
                    {
                        return;
                    });
                case 0b0000_1000:
                    return new Instruction("LD [imm16], SP", 5, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);
                        if (memoryPointer + 1 <= Memory.MEM_MAX_ADDRESS)
                        {
                            _memory[memoryPointer] = _SP[0];
                            _memory[(ushort)(memoryPointer + 1)] = _SP[1];
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD [imm16], SP] an error occurred");
                        }
                    });
                case 0b0000_0111:
                    return new Instruction("rlca", 1, () =>
                    {
                        byte carryOut = (byte)((_AF[1] & 0b1000_0000) >> 7);
                        _AF[1] = (byte)((_AF[1] << 1) | carryOut);

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(carryOut);
                    });
                case 0b0000_1111:
                    return new Instruction("rrca", 1, () =>
                    {
                        byte carryOut = (byte)((_AF[1] & 0b0000_0001));
                        _AF[1] = (byte)((_AF[1] >> 1) | (carryOut << 7));

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(carryOut);
                    });
                case 0b0001_0111:
                    return new Instruction("rla", 1, () =>
                    {
                        byte carryFlagValue = _flags.GetCarryFlagC();
                        byte carryOut = (byte)((_AF[1] & 0b1000_0000) >> 7);

                        _AF[1] = _AF[1] = (byte)((_AF[1] << 1) | carryFlagValue);

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(carryOut);
                    });
                case 0b0001_1111:
                    return new Instruction("rra", 1, () =>
                    {
                        byte carryFlagValue = _flags.GetCarryFlagC();
                        byte carryOut = (byte)((_AF[1] & 0b0000_0001));

                        _AF[1] = (byte)((_AF[1] >> 1) | (carryFlagValue << 7));

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(carryOut);
                    });
                case 0b0010_0111:
                    return new Instruction("daa", 1, () =>
                    {
                        byte A = _AF[1];
                        byte correction = 0;
                        bool setCarry = false;

                        if (_flags.GetSubtractionFlagN() == 0)
                        {
                            if (_flags.GetHalfCarryFlagH() == 1 || (A & 0x0F) > 9)
                            {
                                correction += 0x06;
                            }
                            if (_flags.GetCarryFlagC() == 1 || A > 0x99)
                            {
                                correction += 0x60;
                                setCarry = true;
                            }
                            A += correction;
                        }
                        else
                        {
                            if (_flags.GetHalfCarryFlagH() == 1)
                            {
                                correction += 0x06;
                            }
                            if (_flags.GetCarryFlagC() == 1)
                            {
                                correction += 0x60;
                                setCarry = true;
                            }
                            A -= correction;
                        }

                        _flags.SetZeroFlagZ(A);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(setCarry ? 1 : 0);

                        _AF[1] = A;

                    });
                case 0b0010_1111:
                    return new Instruction("cpl", 1, () =>
                    {
                        _AF[1] = (byte)~_AF[1];
                        _flags.SetSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(1);
                    });
                case 0b0011_0111:
                    return new Instruction("scf", 1, () =>
                    {
                        _flags.SetCarryFlagC(1);

                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                    });
                case 0b0011_1111:
                    return new Instruction("ccf", 1, () =>
                    {
                        byte value = (byte)(_flags.GetCarryFlagC() == 0 ? 1 : 0);
                        _flags.SetCarryFlagC(value);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                    });
                case 0b0001_1000:
                    return new Instruction("jr imm8", 3, () =>
                    {
                        sbyte offest = (sbyte)Fetch();
                        ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

                        ushort newPcValue = (ushort)(pcValue + offest);

                        _PC[0] = (byte)(newPcValue & 0xFF);
                        _PC[1] = (byte)(newPcValue >> 8);
                    });
                case 0b0001_0000:
                    return new Instruction("stop", 0, () =>
                    {
                        _memory[Timers.DIV_ADDRESS] = 0x00;
                        while (GameBoy.IsRunning)
                        {
                            Screen.ListenForEvents(ref GameBoy.IsRunning);

                            byte result = (byte)(_interruptsManager.IE & _interruptsManager.IF);
                            byte joypad = (byte)((result & 0b0000_1000) >> 4);
                            if (joypad != 0)
                            {
                                break;
                            }
                        }
                    });
                default:
                    break;
            }

            // find by 4 bits identifier
            switch (opcode & 0b0000_1111)
            {
                case 0b0001:
                    return new Instruction("LD r16, imm16", 3, () =>
                    {
                        // Fetch the next two bytes for the immediate 16-bit value
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            register[0] = lowByte;
                            register[1] = highByte;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }
                    });
                case 0b0010:
                    return new Instruction("LD [r16mem], A", 2, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[1] << 8) | register[0]);

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS - 1)
                            {
                                _memory[memoryPointer] = _AF[1];
                            }

                            if (registerCode == 0b010)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                value++;
                                _HL[0] = (byte)(value & 0xFF);
                                _HL[1] = (byte)(value >> 8);

                            }
                            else if (registerCode == 0b011)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                value--;
                                _HL[0] = (byte)(value & 0xFF);
                                _HL[1] = (byte)(value >> 8);
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD [r16mem], A] an error occurred");
                        }
                    });
                case 0b1010:
                    return new Instruction("LD A, [r16mem]", 2, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16mem];

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            ushort memoryPointer = (ushort)((register[1] << 8) | register[0]);

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS)
                            {
                                _AF[1] = _memory[memoryPointer];
                            }

                            if (registerCode == 0b010)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                value++;
                                _HL[0] = (byte)(value & 0xFF);
                                _HL[1] = (byte)(value >> 8);

                            }
                            else if (registerCode == 0b011)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                value--;
                                _HL[0] = (byte)(value & 0xFF);
                                _HL[1] = (byte)(value >> 8);
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD A, [r16mem]] an error occurred");
                        }
                    });

                case 0b0011:
                    return new Instruction("INC r16", 2, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];

                            ushort registerValue = (ushort)((register[1] << 8) | register[0]);
                            registerValue++;
                            register[0] = (byte)(registerValue & 0xFF);
                            register[1] = (byte)(registerValue >> 8);

                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }
                    });
                case 0b1011:
                    return new Instruction("DEC r16", 2, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];

                            ushort registerValue = (ushort)((register[1] << 8) | register[0]);
                            registerValue--;
                            register[0] = (byte)(registerValue & 0xFF);
                            register[1] = (byte)(registerValue >> 8);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD r16, imm16] an error occurred");
                        }
                    });
                case 0b1001:
                    return new Instruction("ADD HL, r16", 2, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                        if (registerCode < registries.Count)
                        {

                            byte[] register = registries[registerCode];
                            ushort registerValue = (ushort)((register[1] << 8) | register[0]);

                            ushort oldHLvalue = (ushort)((_HL[1] << 8) | _HL[0]);

                            ushort HLValue = (ushort)(oldHLvalue + registerValue);

                            _HL[0] = (byte)(HLValue & 0xFF);
                            _HL[1] = (byte)(HLValue >> 8);

                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(oldHLvalue, registerValue, true, true);
                            _flags.SetCarryFlagC(registerValue + oldHLvalue, true, false);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[ADD HL, r16] an error occurred");
                        }
                    });
                default:
                    break;
            }

            //find by 3 bits identifier
            switch (opcode & 0b0000_0111)
            {
                case 0b100:
                    return new Instruction("inc r8", 1, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_1000) >> 3);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte newValue;

                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory[value];
                                newValue++;
                                _memory[value] = newValue;

                                _mCycleCounter += 2;
                            }
                            else
                            {
                                registries[registerCode]!.Value = (byte)(registries[registerCode]!.Value + 1);
                                newValue = registries[registerCode]!.Value;
                            }

                            _flags.SetHalfCarryFlagH((ushort)(newValue - 1), 1, true, false);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetZeroFlagZ(newValue);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[inc r8] an error occurred");
                        }
                    });
                case 0b101:
                    return new Instruction("dec r8", 1, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_1000) >> 3);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte newValue;
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory[value];
                                newValue--;
                                _memory[value] = newValue;

                                _mCycleCounter += 2;
                            }
                            else
                            {
                                registries[registerCode]!.Value = (byte)(registries[registerCode]!.Value - 1);
                                newValue = registries[registerCode]!.Value;
                            }
                            _flags.SetHalfCarryFlagH((ushort)(newValue + 1), 1, false, false);
                            _flags.SetSubtractionFlagN(1);
                            _flags.SetZeroFlagZ(newValue);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[dec r8] an error occurred");
                        }
                    });
                case 0b110:
                    return new Instruction("ld r8, Imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte registerCode = (byte)((opcode & 0b0011_1000) >> 3);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory[value] = imm8;

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                registries[registerCode]!.Value = imm8;
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[ld r8, Imm8] an error occurred");
                        }
                    });
                case 0b000:
                    return new Instruction("jr cond, imm8", 2, () =>
                    {
                        sbyte offest = (sbyte)Fetch();
                        byte conditionCode = (byte)((opcode & 0b0001_1000) >> 3);
                        if (getCc(conditionCode))
                        {
                            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

                            ushort newPcValue = (ushort)(pcValue + offest);

                            _PC[0] = (byte)(newPcValue & 0xFF);
                            _PC[1] = (byte)(newPcValue >> 8);

                            _mCycleCounter += 1;
                        }
                    });
                default:
                    break;
            }
            return null;
        }

        private Instruction? LookUpBlockOne(byte opcode)
        {
            Instruction halt = new Instruction("halt", 0, () =>
            {
                if (_interruptsManager.AreEnabled())
                {
                    // Enter low-power mode until an interrupt occurs
                    while (GameBoy.IsRunning)
                    {
                        Screen.ListenForEvents(ref GameBoy.IsRunning);

                        byte result = (byte)(_interruptsManager.IE & _interruptsManager.IF);
                        if (result != 0)
                        {
                            break;
                        }
                    }
                }
                else
                {
                    byte result = (byte)(_interruptsManager.IE & _interruptsManager.IF);
                    if (result == 0)
                    {
                        // No interrupt pending, wait for one to become pending
                        while (GameBoy.IsRunning)
                        {
                            Screen.ListenForEvents(ref GameBoy.IsRunning);

                            result = (byte)(_interruptsManager.IE & _interruptsManager.IF);
                            if (result != 0)
                            {
                                break;
                            }
                        }
                    }
                    else
                    {
                        // Interrupt pending, continue execution but read the next byte twice
                        ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
                        byte nextByte = _memory[pcValue];
                        _instructionRegister = nextByte;
                        DecodeAndExecute();
                    }
                }
            });

            switch (opcode)
            {
                case 0b0111_0110:
                    return halt;
                default:
                    return new Instruction("ld r8, r8", 1, () =>
                    {
                        byte registerCodeDestination = (byte)((opcode & 0b0011_1000) >> 3);
                        byte registerCodeSource = (byte)((opcode & 0b0000_0111));
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCodeDestination < registries.Count && registerCodeSource < registries.Count)
                        {
                            if (registerCodeDestination == 0b110 && registerCodeSource == 0b110)
                            {
                                halt.Execute();
                            }
                            else
                            {
                                if (registerCodeDestination == 0b110)
                                {
                                    ushort hlAddress = (ushort)((_HL[1] << 8) | _HL[0]);
                                    _memory[hlAddress] = registries[registerCodeSource]!.Value;

                                    _mCycleCounter += 1;
                                }
                                else if (registerCodeSource == 0b110)
                                {
                                    ushort hlAddress = (ushort)((_HL[1] << 8) | _HL[0]);
                                    registries[registerCodeDestination]!.Value = _memory[hlAddress];

                                    _mCycleCounter += 1;
                                }
                                else
                                {
                                    registries[registerCodeDestination]!.Value = registries[registerCodeSource]!.Value;
                                }
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[ld r8, r8] an error occurred");
                        }
                    });
            }
        }

        private Instruction? LookUpBlockTwo(byte opcode)
        {
            switch (opcode & 0b1111_1000)
            {
                case 0b1000_0000:
                    return new Instruction("add a, r8", 1, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0000_0111));
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = _memory[pointerValue];

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                valueToAdd = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] + valueToAdd);

                            _flags.SetZeroFlagZ(result);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(_AF[1], valueToAdd, true, false);
                            _flags.SetCarryFlagC(_AF[1] + valueToAdd, false, false);

                            _AF[1] = result;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[add a, r8] an error occurred");
                        }
                    });
                case 0b1000_1000:
                    return new Instruction("adc a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryIn = _flags.GetCarryFlagC();

                        if (registerCode < registries.Count)
                        {
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = (byte)(_memory[pointerValue]);

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                valueToAdd = (byte)(registries[registerCode]!.Value);
                            }

                            byte result = (byte)(_AF[1] + valueToAdd + carryIn);

                            _flags.SetZeroFlagZ(result);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(_AF[1], valueToAdd, carryIn, true);
                            _flags.SetCarryFlagC(_AF[1], valueToAdd, carryIn);

                            _AF[1] = result;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[adc a, r8] an error occurred");
                        }
                    });
                case 0b1001_0000:
                    return new Instruction("sub a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = _memory[pointerValue];

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] - operand);

                            _flags.SetZeroFlagZ(result);
                            _flags.SetSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                            _flags.SetCarryFlagC(operand > _AF[1] ? 1 : 0);

                            _AF[1] = result;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[sub a, r8] an error occurred");
                        }
                    });
                case 0b1001_1000:
                    return new Instruction("sbc a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryIn = _flags.GetCarryFlagC();
                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory[pointerValue]);

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            byte result = (byte)((_AF[1] - operand) - carryIn);

                            _flags.SetZeroFlagZ(result);
                            _flags.SetSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, carryIn, false);
                            _flags.SetCarryFlagC(operand + carryIn > _AF[1] ? 1 : 0);

                            _AF[1] = result;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[sbc a, r8] an error occurred");
                        }
                    });
                case 0b1010_0000:
                    return new Instruction("and a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory[pointerValue]);

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _AF[1] = (byte)(_AF[1] & operand);

                            _flags.SetZeroFlagZ(_AF[1]);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(1);
                            _flags.SetCarryFlagC(0);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[and a, r8] an error occurred");
                        }
                    });
                case 0b1010_1000:
                    return new Instruction("xor a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory[pointerValue]);

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _AF[1] = (byte)(_AF[1] ^ operand);

                            _flags.SetZeroFlagZ(_AF[1]);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(0);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[xor a, r8] an error occurred");
                        }
                    });
                case 0b1011_0000:
                    return new Instruction("or a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory[pointerValue]);

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _AF[1] = (byte)(_AF[1] | operand);

                            _flags.SetZeroFlagZ(_AF[1]);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(0);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[or a, r8] an error occurred");
                        }
                    });
                case 0b1011_1000:
                    return new Instruction("cp a, r8", 1, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = _memory[pointerValue];

                                _mCycleCounter += 1;
                            }
                            else
                            {
                                operand = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] - operand);

                            _flags.SetZeroFlagZ(result);
                            _flags.SetSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                            _flags.SetCarryFlagC(operand > _AF[1] ? 1 : 0);

                            // AF[1] MUST not be set
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[cp a, r8] an error occurred");
                        }
                    });
                default:
                    break;
            }
            return null;
        }

        private Instruction? LookUpBlockThree(byte opcode)
        {
            switch (opcode)
            {
                case 0b1100_0110:
                    return new Instruction("add a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte result = (byte)(_AF[1] + imm8);

                        _flags.SetZeroFlagZ(result);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, true, false);
                        _flags.SetCarryFlagC(_AF[1] + imm8, false, false);

                        _AF[1] = result;

                    });
                case 0b1100_1110:
                    return new Instruction("adc a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte carryIn = _flags.GetCarryFlagC();
                        byte operand = (byte)(imm8);
                        byte result = (byte)(_AF[1] + operand + carryIn);

                        _flags.SetZeroFlagZ(result);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(_AF[1], operand, carryIn, true);
                        _flags.SetCarryFlagC(_AF[1], operand, carryIn);

                        _AF[1] = result;
                    });
                case 0b1101_0110:
                    return new Instruction("sub a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte result = (byte)(_AF[1] - imm8);

                        _flags.SetZeroFlagZ(result);
                        _flags.SetSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, false, false);
                        _flags.SetCarryFlagC(imm8 > _AF[1] ? 1 : 0);

                        _AF[1] = result;
                    });
                case 0b1101_1110:
                    return new Instruction("sbc a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte carryIn = _flags.GetCarryFlagC();
                        byte result = (byte)((_AF[1] - imm8) - carryIn);

                        _flags.SetZeroFlagZ(result);
                        _flags.SetSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, carryIn, false);
                        _flags.SetCarryFlagC((imm8 + carryIn) > AF[1] ? 1 : 0);

                        _AF[1] = result;
                    });
                case 0b1110_0110:
                    return new Instruction("and a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] & imm8);

                        _flags.SetZeroFlagZ(_AF[1]);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(1);
                        _flags.SetCarryFlagC(0);
                    });
                case 0b1110_1110:
                    return new Instruction("xor a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] ^ imm8);

                        _flags.SetZeroFlagZ(_AF[1]);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(0);
                    });
                case 0b1111_0110:
                    return new Instruction("or a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] | imm8);

                        _flags.SetZeroFlagZ(_AF[1]);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.SetCarryFlagC(0);
                    });
                case 0b1111_1110:
                    return new Instruction("cp a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte result = (byte)(_AF[1] - imm8);

                        _flags.SetZeroFlagZ(result);
                        _flags.SetSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, false, false);
                        _flags.SetCarryFlagC(imm8 > _AF[1] ? 1 : 0);

                        // AF[1] MUST not be set
                    });
                case 0b1100_1001:
                    return new Instruction("ret", 4, () =>
                    {
                        Pop(out byte highByte, out byte lowByte);

                        _PC[0] = lowByte;
                        _PC[1] = highByte;
                    });
                case 0b1101_1001:
                    return new Instruction("reti", 4, () =>
                    {
                        Pop(out byte highByte, out byte lowByte);

                        _PC[0] = lowByte;
                        _PC[1] = highByte;

                        _interruptsManager.EnableInterrupts();
                    });
                case 0b1100_0011:
                    return new Instruction("jp n16", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        _PC[0] = lowByte;
                        _PC[1] = highByte;
                    });
                case 0b1110_1001:
                    return new Instruction("jp hl", 1, () =>
                    {
                        _PC[0] = _HL[0];
                        _PC[1] = _HL[1];
                    });
                case 0b1100_1101:
                    return new Instruction("call n16", 6, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        Push(highByte: _PC[1], lowByte: _PC[0]);

                        _PC[0] = lowByte;
                        _PC[1] = highByte;

                        _mCycleCounter += 3;
                    });
                case 0b1100_1011:
                    return LookUpBlockCB();
                case 0b1110_0010:
                    return new Instruction("ldh [c], a", 2, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + _BC[0]);

                        _memory[memoryPointer] = _AF[1];
                    });
                case 0b1110_0000:
                    return new Instruction("ldh [imm8], a", 3, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + Fetch());

                        _memory[memoryPointer] = _AF[1];
                    });
                case 0b1110_1010:
                    return new Instruction("ld [imm16], a", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);

                        _memory[memoryPointer] = _AF[1];
                    });
                case 0b1111_0010:
                    return new Instruction("ldh a, [c]", 2, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + _BC[0]);

                        _AF[1] = _memory[memoryPointer];
                    });
                case 0b1111_0000:
                    return new Instruction("ldh a, [imm8]", 3, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + Fetch());
                        _AF[1] = _memory[memoryPointer];
                    });
                case 0b1111_1010:
                    return new Instruction("ld a, [imm16]", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);
                        _AF[1] = _memory[memoryPointer];
                    });
                case 0b1110_1000:
                    return new Instruction("add sp, imm8", 4, () =>
                    {
                        sbyte imm8 = (sbyte)Fetch();
                        ushort stackPointer = (ushort)((_SP[1] << 8) | _SP[0]);
                        ushort result = (ushort)(stackPointer + imm8);

                        _SP[0] = (byte)(result & 0xFF);
                        _SP[1] = (byte)(result >> 8);

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH((ushort)(stackPointer & 0xF), (ushort)(imm8 & 0xF), true, false);
                        _flags.SetCarryFlagC((stackPointer & 0xFF) + (imm8 & 0xFF) > 0xFF ? 1 : 0);
                    });
                case 0b1111_1000:
                    return new Instruction("ld hl, sp + imm8", 3, () =>
                    {
                        sbyte imm8 = (sbyte)Fetch();
                        ushort stackPointer = (ushort)((_SP[1] << 8) | _SP[0]);
                        ushort result = (ushort)(stackPointer + imm8);

                        _HL[0] = (byte)(result & 0xFF);
                        _HL[1] = (byte)(result >> 8);

                        _flags.SetZeroFlagDirectlyZ(0);
                        _flags.SetSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH((ushort)(stackPointer & 0xF), (ushort)(imm8 & 0xF), true, false);
                        _flags.SetCarryFlagC((stackPointer & 0xFF) + (imm8 & 0xFF) > 0xFF ? 1 : 0);
                    });
                case 0b1111_1001:
                    return new Instruction("ld sp, hl", 2, () =>
                    {
                        _SP[0] = _HL[0];
                        _SP[1] = _HL[1];
                    });
                case 0b1111_0011:
                    return new Instruction("di", 1, () =>
                    {
                        _interruptsManager.DisableInterrupts();
                    });
                case 0b1111_1011:
                    return new Instruction("ei", 1, () =>
                    {
                        _interruptsManager.EI();
                    });
                default:
                    break;
            }

            switch (opcode & 0b1110_0111)
            {
                case 0b1100_0000:
                    return new Instruction("ret cond", 2, () =>
                    {
                        byte conditionCode = (byte)((opcode & 0b0001_1000) >> 3);
                        if (getCc(conditionCode))
                        {
                            Pop(out byte highByte, out byte lowByte);

                            _PC[0] = lowByte;
                            _PC[1] = highByte;

                            _mCycleCounter += 3;
                        }
                    });
                case 0b1100_0010:
                    return new Instruction("jp cond, n16", 3, () =>
                    {
                        byte conditionCode = (byte)((opcode & 0b0001_1000) >> 3);

                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        if (getCc(conditionCode))
                        {
                            _PC[0] = lowByte;
                            _PC[1] = highByte;

                            _mCycleCounter += 1;
                        }
                    });
                case 0b1100_0100:
                    return new Instruction("call cc, n16", 3, () =>
                    {
                        byte conditionCode = (byte)((opcode & 0b0001_1000) >> 3);

                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        if (getCc(conditionCode))
                        {
                            Push(highByte: _PC[1], lowByte: _PC[0]);

                            _PC[0] = lowByte;
                            _PC[1] = highByte;

                            _mCycleCounter += 3;
                        }
                    });
                default:
                    break;
            }

            switch (opcode & 0b1100_0111)
            {
                case 0b1100_0111:
                    return new Instruction("rst tgt3", 4, () =>
                    {
                        byte vec = (byte)((opcode & 0b0011_1000) >> 3);

                        ushort vec3 = (ushort)(vec * 8);

                        Push(highByte: _PC[1], lowByte: _PC[0]);

                        _PC[0] = (byte)(vec3 & 0xFF);
                        _PC[1] = (byte)(vec3 >> 8);

                    });
                case 0b1100_0001:
                    return new Instruction("pop r16stk", 3, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16stk];
                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];

                            Pop(out byte highByte, out byte lowByte);

                            if (registerCode == 0b11)
                            {
                                lowByte &= 0b1111_0000;
                            }
                            register[0] = lowByte;
                            register[1] = highByte;
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[pop r16stk] an error occurred");
                        }
                    });
                case 0b1100_0101:
                    return new Instruction("push r16stk", 4, () =>
                    {
                        byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                        List<Byte[]> registries = _16bitsRegistries[paramsType.r16stk];
                        if (registerCode < registries.Count)
                        {
                            byte[] register = registries[registerCode];
                            Push(highByte: register[1], lowByte: register[0]);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[pop r16stk] an error occurred");
                        }
                    });
                default:
                    break;
            }
            return null;
        }

        private Instruction? LookUpBlockCB()
        {
            byte opcode = Fetch();
            switch (opcode & 0b1111_1000)
            {
                case 0b0000_0000:
                    return new Instruction("rlc r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory[memoryPointer] & 0b1000_0000) >> 7);
                                _memory[memoryPointer] = (byte)((_memory[memoryPointer] << 1) | carryOut);

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)((register.Value << 1) | carryOut);

                                _flags.SetZeroFlagZ(register.Value);
                            }


                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[rlc r8] an error occurred");
                        }
                    });
                case 0b0000_1000:
                    return new Instruction("rrc r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory[memoryPointer] & 0b0000_0001));
                                _memory[memoryPointer] = (byte)((_memory[memoryPointer] >> 1) | carryOut << 7);

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b0000_0001));
                                register.Value = (byte)((register.Value >> 1) | carryOut << 7);

                                _flags.SetZeroFlagZ(register.Value);
                            }


                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[rrc r8] an error occurred");
                        }
                    });
                case 0b0001_0000:
                    return new Instruction("rl r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory[memoryPointer] & 0b1000_0000) >> 7);

                                _memory[memoryPointer] = (byte)((_memory[memoryPointer] << 1) | _flags.GetCarryFlagC());

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)((register.Value << 1) | _flags.GetCarryFlagC());

                                _flags.SetZeroFlagZ(register.Value);
                            }


                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[rl r8] an error occurred");
                        }
                    });
                case 0b0001_1000:
                    return new Instruction("rr r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)(_memory[memoryPointer] & 0b0000_0001);
                                _memory[memoryPointer] = (byte)((_memory[memoryPointer] >> 1) | (_flags.GetCarryFlagC() << 7));

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)(register.Value & 0b0000_0001);
                                register.Value = (byte)((register.Value >> 1) | (_flags.GetCarryFlagC() << 7));

                                _flags.SetZeroFlagZ(register.Value);
                            }

                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[rr r8] an error occurred");
                        }
                    });
                case 0b0010_0000:
                    return new Instruction("sla r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory[memoryPointer] & 0b1000_0000) >> 7);
                                _memory[memoryPointer] = (byte)(_memory[memoryPointer] << 1);

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)(register.Value << 1);

                                _flags.SetZeroFlagZ(register.Value);
                            }

                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[sla r8] an error occurred");
                        }
                    });
                case 0b0010_1000:
                    return new Instruction("sra r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                byte originalValue = _memory[memoryPointer];
                                carryOut = (byte)(originalValue & 0b0000_0001);
                                _memory[memoryPointer] = (byte)((originalValue >> 1) | (originalValue & 0b1000_0000));

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                byte originalValue = register.Value;
                                carryOut = (byte)(originalValue & 0b0000_0001);
                                register.Value = (byte)((originalValue >> 1) | (originalValue & 0b1000_0000));

                                _flags.SetZeroFlagZ(register.Value);
                            }

                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[sra r8] an error occurred");
                        }
                    });
                case 0b0011_0000:
                    return new Instruction("swap r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte newValue;
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                byte firstFour = (byte)((_memory[memoryPointer] & 0b1111_0000) >> 4);
                                byte lastFour = (byte)((_memory[memoryPointer] & 0b0000_1111) << 4);


                                newValue = (byte)(firstFour | lastFour);
                                _memory[memoryPointer] = newValue;

                                _mCycleCounter += 2;
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                byte firstFour = (byte)((register.Value & 0b1111_0000) >> 4);
                                byte lastFour = (byte)((register.Value & 0b0000_1111) << 4);

                                newValue = (byte)(firstFour | lastFour);
                                register.Value = newValue;
                            }

                            _flags.SetZeroFlagZ(newValue);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(0);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[swap r8] an error occurred");
                        }
                    });
                case 0b0011_1000:
                    return new Instruction("srl r8", 2, () =>
                    {
                        byte registerCode = (byte)(opcode & 0b0000_0111);
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)(_memory[memoryPointer] & 0b0000_0001);
                                _memory[memoryPointer] = (byte)(_memory[memoryPointer] >> 1);

                                _mCycleCounter += 2;
                                _flags.SetZeroFlagZ(_memory[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)(register.Value & 0b0000_0001);
                                register.Value = (byte)(register.Value >> 1);

                                _flags.SetZeroFlagZ(register.Value);
                            }

                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.SetCarryFlagC(carryOut);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[srl r8] an error occurred");
                        }
                    });
                default:
                    break;
            }
            switch (opcode & 0b1100_0000)
            {
                case 0b0100_0000:
                    return new Instruction("bit b3, r8", 2, () =>
                    {
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte bitIndex = (byte)((opcode & 0b0011_1000) >> 3);
                        byte registerCode = (byte)(opcode & 0b0000_0111);

                        if (registerCode < registries.Count)
                        {
                            byte bit;
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                bit = (byte)((_memory[memoryPointer] >> bitIndex) & 0b0000_0001);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                bit = (byte)((register.Value >> bitIndex) & 0b0000_0001);
                            }


                            _flags.SetZeroFlagZ(bit);
                            _flags.SetSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(1);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[bit b3, r8] an error occurred");
                        }
                    });
                case 0b1000_0000:
                    return new Instruction("res b3, r8", 2, () =>
                    {
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte bitIndex = (byte)((opcode & 0b0011_1000) >> 3);
                        byte registerCode = (byte)(opcode & 0b0000_0111);

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory[memoryPointer] &= (byte)~(1 << bitIndex);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                register.Value &= (byte)~(1 << bitIndex);

                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[res b3, r8] an error occurred");
                        }
                    });
                case 0b1100_0000:
                    return new Instruction("set b3, r8", 2, () =>
                    {
                        List<ByteRegister?> registries = _8bitsRegistries[paramsType.r8];

                        byte bitIndex = (byte)((opcode & 0b0011_1000) >> 3);
                        byte registerCode = (byte)(opcode & 0b0000_0111);

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory[memoryPointer] |= (byte)(1 << bitIndex);

                                _mCycleCounter += 2;
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                register.Value |= (byte)(1 << bitIndex);
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[set b3, r8] si è verificato un errore");
                        }
                    });
                default:
                    break;
            }
            return null;
        }

        internal struct Instruction
        {
            public readonly string Name;
            public readonly ushort Cycles;
            public readonly Action Execute;
            public Instruction(string name, ushort cycles, Action execute)
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
            cc,
        }

        byte Fetch()
        {
            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

            if (pcValue > Memory.MEM_MAX_ADDRESS)
            {
                _logger.Fatal("Attempted to fetch beyond ROM boundaries.");
                throw new IndexOutOfRangeException("Program Counter exceeded ROM boundaries.");
            }

            byte nextByte = _memory[pcValue];

            pcValue++;

            _PC[0] = (byte)(pcValue & 0xFF);
            _PC[1] = (byte)(pcValue >> 8);

            return nextByte;
        }

        public void DecodeAndExecute()
        {
            Instruction? instruction;
            try
            {
                //here it will bind the opcode with the parameter fetching it and executing the code
                byte block = (byte)((_instructionRegister & 0b1100_0000) >> 6); // read the instruction block

                switch (block)
                {
                    case 0b00:
                        instruction = LookUpBlockZero(_instructionRegister);
                        break;
                    case 0b01:
                        instruction = LookUpBlockOne(_instructionRegister);
                        break;
                    case 0b10:
                        instruction = LookUpBlockTwo(_instructionRegister);
                        break;
                    case 0b11:
                        instruction = LookUpBlockThree(_instructionRegister);
                        break;
                    default:
                        instruction = null;
                        break;
                }
            }
            catch (Exception e)
            {
                _logger.Fatal($"Error while decoding the instruction {e.Message}");
                return;
            }

            //Execute the intruction
            try
            {
                if (instruction != null)
                {

                    instruction?.Execute();

                    _logger.Debug($"Opcode: {_instructionRegister} ({instruction?.Name})"); // commented to reduce overhead
                    _logger.Debug($"AF: {BitConverter.ToString(_AF.ToArray())}, BC: {BitConverter.ToString(_BC.ToArray())}, DE: {BitConverter.ToString(_DE.ToArray())}, HL: {BitConverter.ToString(_HL.ToArray())}, SP: {BitConverter.ToString(_SP.ToArray())}, PC: {BitConverter.ToString(_PC.ToArray())}");

                    _mCycleCounter += instruction?.Cycles ?? 0;
                    _machineCycles.Tick(_mCycleCounter);
                    _mCycleCounter = 0;
                }
                else
                {
                    throw new InstructionExcecutionException($"Instruction [{_instructionRegister}] not found");
                }
            }
            catch (InstructionExcecutionException IntrExEx)
            {
                _logger.Fatal(IntrExEx.Message);
                throw new InstructionExcecutionException(IntrExEx.Message);
            }
        }

        public void Run()
        {
            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
            _interruptsManager.HandleEiIfNeeded();

            if (_interruptsManager.AreEnabled())
            {
                byte result = (byte)(_interruptsManager.IE & _interruptsManager.IF);

                byte vBlank = (byte)(result & 0b0000_0001);
                byte lcd = (byte)((result & 0b0000_0010) >> 1);
                byte timer = (byte)((result & 0b0000_0100) >> 2);
                byte serial = (byte)((result & 0b0000_1000) >> 3);
                byte joypad = (byte)((result & 0b0001_0000) >> 4);

                if (result != 0)
                {
                    if (vBlank == 1)
                    {
                        Push(highByte: _PC[1], lowByte: _PC[0]);
                        _PC[0] = 0x40;
                        _PC[1] = 0x00;
                        _interruptsManager.DisableInterrupts();
                        _interruptsManager.DisableVblankInterrupt();
                        _mCycleCounter += 5;

                    }
                    else if (lcd == 1)
                    {
                        Push(highByte: _PC[1], lowByte: _PC[0]);
                        _PC[0] = 0x48;
                        _PC[1] = 0x00;
                        _interruptsManager.DisableInterrupts();
                        _interruptsManager.DisableStatInterrupt();
                        _mCycleCounter += 5;

                    }
                    else if (timer == 1)
                    {
                        Push(highByte: _PC[1], lowByte: _PC[0]);
                        _PC[0] = 0x50;
                        _PC[1] = 0x00;
                        _interruptsManager.DisableInterrupts();
                        _interruptsManager.DisableTimerInterrupt();
                        _mCycleCounter += 5;

                    }
                    else if (serial == 1)
                    {
                        Push(highByte: _PC[1], lowByte: _PC[0]);
                        _PC[0] = 0x58;
                        _PC[1] = 0x00;
                        _interruptsManager.DisableInterrupts();
                        _interruptsManager.DisableSerialInterrupt();
                        _mCycleCounter += 5;

                    }
                    else if (joypad == 1)
                    {
                        Push(highByte: _PC[1], lowByte: _PC[0]);
                        _PC[0] = 0x60;
                        _PC[1] = 0x00;
                        _interruptsManager.DisableInterrupts();
                        _interruptsManager.DisableJoypadInterrupt();
                        _mCycleCounter += 5;
                    }
                }
            }

            byte data = Fetch();
            _instructionRegister = data;
            try
            {
                DecodeAndExecute();
            }
            catch (InstructionExcecutionException)
            {
                return;
            }

            pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
        }
    }
}

