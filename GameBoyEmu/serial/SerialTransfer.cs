using GameBoyEmu.InterruptNamespace;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using NLog;

namespace GameBoyEmu.SerialTransferNamespace
{
    internal class SerialTransfer
    {
        private static SerialTransfer? _instance;
        private Logger _logger = LogManager.GetCurrentClassLogger();
        private Interrupts _interrupts = Interrupts.GetInstance();

        public const ushort SB_ADDRESS = 0xFF01;
        public const ushort SC_ADDRESS = 0xFF02;

        byte _sb;
        byte _sc;

        public byte Sb { get => _sb; set => _sb = value; }
        public byte Sc
        {
            get => _sc;
            set { _sc = value; Send(); }
        }

        public static SerialTransfer GetInstance()
        {
            if (_instance == null)
            {
                _instance = new SerialTransfer();
            }
            return _instance;
        }

        string _charBuffer = "";
        private void Send()
        {
            if ((_sc & 0b1000_0000) >> 7 == 1)
            {
                char outputChar = (char)_sb;

                if (outputChar == '\n')
                {
                    _logger.Info(_charBuffer);
                    _charBuffer = "";
                }
                else
                {
                    _charBuffer += outputChar;
                }


                _sc &= 0x7F;
                _interrupts.RequestSerialInterrupt();
            }
        }

    }
}
