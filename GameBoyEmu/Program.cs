using GameBoyEmu.RomNamespace;
using GameBoyEmu;
using GameBoyEmu.CpuNamespace;
using System.Runtime.CompilerServices;

namespace GameBoyEmu
{
    internal class Program
    {

        //TODO: add boot rom and change the initialization of PC to 0000
        static void Main(string[] args)
        {
            Cpu cpu = new Cpu();
            cpu.Execute();
        }
    }
}
