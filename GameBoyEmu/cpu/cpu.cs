﻿using GameBoyEmu.Exceptions;
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
    class Cpu
    {
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Memory _memory;
        private FlagsHelper _flags;

        //16 bits
        private byte[] _AF = new byte[2] { 0x80, 0x01 };
        private byte[] _BC = new byte[2] { 0x13, 0x00 };
        private byte[] _DE = new byte[2] { 0xD8, 0x00 };
        private byte[] _HL = new byte[2] { 0x4D, 0x01 };
        private byte[] _SP = new byte[2] { 0xFE, 0xFF };
        private byte[] _PC = new byte[2] { 0x00, 0x01 };

        private byte _instructionRegister = 0x00;
        private bool imeFlag = false;

        private int _cycles = 0;

        private readonly Dictionary<paramsType, List<byte[]>> _16bitsRegistries;
        private readonly Dictionary<paramsType, List<ByteRegister?>> _8bitsRegistries;

        public Cpu(ref Memory _mem)
        {
            _flags = new FlagsHelper(ref _AF);
            this._memory = _mem;

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
                        new ByteRegister(_BC, 0),
                        new ByteRegister(_BC, 1),
                        new ByteRegister(_DE, 0),
                        new ByteRegister(_DE, 1),
                        new ByteRegister(_HL, 0),
                        new ByteRegister(_HL, 1),
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
            _memory.memoryMap[sp] = lowByte;

            sp--;
            _memory.memoryMap[sp] = highByte;

            _SP[0] = (byte)(sp & 0xFF);
            _SP[1] = (byte)(sp >> 8);
        }


        private void Pop(out byte highByte, out byte lowByte)
        {
            ushort sp = (ushort)((_SP[1] << 8) | _SP[0]);

            highByte = _memory.memoryMap[sp];
            sp++;

            lowByte = _memory.memoryMap[sp];
            sp++;

            _SP[0] = (byte)(sp & 0xFF);
            _SP[1] = (byte)(sp >> 8);
        }

        private bool getCc(byte cond)
        {
            switch (cond)
            {
                case 0b00: // NZ
                    return _flags.getZeroFlagZ() == 0;
                case 0b01: // Z
                    return _flags.getZeroFlagZ() == 1;
                case 0b10: // NC
                    return _flags.getCarryFlagC() == 0;
                case 0b11: // C
                    return _flags.getCarryFlagC() == 1;
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
                        _logger.Debug($"Instruction Fetched: {"NOP"}");
                        return;
                    });
                case 0b0000_0111:
                    return new Instruction("rlca", 1, () =>
                    {
                        byte carryOut = (byte)((_AF[1] & 0b1000_0000) >> 7);
                        _AF[1] = (byte)((_AF[1] << 1) | carryOut);

                        _logger.Debug($"Instruction Fetched: {"rlca"}");

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(carryOut);
                    });
                case 0b0000_1111:
                    return new Instruction("rrca", 1, () =>
                    {
                        byte carryOut = (byte)((_AF[1] & 0b0000_0001));
                        _AF[1] = (byte)((_AF[1] >> 1) | (carryOut << 7));

                        _logger.Debug($"Instruction Fetched: {"rrca"}");

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(carryOut);
                    });
                case 0b0001_0111:
                    return new Instruction("rla", 1, () =>
                    {
                        byte carryFlagValue = _flags.getCarryFlagC();
                        byte carryOut = (byte)((_AF[1] & 0b1000_0000) >> 7);

                        _AF[1] = _AF[1] = (byte)((_AF[1] << 1) | carryFlagValue);

                        _logger.Debug($"Instruction Fetched: {"rla"}");

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(carryOut);
                    });
                case 0b0001_1111:
                    return new Instruction("rra", 1, () =>
                    {
                        byte carryFlagValue = _flags.getCarryFlagC();
                        byte carryOut = (byte)((_AF[1] & 0b0000_0001));

                        _AF[1] = (byte)((_AF[1] >> 1) | (carryFlagValue << 7));

                        _logger.Debug($"Instruction Fetched: {"rra"}");

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(carryOut);
                    });
                case 0b0010_0111:
                    return new Instruction("daa", 1, () =>
                    {
                        byte A = _AF[1];
                        bool subtraction = _flags.getSubtractionFlagN() == 0 ? true : false;

                        if (!subtraction)
                        {
                            if ((A & 0x0F) > 9 || _flags.getHalfCarryFlagH() == 1)
                            {
                                A += 0x06;
                            }

                            if (_flags.getCarryFlagC() == 1 || A > 0x99)
                            {
                                A += 0x60;
                            }
                        }
                        else
                        {
                            if ((A & 0x0F) > 9 || _flags.getHalfCarryFlagH() == 1)
                            {
                                A -= 0x06;
                            }

                            if (_flags.getCarryFlagC() == 1)
                            {
                                A -= 0x60;
                            }
                        }

                        _AF[1] = A;

                        _logger.Debug($"Instruction Fetched: {"daa"}");

                        _flags.setZeroFlagZ(_AF[1]);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(A > 0x99 ? 1 : 0);

                    });
                case 0b0010_1111:
                    return new Instruction("cpl", 1, () =>
                    {
                        _AF[1] = (byte)~_AF[1];
                        _logger.Debug($"Instruction Fetched: {"cpl"}");
                        _flags.setSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(1);
                    });
                case 0b0011_0111:
                    return new Instruction("scf", 1, () =>
                    {
                        _flags.setCarryFlagC(1);

                        _logger.Debug($"Instruction Fetched: {"scf"}");

                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                    });
                case 0b0011_1111:
                    return new Instruction("ccf", 1, () =>
                    {
                        _flags.setCarryFlagC((byte)~_flags.getCarryFlagC());

                        _logger.Debug($"Instruction Fetched: {"ccf"}");
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                    });
                case 0b0001_1000:
                    return new Instruction("jr imm8", 3, () =>
                    {
                        sbyte offest = (sbyte)Fetch();
                        ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

                        ushort newPcValue = (ushort)(pcValue + offest);
                        _logger.Debug($"Instruction Fetched: {"jr imm8"}, with pcValue: {pcValue}, offset: {offest} and new pcValue: {newPcValue}");

                        _PC[0] = (byte)(newPcValue & 0xFF);
                        _PC[1] = (byte)(newPcValue >> 8);
                    });
                case 0b0001_0000:
                    return new Instruction("stop", 0, () =>
                    {
                        //TODO: Handle Interrupts
                        byte nextByte = Fetch();
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

                        _logger.Debug($"Instruction Fetched: {"LD r16, imm16"} with params: {lowByte}, {highByte} and register {registerCode}");

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

                            _logger.Debug($"Instruction Fetched: {"LD [r16mem], A"}, register {registerCode}");

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS - 1)
                            {
                                _memory.memoryMap[memoryPointer] = _AF[1];
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

                            _logger.Debug($"Instruction Fetched: {"LD A, [r16mem]"}, register {registerCode}");

                            if (memoryPointer <= Memory.MEM_MAX_ADDRESS)
                            {
                                _AF[1] = _memory.memoryMap[memoryPointer];
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
                case 0b1000:
                    return new Instruction("LD [imm16], SP", 5, () =>
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
                    });
                case 0b0011:
                    return new Instruction("INC r16", 2, () =>
                        {
                            byte registerCode = (byte)((opcode & 0b0011_0000) >> 4);
                            List<Byte[]> registries = _16bitsRegistries[paramsType.r16];

                            _logger.Debug($"Instruction Fetched: {"INC r16"}, register {registerCode}");

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

                        _logger.Debug($"Instruction Fetched: {"DEC r16"}, register {registerCode}");

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

                        _logger.Debug($"Instruction Fetched: {"add HL, r16"}, register {registerCode}");

                        if (registerCode < registries.Count)
                        {

                            byte[] register = registries[registerCode];
                            ushort registerValue = (ushort)((register[1] << 8) | register[0]);

                            ushort HLValue = (ushort)((_HL[1] << 8) | _HL[0]);

                            HLValue += registerValue;

                            _HL[0] = (byte)(HLValue & 0xFF);
                            _HL[1] = (byte)(HLValue >> 8);

                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(HLValue, registerValue, true, true);
                            _flags.setCarryFlagC(registerValue + HLValue, true, false);
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

                            _logger.Debug($"Instruction Fetched: {"inc r8"}, register {registerCode}");
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory.memoryMap[value];
                                newValue++;
                                _memory.memoryMap[value] = newValue;

                                _cycles += 2;
                            }
                            else
                            {
                                registries[registerCode]!.Value = (byte)(registries[registerCode]!.Value + 1);
                                newValue = registries[registerCode]!.Value;
                            }

                            _flags.SetHalfCarryFlagH((ushort)(newValue - 1), 1, true, false);
                            _flags.setSubtractionFlagN(0);
                            _flags.setZeroFlagZ(newValue);
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
                            _logger.Debug($"Instruction Fetched: {"dec r8"}, register {registerCode}");
                            byte newValue;
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory.memoryMap[value];
                                newValue--;
                                _memory.memoryMap[value] = newValue;

                                _cycles += 2;
                            }
                            else
                            {
                                registries[registerCode]!.Value = (byte)(registries[registerCode]!.Value - 1);
                                newValue = registries[registerCode]!.Value;
                            }
                            _flags.SetHalfCarryFlagH((ushort)(newValue + 1), 1, false, false);
                            _flags.setSubtractionFlagN(1);
                            _flags.setZeroFlagZ(newValue);
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
                            _logger.Debug($"Instruction Fetched: {"ld r8, Imm8"}, value: {imm8} with register {registerCode}");
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory.memoryMap[value] = imm8;

                                _cycles += 1;
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

                            _logger.Debug($"Instruction Fetched: {"jr cond, imm8"}, with cc: {conditionCode}, pcValue: {pcValue}, offset: {offest} and new pcValue: {newPcValue}");

                            _PC[0] = (byte)(newPcValue & 0xFF);
                            _PC[1] = (byte)(newPcValue >> 8);

                            _cycles += 1;
                        }
                        else
                        {
                            _logger.Debug($"Instruction Fetched: {"jr cond, imm8"}, with cc: {conditionCode}");
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
                //TODO: handle interrupts
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

                        _logger.Debug($"Instruction Fetched: {"ld r8, r8"}, destination: {registerCodeDestination}, source: {registerCodeSource}");

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
                                    _memory.memoryMap[hlAddress] = registries[registerCodeSource]!.Value;

                                    _cycles += 1;
                                }
                                else if (registerCodeSource == 0b110)
                                {
                                    ushort hlAddress = (ushort)((_HL[1] << 8) | _HL[0]);
                                    registries[registerCodeDestination]!.Value = _memory.memoryMap[hlAddress];

                                    _cycles += 1;
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
                            _logger.Debug($"Instruction Fetched: {"add a, r8"}, register {registerCode}");
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = _memory.memoryMap[pointerValue];

                                _cycles += 1;
                            }
                            else
                            {
                                valueToAdd = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] + valueToAdd);

                            _flags.setZeroFlagZ(result);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(_AF[1], valueToAdd, true, false);
                            _flags.setCarryFlagC(_AF[1] + valueToAdd, false, false);

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

                        if (registerCode < registries.Count)
                        {
                            _logger.Debug($"Instruction Fetched: {"adc a, r8"}, register {registerCode}");
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = (byte)(_memory.memoryMap[pointerValue] + _flags.getCarryFlagC());

                                _cycles += 1;
                            }
                            else
                            {
                                valueToAdd = (byte)(registries[registerCode]!.Value + _flags.getCarryFlagC());
                            }

                            byte result = (byte)(_AF[1] + valueToAdd);

                            _flags.setZeroFlagZ(result);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(_AF[1], valueToAdd, true, false);
                            _flags.setCarryFlagC(_AF[1] + valueToAdd, false, false);

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
                                operand = _memory.memoryMap[pointerValue];

                                _cycles += 1;
                            }
                            else
                            {
                                operand = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] - operand);

                            _logger.Debug($"Instruction Fetched: {"sub a, r8"}, register {registerCode}, operand {operand} result {result}");

                            _flags.setZeroFlagZ(result);
                            _flags.setSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                            _flags.setCarryFlagC(operand > _AF[1] ? 1 : 0);

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

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory.memoryMap[pointerValue] + _flags.getCarryFlagC());

                                _cycles += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value + _flags.getCarryFlagC());
                            }

                            byte result = (byte)(_AF[1] - operand);

                            _logger.Debug($"Instruction Fetched: {"sbc a, r8"}, register {registerCode}, operand {operand} result {result}");

                            _flags.setZeroFlagZ(result);
                            _flags.setSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                            _flags.setCarryFlagC(operand > _AF[1] ? 1 : 0);

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
                                operand = (byte)(_memory.memoryMap[pointerValue]);

                                _cycles += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _logger.Debug($"Instruction Fetched: {"and a, r8"}, register {registerCode}");

                            _AF[1] = (byte)(_AF[1] & operand);

                            _flags.setZeroFlagZ(_AF[1]);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(1);
                            _flags.setCarryFlagC(0);
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
                                operand = (byte)(_memory.memoryMap[pointerValue]);

                                _cycles += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _logger.Debug($"Instruction Fetched: {"xor a, r8"}, register {registerCode}");

                            _AF[1] = (byte)(_AF[1] ^ operand);

                            _flags.setZeroFlagZ(_AF[1]);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(0);
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
                                operand = (byte)(_memory.memoryMap[pointerValue]);

                                _cycles += 1;
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]!.Value);
                            }

                            _logger.Debug($"Instruction Fetched: {"or a, r8"}, register {registerCode}");

                            _AF[1] = (byte)(_AF[1] | operand);

                            _flags.setZeroFlagZ(_AF[1]);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(0);
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
                                operand = _memory.memoryMap[pointerValue];

                                _cycles += 1;
                            }
                            else
                            {
                                operand = registries[registerCode]!.Value;
                            }

                            byte result = (byte)(_AF[1] - operand);

                            _logger.Debug($"Instruction Fetched: {"cp a, r8"}, register {registerCode}, operand {operand} result {result}");

                            _flags.setZeroFlagZ(result);
                            _flags.setSubtractionFlagN(1);
                            _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                            _flags.setCarryFlagC(operand > _AF[1] ? 1 : 0);

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

                        _logger.Debug($"Instruction Fetched: {"add a, imm8"} operand {imm8} result {result}");

                        _flags.setZeroFlagZ(result);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, true, false);
                        _flags.setCarryFlagC(_AF[1] + imm8, false, false);

                        _AF[1] = result;

                    });
                case 0b1100_1110:
                    return new Instruction("adc a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte operand = (byte)(imm8 + _flags.getCarryFlagC());
                        byte result = (byte)(_AF[1] + operand);

                        _logger.Debug($"Instruction Fetched: {"adc a, imm8"} operand {imm8} result {result}");

                        _flags.setZeroFlagZ(result);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(_AF[1], operand, true, false);
                        _flags.setCarryFlagC(_AF[1] + operand, false, false);

                        _AF[1] = result;
                    });
                case 0b1101_0110:
                    return new Instruction("sub a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte result = (byte)(_AF[1] - imm8);

                        _logger.Debug($"Instruction Fetched: {"sub a, imm8"} operand {imm8} result {result}");

                        _flags.setZeroFlagZ(result);
                        _flags.setSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, false, false);
                        _flags.setCarryFlagC(imm8 > _AF[1] ? 1 : 0);

                        _AF[1] = result;
                    });
                case 0b1101_1110:
                    return new Instruction("sbc a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte operand = (byte)(imm8 + _flags.getCarryFlagC());
                        byte result = (byte)(_AF[1] - operand);

                        _logger.Debug($"Instruction Fetched: {"sbc a, imm8"} operand {imm8} result {result}");

                        _flags.setZeroFlagZ(result);
                        _flags.setSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], operand, false, false);
                        _flags.setCarryFlagC(operand > _AF[1] ? 1 : 0);

                        _AF[1] = result;
                    });
                case 0b1110_0110:
                    return new Instruction("and a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] & imm8);

                        _logger.Debug($"Instruction Fetched: {"and a, imm8"} operand {imm8}");

                        _flags.setZeroFlagZ(_AF[1]);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(1);
                        _flags.setCarryFlagC(0);
                    });
                case 0b1110_1110:
                    return new Instruction("xor a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] ^ imm8);

                        _logger.Debug($"Instruction Fetched: {"xor a, imm8"} operand {imm8}");

                        _flags.setZeroFlagZ(_AF[1]);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(0);
                    });
                case 0b1111_0110:
                    return new Instruction("or a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        _AF[1] = (byte)(_AF[1] | imm8);

                        _logger.Debug($"Instruction Fetched: {"or a, imm8"} operand {imm8}");

                        _flags.setZeroFlagZ(_AF[1]);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH(0);
                        _flags.setCarryFlagC(0);
                    });
                case 0b1111_1110:
                    return new Instruction("cp a, imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte result = (byte)(_AF[1] - imm8);

                        _logger.Debug($"Instruction Fetched: {"cp a, imm8"}, operand {imm8} result {result}");

                        _flags.setZeroFlagZ(result);
                        _flags.setSubtractionFlagN(1);
                        _flags.SetHalfCarryFlagH(_AF[1], imm8, false, false);
                        _flags.setCarryFlagC(imm8 > _AF[1] ? 1 : 0);

                        // AF[1] MUST not be set
                    });
                case 0b1100_1001:
                    return new Instruction("ret", 4, () =>
                    {
                        Pop(out byte highByte, out byte lowByte);

                        _logger.Debug($"Instruction Fetched: {"ret"}");

                        _PC[0] = lowByte;
                        _PC[1] = highByte;
                    });
                case 0b1101_1001:
                    return new Instruction("reti", 4, () =>
                    {
                        Pop(out byte highByte, out byte lowByte);

                        _logger.Debug($"Instruction Fetched: {"reti"}");

                        _PC[0] = lowByte;
                        _PC[1] = highByte;

                        imeFlag = true;
                    });
                case 0b1100_0011:
                    return new Instruction("jp n16", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        _PC[0] = lowByte;
                        _PC[1] = highByte;

                        _logger.Debug($"Instruction Fetched: {"jp n16"} with params: {lowByte}, {highByte}");
                    });
                case 0b1110_1001:
                    return new Instruction("jp hl", 1, () =>
                    {
                        _PC[0] = _HL[0];
                        _PC[1] = _HL[1];

                        _logger.Debug($"Instruction Fetched: {"jp hl"}");
                    });
                case 0b1100_1101:
                    return new Instruction("call n16", 6, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        Push(highByte: _PC[1], lowByte: _PC[0]);

                        _PC[0] = lowByte;
                        _PC[1] = highByte;

                        _cycles += 3;
                        _logger.Debug($"Instruction Fetched: {"call n16"} with params: {lowByte}, {highByte}");
                    });
                case 0b1100_1011:
                    return LookUpBlockCB();
                case 0b1110_0010:
                    return new Instruction("ldh [c], a", 2, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + _BC[0]);

                        _logger.Debug($"Instruction Fetched: {"ldh [c], a"} with memory pointer: {memoryPointer}");

                        _memory.memoryMap[memoryPointer] = _AF[1];
                    });
                case 0b1110_0000:
                    return new Instruction("ldh [imm8], a", 3, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + Fetch());

                        _logger.Debug($"Instruction Fetched: {"ldh [imm8], a"} with memory pointer: {memoryPointer}");

                        _memory.memoryMap[memoryPointer] = _AF[1];
                    });
                case 0b1110_1010:
                    return new Instruction("ld [imm16], a", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);

                        _logger.Debug($"Instruction Fetched: {"ld [imm16], a"} with memory pointer: {memoryPointer}");

                        _memory.memoryMap[memoryPointer] = _AF[1];
                    });
                case 0b1111_0010:
                    return new Instruction("ldh a, [c]", 2, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + _BC[0]);

                        _logger.Debug($"Instruction Fetched: {"ldh a, [c]"} with memory pointer: {memoryPointer}");

                        _AF[1] = _memory.memoryMap[memoryPointer];
                    });
                case 0b1111_0000:
                    return new Instruction("ldh a, [imm8]", 3, () =>
                    {
                        ushort memoryPointer = (ushort)(0xFF00 + Fetch());

                        _logger.Debug($"Instruction Fetched: {"ldh a, [imm8]"} with memory pointer: {memoryPointer}");

                        _AF[1] = _memory.memoryMap[memoryPointer];
                    });
                case 0b1111_1010:
                    return new Instruction("ld a, [imm16]", 4, () =>
                    {
                        byte lowByte = Fetch();
                        byte highByte = Fetch();

                        ushort memoryPointer = (ushort)((highByte << 8) | lowByte);

                        _logger.Debug($"Instruction Fetched: {"ld a, [imm16]"} with memory pointer: {memoryPointer}");

                        _AF[1] = _memory.memoryMap[memoryPointer];
                    });
                case 0b1110_1000:
                    return new Instruction("add sp, imm8", 4, () =>
                    {
                        sbyte imm8 = (sbyte)Fetch();
                        ushort stackPointer = (ushort)((_SP[1] << 8) | _SP[0]);
                        ushort result = (ushort)(stackPointer + imm8);

                        _logger.Debug($"Instruction Fetched: {"add sp, imm8"} with param: {imm8}");

                        _SP[0] = (byte)(result & 0xFF);
                        _SP[1] = (byte)(result >> 8);

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH((ushort)(stackPointer & 0xF), (ushort)(imm8 & 0xF), true, false);
                        _flags.setCarryFlagC((stackPointer & 0xFF) + (imm8 & 0xFF) > 0xFF ? 1 : 0);
                    });
                case 0b1111_1000:
                    return new Instruction("ld hl, sp + imm8", 3, () =>
                    {
                        sbyte imm8 = (sbyte)Fetch();
                        ushort stackPointer = (ushort)((_SP[1] << 8) | _SP[0]);
                        ushort result = (ushort)(stackPointer + imm8);

                        _logger.Debug($"Instruction Fetched: {"ld hl, sp + imm8"} with param: {imm8}");

                        _HL[0] = (byte)(result & 0xFF);
                        _HL[1] = (byte)(result >> 8);

                        _flags.setZeroFlagZ(0);
                        _flags.setSubtractionFlagN(0);
                        _flags.SetHalfCarryFlagH((ushort)(stackPointer & 0xF), (ushort)(imm8 & 0xF), true, false);
                        _flags.setCarryFlagC((stackPointer & 0xFF) + (imm8 & 0xFF) > 0xFF ? 1 : 0);
                    });
                case 0b1111_1001:
                    return new Instruction("ld sp, hl", 2, () =>
                    {
                        _SP[0] = _HL[0];
                        _SP[1] = _HL[1];

                        _logger.Debug($"Instruction Fetched: {"ld sp, hl"}");
                    });
                case 0b1111_0011:
                    return new Instruction("di", 1, () =>
                    {
                        imeFlag = false;

                        _logger.Debug($"Instruction Fetched: {"di"}");
                    });
                case 0b1111_1011:
                    return new Instruction("ei", 1, () =>
                    {
                        imeFlag = true;

                        _logger.Debug($"Instruction Fetched: {"ei"}");
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

                            _cycles += 3;
                        }
                        _logger.Debug($"Instruction Fetched: {"ret cond"} with condition {conditionCode}");
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

                            _cycles += 1;
                        }
                        _logger.Debug($"Instruction Fetched: {"jp cond, n16"} with params: {lowByte}, {highByte} and condition {conditionCode}");
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

                            _cycles += 3;
                        }
                        _logger.Debug($"Instruction Fetched: {"call cc, n16"} with params: {lowByte}, {highByte} and condition {conditionCode}");
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

                        _logger.Debug($"Instruction Fetched: {"rst tgt3"} with vec3: {vec3}");
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

                            _logger.Debug($"Instruction Fetched: {"pop r16stk"} with param {registerCode}");

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

                            _logger.Debug($"Instruction Fetched: {"push r16stk"} with param {registerCode}");
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

                        _logger.Debug($"Instruction Fetched: {"rlc r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory.memoryMap[memoryPointer] & 0b1000_0000) >> 7);
                                _memory.memoryMap[memoryPointer] = (byte)((_memory.memoryMap[memoryPointer] << 1) | carryOut);

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)((register.Value << 1) | carryOut);

                                _flags.setZeroFlagZ(register.Value);
                            }


                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"rrc r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory.memoryMap[memoryPointer] & 0b0000_0001));
                                _memory.memoryMap[memoryPointer] = (byte)((_memory.memoryMap[memoryPointer] >> 1) | carryOut);

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b0000_0001));
                                register.Value = (byte)((register.Value >> 1) | carryOut);

                                _flags.setZeroFlagZ(register.Value);
                            }


                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"rl r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory.memoryMap[memoryPointer] & 0b1000_0000) >> 7);

                                _memory.memoryMap[memoryPointer] = (byte)((_memory.memoryMap[memoryPointer] << 1) | _flags.getCarryFlagC());

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)((register.Value << 1) | _flags.getCarryFlagC());

                                _flags.setZeroFlagZ(register.Value);
                            }


                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"rr r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)(_memory.memoryMap[memoryPointer] & 0b0000_0001);
                                _memory.memoryMap[memoryPointer] = (byte)((_memory.memoryMap[memoryPointer] >> 1) | (_flags.getCarryFlagC() << 7));

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)(register.Value & 0b0000_0001);
                                register.Value = (byte)((register.Value >> 1) | (_flags.getCarryFlagC() << 7));

                                _flags.setZeroFlagZ(register.Value);
                            }

                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"sla r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)((_memory.memoryMap[memoryPointer] & 0b1000_0000) >> 7);
                                _memory.memoryMap[memoryPointer] = (byte)(_memory.memoryMap[memoryPointer] << 1);

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)((register.Value & 0b1000_0000) >> 7);
                                register.Value = (byte)(register.Value << 1);

                                _flags.setZeroFlagZ(register.Value);
                            }

                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"sra r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                byte originalValue = _memory.memoryMap[memoryPointer];
                                carryOut = (byte)(originalValue & 0b0000_0001);
                                _memory.memoryMap[memoryPointer] = (byte)((originalValue >> 1) | (originalValue & 0b1000_0000));

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                byte originalValue = register.Value;
                                carryOut = (byte)(originalValue & 0b0000_0001);
                                register.Value = (byte)((originalValue >> 1) | (originalValue & 0b1000_0000));

                                _flags.setZeroFlagZ(register.Value);
                            }

                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"swap r8"} with param {registerCode}");

                        if (registerCode < registries.Count)
                        {
                            byte newValue;
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                byte firstFour = (byte)((_memory.memoryMap[memoryPointer] & 0b1111_0000) >> 4);
                                byte lastFour = (byte)((_memory.memoryMap[memoryPointer] & 0b0000_1111) << 4);


                                newValue = (byte)(firstFour | lastFour);
                                _memory.memoryMap[memoryPointer] = newValue;

                                _cycles += 2;
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                byte firstFour = (byte)((register.Value & 0b1111_0000) >> 4);
                                byte lastFour = (byte)((register.Value & 0b0000_1111) << 4);

                                newValue = (byte)(firstFour | lastFour);
                                register.Value = newValue;
                            }

                            _flags.setZeroFlagZ(newValue);
                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(0);
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

                        _logger.Debug($"Instruction Fetched: {"srl r8"} with param {registerCode}");

                        byte carryOut;

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                carryOut = (byte)(_memory.memoryMap[memoryPointer] & 0b0000_0001);
                                _memory.memoryMap[memoryPointer] = (byte)(_memory.memoryMap[memoryPointer] >> 1);

                                _cycles += 2;
                                _flags.setZeroFlagZ(_memory.memoryMap[memoryPointer]);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                carryOut = (byte)(register.Value & 0b0000_0001);
                                register.Value = (byte)(register.Value >> 1);

                                _flags.setZeroFlagZ(register.Value);
                            }

                            _flags.setSubtractionFlagN(0);
                            _flags.SetHalfCarryFlagH(0);
                            _flags.setCarryFlagC(carryOut);
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

                        _logger.Debug($"Instruction Fetched: {"bit b3, r8"} with param {registerCode} and index {bitIndex}");

                        if (registerCode < registries.Count)
                        {
                            byte bit;
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                bit = (byte)((_memory.memoryMap[memoryPointer] >> bitIndex) & 0b0000_0001);
                            }
                            else
                            {
                                ByteRegister register = registries[registerCode]!;

                                bit = (byte)((register.Value >> bitIndex) & 0b0000_0001);
                            }


                            _flags.setZeroFlagZ(bit);
                            _flags.setSubtractionFlagN(0);
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

                        _logger.Debug($"Instruction Fetched: {"res b3, r8"} with param {registerCode} and index {bitIndex}");

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory.memoryMap[memoryPointer] &= (byte)~(1 << bitIndex);
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

                        _logger.Debug($"Instruction Fetched: {"set b3, r8"} con parametro registro {registerCode} e indice bit {bitIndex}");

                        if (registerCode < registries.Count)
                        {
                            if (registerCode == 0b110)
                            {
                                ushort memoryPointer = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory.memoryMap[memoryPointer] |= (byte)(1 << bitIndex);

                                _cycles += 2;
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
            cc,
        }

        byte Fetch()
        {
            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

            if (pcValue > Memory.ROM_MAX_ADDRESS)
            {
                _logger.Fatal("Attempted to fetch beyond ROM boundaries.");
                throw new IndexOutOfRangeException("Program Counter exceeded ROM boundaries.");
            }

            byte nextByte = _memory.memoryMap[pcValue];

            pcValue++;

            _PC[0] = (byte)(pcValue & 0xFF);
            _PC[1] = (byte)(pcValue >> 8);

            return nextByte;
        }

        private void Decode()
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
                    _cycles += instruction?.Cycles ?? 0;
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

        public void Execute()
        {
            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
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
                _logger.Debug($"PC: {pcValue}: Values after operation: {_instructionRegister} " +
                    $"AF: {string.Join(",", _AF.Select(b => b.ToString()))}, " +
                    $"BC: {string.Join(",", _BC.Select(b => b.ToString()))}, " +
                    $"DE: {string.Join(",", _DE.Select(b => b.ToString()))}, " +
                    $"HL: {string.Join(",", _HL.Select(b => b.ToString()))}, " +
                    $"SP: {string.Join(",", _SP.Select(b => b.ToString()))}, " +
                    $"PC: {string.Join(",", _PC.Select(b => b.ToString()))}, " +
                    $"Cycles: {_cycles}");

                pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
            }
            _logger.Debug($"Memory dump: {string.Join(" - ", _memory.memoryMap.Select((value, index) => index > 0x7FFF ? $"{index:X4}:[{value:X2}]" : string.Empty).Where(s => !string.IsNullOrEmpty(s)))}");
        }
    }
}
