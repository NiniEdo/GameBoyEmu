using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using GameBoyEmu.MemoryNamespace;


namespace ConsoleAppTESTS
{
    internal class TestMemory : Memory
    {
        private byte[] _memoryMap = new byte[0x1_0000]; //2^16 addresses (65.536)
        public TestMemory() :base(true)
        { 
        }

        public override byte this[ushort address]
        {
            get
            {
                return _memoryMap[address];
            }
            set
            {
                _memoryMap[address] = value;
            }
        }

        public void Clear()
        {
            Array.Clear(_memoryMap);
        }
    }
}
