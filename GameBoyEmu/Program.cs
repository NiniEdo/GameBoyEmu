using GameBoyEmu.RomNamespace;
using GameBoyEmu;
using GameBoyEmu.CpuNamespace;
using System.Runtime.CompilerServices;

namespace GameBoyEmu
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Cpu cpu = Cpu.GetCpu();
            cpu.execute();
        }
    }
}
