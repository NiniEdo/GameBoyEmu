using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.FlagsHelperNamespace
{
    internal class FlagsHelper
    {
        private byte[] _AF;
        public FlagsHelper(ref byte[] AF)
        {
            _AF = AF;
        }

        public void SetZeroFlagZ(uint result)
        {
            int intValue = result == 0 ? 1 : 0;
            _AF[0] = (byte)((_AF[0] & 0b0111_1111) | (intValue << 7));
        }
        public void SetZeroFlagDirectlyZ(byte result)
        {
            _AF[0] = (byte)((_AF[0] & 0b0111_1111) | (result << 7));
        }

        public void SetSubtractionFlagN(byte value)
        {
            _AF[0] = (byte)((_AF[0] & 0b1011_1111) | (value << 6));
        }

        public void SetHalfCarryFlagH(ushort operand1, ushort operand2, bool isAddition, bool is16Bit)
        {
            bool halfCarry;
            if (is16Bit)
            {
                if (isAddition)
                {
                    halfCarry = (((operand1 & 0x0FFF) + (operand2 & 0x0FFF)) & 0x1000) != 0;
                }
                else
                {
                    halfCarry = (operand1 & 0x0FFF) < (operand2 & 0x0FFF);
                }
            }
            else
            {
                byte op1 = (byte)operand1;
                byte op2 = (byte)operand2;

                if (isAddition)
                {
                    halfCarry = (((op1 & 0x0F) + (op2 & 0x0F)) & 0x10) != 0;
                }
                else
                {
                    halfCarry = (op1 & 0x0F) < (op2 & 0x0F);
                }
            }

            if (halfCarry)
            {
                _AF[0] |= 0b0010_0000;
            }
            else
            {
                _AF[0] &= 0b1101_1111;
            }
        }

        public void SetHalfCarryFlagH(byte value)
        {
            _AF[0] = (byte)((_AF[0] & 0b1101_1111) | (value << 5));
        }

        public void SetCarryFlagC(int value, bool is16bits, bool carryOut)
        {
            int intValue = 0;
            if (!carryOut)
            {
                if (value >= 0)
                {
                    if (is16bits)
                        intValue = value > 0xFFFF ? 1 : 0;
                    else
                        intValue = value > 0xFF ? 1 : 0;
                }
                else { intValue = 1; }
            }
            else { intValue = 1; }

            _AF[0] = (byte)((_AF[0] & 0b1110_1111) | (intValue << 4));
        }

        public void SetCarryFlagC(int value)
        {
            _AF[0] = (byte)((_AF[0] & 0b1110_1111) | (value << 4));
        }
        public byte GetZeroFlagZ()
        {
            return (byte)((_AF[0] & 0b1000_0000) >> 7);
        }
        public byte GetSubtractionFlagN()
        {
            return (byte)((_AF[0] & 0b0100_0000) >> 6);
        }
        public byte GetHalfCarryFlagH()
        {
            return (byte)((_AF[0] & 0b0010_0000) >> 5);
        }
        public byte GetCarryFlagC()
        {
            return (byte)((_AF[0] & 0b0001_0000) >> 4);
        }
    }
}
