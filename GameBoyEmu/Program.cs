using GameBoyEmu;
using RomNamespace;

namespace GameBoyEmu
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Rom rom = Rom.GetRom();
            rom.loadFromCartrige();
        }
    }
}
