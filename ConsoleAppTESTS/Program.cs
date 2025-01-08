using GameBoyEmu;
using GameBoyEmu.CpuNamespace;
using GameBoyEmu.MemoryNamespace;
using NLog;
namespace ConsoleAppTESTS
{
    internal class Program
    {
        static void Main(string[] args)
        {
            

            Tests test = new Tests();

            test.Start();
        }
    }
}
