using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NLog;
using GameBoyEmu.Exceptions;
using static System.Net.Mime.MediaTypeNames;

namespace GameBoyEmu.CartridgeNamespace
{
    internal class Cartridge
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        private byte[]? romDump; 
        //TODO: Implement switchable rom and mbc
        public Cartridge() { }

        public byte[] loadRomFromCartridge()
        {
            string directoryPath = @"..\..\..";
            String[] files = Directory.GetFiles(directoryPath, "*.gb");
            String? bgFile = files.FirstOrDefault();

            if (bgFile == null)
            {
                throw new CartridgeException("Cartrige not found");
            }

            try
            {
                romDump = File.ReadAllBytes(bgFile);
            }
            catch (IOException IOex)
            {
                throw new CartridgeException("Failed to read cartridge: " + IOex.Message);
            }

            readHeaderData();

            return romDump;
        }
        private void readHeaderData()
        {

        }
    }
}
