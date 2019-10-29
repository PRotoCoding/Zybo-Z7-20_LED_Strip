﻿using System;
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

        public void ApplyFixColor(byte r, byte g, byte b)
        {
            for(int i = 0; i < NumberOfLeds; i++)
            {
                Leds[i] = new Led(g, r, b);
            }
        }

        public void ApplyColorGradient(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            for(int i = 0; i < NumberOfLeds; i++)
            {
                double val = (double)i / NumberOfLeds;
                Leds[i] = new Led(Convert.ToByte(((double)(g2 - g1)) / NumberOfLeds * i + g1), Convert.ToByte(((double)(r2 - r1)) / NumberOfLeds * i + r1), Convert.ToByte(((double)(b2 - b1)) / NumberOfLeds * i + b1));
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
