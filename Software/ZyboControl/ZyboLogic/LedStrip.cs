using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ZyboLogic
{
    public class LedStrip
    {
        public Led[] Leds;
        public uint NumberOfLeds = 144;

        public LedStrip()
        {
            Leds = new Led[NumberOfLeds];
            for(int i = 0; i < NumberOfLeds; i++)
            {
                double val = (double) i / NumberOfLeds;
                Leds[i] = new Led(Convert.ToByte(255.0 * (1.0 - val)), 0, Convert.ToByte(255.0 * val));
            }
        }

        public byte[] ToByteArray()
        {
            int i = 0;
            byte[] returnBytes = new byte[Leds.Count() * 4];
            foreach(Led led in Leds)
            {
                returnBytes[i++] = led.Green;
                returnBytes[i++] = led.Red;
                returnBytes[i++] = led.Blue;
                returnBytes[i++] = 0;
            }
            return returnBytes;
        }
    }
}
