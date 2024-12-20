using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using NLog;
using GameBoyEmu.Exceptions;
using static System.Net.Mime.MediaTypeNames;

namespace GameBoyEmu.RomNamespace
{
    internal class Rom
    {
        private Logger logger = LogManager.GetCurrentClassLogger();
        private static Rom _rom = new Rom();
        private byte[]? romDump; 

        public Rom() { }

        public byte[] loadFromCartridge()
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

            return romDump;
        }
        public void validateRom()
        {

        }
    }
}
