using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO;


namespace RomNamespace
{
    internal class Rom
    {
        private static Rom _rom = new Rom();
        private byte[]? romDump;
        private Rom() { }
        public static Rom GetRom()
        {
            return _rom;
        }

        public void loadFromCartrige()
        {
            string directoryPath = @"..\..\..";
            String[] files = Directory.GetFiles(directoryPath, "*.gb");
            String? bgFile = files.FirstOrDefault();

            if (bgFile == null)
            {
                Console.WriteLine("file not found");
                return;
            }

            romDump = File.ReadAllBytes(bgFile);
            Console.WriteLine($"{BitConverter.ToString(romDump)}");
        }
        public void validateRom()
        {

        }
    }
}
