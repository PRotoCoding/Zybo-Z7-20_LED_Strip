using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZyboLogic
{
    public class Led
    {
        public byte Green;
        public byte Red;
        public byte Blue;

        public Led(byte green, byte red, byte blue)
        {
            Green = green;
            Red = red;
            Blue = blue;
        }

        byte[] ToByteArray()
        {
            return new byte[] { Green, Red, Blue, 0 };
        }

        public override string ToString()
        {
            return BitConverter.ToString(ToByteArray());
        }
    }
}
