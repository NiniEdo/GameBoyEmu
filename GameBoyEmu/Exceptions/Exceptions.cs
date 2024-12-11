using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace GameBoyEmu.Exceptions
{
    public class CartridgeException : Exception
    {
        public CartridgeException() { }
        public CartridgeException(string message)
            : base(message) { }
    }
}
