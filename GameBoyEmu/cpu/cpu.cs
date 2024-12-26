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
using System.Reflection.Emit;
using static System.Runtime.InteropServices.JavaScript.JSType;


namespace GameBoyEmu.CpuNamespace
{
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

        private readonly Dictionary<paramsType, List<byte[]>> _16bitsRegistries;
        private readonly Dictionary<paramsType, List<byte>> _8bitsRegistries;

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
            {                                                                                //0 because it's [hl] and will be handled separatly
                {paramsType.r8, new List<byte>{_BC[0], _BC[1], _DE[0], _DE[1], _HL[0], _HL[1], 0, _AF[1] } },
            };
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

                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                    return new Instruction("LD [r16mem], A", 4, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                            byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                        byte registerCode = (byte)((_instructionRegister & 0b0011_0000) >> 4);
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
                    return new Instruction("INC R8", 1, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_1000) >> 3);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte newValue;

                            _logger.Debug($"Instruction Fetched: {"INC R8"}, register {registerCode}");
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory.memoryMap[value];
                                newValue++;
                                _memory.memoryMap[value] = newValue;
                            }
                            else
                            {
                                registries[registerCode] = (byte)(registries[registerCode] + 1);
                                newValue = registries[registerCode];
                            }

                            _flags.SetHalfCarryFlagH((ushort)(newValue - 1), 1, true, false);
                            _flags.setSubtractionFlagN(0);
                            _flags.setZeroFlagZ(newValue);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[INC R8] an error occurred");
                        }
                    });
                case 0b101:
                    return new Instruction("DEC R8", 1, () =>
                    {
                        byte registerCode = (byte)((_instructionRegister & 0b0011_1000) >> 3);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            _logger.Debug($"Instruction Fetched: {"DEC R8"}, register {registerCode}");
                            byte newValue;
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);
                                newValue = _memory.memoryMap[value];
                                newValue--;
                                _memory.memoryMap[value] = newValue;
                            }
                            else
                            {
                                registries[registerCode] = (byte)(registries[registerCode] -= 1);
                                newValue = registries[registerCode];
                            }
                            _flags.SetHalfCarryFlagH((ushort)(newValue + 1), 1, false, false);
                            _flags.setSubtractionFlagN(1);
                            _flags.setZeroFlagZ(newValue);
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[DEC R8] an error occurred");
                        }
                    });
                case 0b110:
                    return new Instruction("LD r8, Imm8", 2, () =>
                    {
                        byte imm8 = Fetch();

                        byte registerCode = (byte)((_instructionRegister & 0b0011_1000) >> 3);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            _logger.Debug($"Instruction Fetched: {"LD r8, Imm8"}, value: {imm8} with register {registerCode}");
                            if (registerCode == 0b110)
                            {
                                ushort value = (ushort)((_HL[1] << 8) | _HL[0]);

                                _memory.memoryMap[value] = imm8;
                            }
                            else
                            {
                                registries[registerCode] = imm8;
                            }
                        }
                        else
                        {
                            throw new InstructionExcecutionException("[LD r8, Imm8] an error occurred");
                        }
                    });
                case 0b000:
                    return new Instruction("jr cond, imm8", 3, () =>
                    {
                        byte ConditionCode = (byte)((_instructionRegister & 0b0001_1000) >> 4);
                        if (getCc(ConditionCode))
                        {
                            sbyte offest = (sbyte)Fetch();
                            ushort pcValue = (ushort)((_PC[1] << 8) | _PC[0]);

                            ushort newPcValue = (ushort)(pcValue + offest);

                            _logger.Debug($"Instruction Fetched: {"jr cond, imm8"}, with cc: {ConditionCode}, pcValue: {pcValue}, offset: {offest} and new pcValue: {newPcValue}");

                            _PC[0] = (byte)(newPcValue & 0xFF);
                            _PC[1] = (byte)(newPcValue >> 8);
                        }
                        else
                        {
                            _logger.Debug($"Instruction Fetched: {"jr cond, imm8"}, with cc: {ConditionCode}");
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
                        byte registerCodeDestination = (byte)((_instructionRegister & 0b0011_1000) >> 3);
                        byte registerCodeSource = (byte)((_instructionRegister & 0b0000_0111));
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

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
                                    _memory.memoryMap[hlAddress] = registries[registerCodeSource];
                                }
                                else if (registerCodeSource == 0b110)
                                {
                                    ushort hlAddress = (ushort)((_HL[1] << 8) | _HL[0]);
                                    registries[registerCodeDestination] = _memory.memoryMap[hlAddress];
                                }
                                else
                                {
                                    registries[registerCodeDestination] = registries[registerCodeSource];
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
                        byte registerCode = (byte)((_instructionRegister & 0b0000_0111));
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            _logger.Debug($"Instruction Fetched: {"add a, r8"}, register {registerCode}");
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = _memory.memoryMap[pointerValue];
                            }
                            else
                            {
                                valueToAdd = registries[registerCode];
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            _logger.Debug($"Instruction Fetched: {"adc a, r8"}, register {registerCode}");
                            byte valueToAdd;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                valueToAdd = (byte)(_memory.memoryMap[pointerValue] + _flags.getCarryFlagC());
                            }
                            else
                            {
                                valueToAdd = (byte)(registries[registerCode] + _flags.getCarryFlagC());
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = _memory.memoryMap[pointerValue];
                            }
                            else
                            {
                                operand = registries[registerCode];
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory.memoryMap[pointerValue] + _flags.getCarryFlagC());
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode] + _flags.getCarryFlagC());
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory.memoryMap[pointerValue]);
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]);
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory.memoryMap[pointerValue]);
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]);
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = (byte)(_memory.memoryMap[pointerValue]);
                            }
                            else
                            {
                                operand = (byte)(registries[registerCode]);
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
                        byte registerCode = (byte)(_instructionRegister & 0b0000_0111);
                        List<Byte> registries = _8bitsRegistries[paramsType.r8];

                        if (registerCode < registries.Count)
                        {
                            byte operand;
                            if (registerCode == 0b110)
                            {
                                ushort pointerValue = (ushort)((_HL[1] << 8) | _HL[0]);
                                operand = _memory.memoryMap[pointerValue];
                            }
                            else
                            {
                                operand = registries[registerCode];
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
                byte block = (byte)(_instructionRegister & (0b1100_0000)); // read the instruction block

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
                }
                else
                {
                    _logger.Warn($"Instruction [{_instructionRegister}] not found");
                }
            }
            catch (InstructionExcecutionException IntrExEx)
            {
                _logger.Fatal(IntrExEx.Message);
                throw new InstructionExcecutionException();
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
                _logger.Debug($"{pcValue}: Values after operation: {_instructionRegister} " +
                    $"AF: {BitConverter.ToString(_AF)}, " + $"BC: {BitConverter.ToString(_BC)}, " +
                    $"DE: {BitConverter.ToString(_DE)}, " + $"HL: {BitConverter.ToString(_HL)}, " +
                    $"SP: {BitConverter.ToString(_SP)}, " + $"PC: {BitConverter.ToString(_PC)}");

                pcValue = (ushort)((_PC[1] << 8) | _PC[0]);
            }
            _logger.Debug($"Memory dump: {string.Join("-", _memory.memoryMap)}");
        }
    }
}
