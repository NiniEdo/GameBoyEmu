using GameBoyEmu.CartridgeNamespace;
using GameBoyEmu;
using GameBoyEmu.CpuNamespace;
using System.Runtime.CompilerServices;
using NLog;

namespace GameBoyEmu
{
    internal class Program
    {
        static void Main(string[] args)
        {

            Logger _logger = LogManager.GetCurrentClassLogger();

            GameBoy _gameboy;
            try
            {
                _gameboy = new GameBoy();
                _gameboy.Start();
            }
            catch (Exception ex)
            {
                _logger.Error($"Emulation stopped: {ex.Message}");
            }

        }
    }
}
