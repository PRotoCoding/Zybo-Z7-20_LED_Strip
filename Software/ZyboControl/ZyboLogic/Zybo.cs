


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;
using Utilities;
using System.Diagnostics;
using System.Drawing;

namespace ZyboLogic
{
    public class LedColor { public byte r, g, b; }

    public class Zybo
    {
        const byte COMMAND_ID_MASK = 0xFF;
        const byte COMMAND_SET_SINGLE_LED = 0x01;
        const byte COMMAND_SET_MULTIPLE_LEDS = 0x02;
        const ushort COMMAND_SET_MULTIPLE_LEDS_NUMBER_MASK = 0xFF00;
        const byte COMMAND_SET_MULTIPLE_LEDS_NUMBER_OFFSET = 8;

        const byte COMMAND_RESPONSE_OK = 0xAA;

        const uint NUMBER_OF_LEDS = 144;

        public event EventHandler<List<String>> SerialPortsChanged;
        public event EventHandler Connected;
        public event EventHandler Disconnected;
        public event EventHandler<Led[]> LedColorChanged;

        public List<String> ports;
        public System.IO.Ports.SerialPort SerialMaster;
        public FFT Fft;

        private bool WaitingForResponse;

        public LedStrip FixColorStrip;

        public Zybo()
        {
            Fft = new FFT();
            Fft.NewFftDataAvailable += Fft_NewFftDataAvailable;
            RefreshSerialPorts();
            FixColorStrip = new LedStrip();
        }

        private void Fft_NewFftDataAvailable(object sender, double[] e)
        {
            uint compressionSize = (uint) e.Length / NUMBER_OF_LEDS;
            for(int ledCount = 0; ledCount < NUMBER_OF_LEDS; ledCount++)
            {
                double sum = 0;
                for (int pointCount = 0; pointCount < compressionSize; pointCount++)
                {
                    sum += e[ledCount * compressionSize + pointCount];
                }
                double product = sum * 100;
                uint productInt = (uint) product;
                if(productInt > Math.Pow(2, 24))
                {
                    Console.WriteLine("Number too big");
                }
                uint result = (productInt >> 8) & 0xFF;

                double h = product * 2;
                double v = product / 10;
                //Console.Write($"H: {h}, V:{v}");
                //Console.WriteLine();
                HsvToRgb(h, 1.0, v, out int r, out int g, out int b);
                FixColorStrip.Leds[ledCount] = new Led(Convert.ToByte(g & 0xFF), Convert.ToByte(r & 0xFF), Convert.ToByte(b & 0xFF));
                //FixColorStrip.Leds[ledCount] = new Led(Convert.ToByte((productInt >> 16) & 0xFF), Convert.ToByte((productInt >> 8) & 0xFF), Convert.ToByte((productInt >> 0) & 0xFF));
            }
            SendMultipleLeds(FixColorStrip);
        }

        public void RefreshSerialPorts()
        {
            ports = new List<string>();
            foreach (string s in SerialPort.GetPortNames())
            {
                ports.Add(s);
            }
            ports.Sort();
            SerialPortsChanged?.Invoke(this, ports);
        }

        public void SetupPort(string portName)
        {
            if (ports.Contains(portName))
            {
                SerialMaster = new SerialPort();
                SerialMaster.PortName = portName;
                SerialMaster.BaudRate = 115200;
                SerialMaster.DataBits = 8;
                SerialMaster.Parity = Parity.None;
                SerialMaster.StopBits = StopBits.One;
                SerialMaster.WriteBufferSize = 1000;

                SerialMaster.ReadTimeout = 2000;
                SerialMaster.WriteTimeout = 2000;

                SerialMaster.Open();
                SerialMaster.DiscardNull.ToString();
                SerialMaster.Encoding.ToString();
                while(SerialMaster.BytesToRead > 0) { SerialMaster.ReadByte(); }
                SerialMaster.DataReceived += (o, e) =>
                {
                    int resp = SerialMaster.ReadByte();
                    if (resp == COMMAND_RESPONSE_OK)
                    {
                        WaitingForResponse = false;
                    }
                    else
                    {
                        throw new Exception("Invalid Response received");
                    }
                };
            }
            else
            {
                //throw new SystemException("Port name " + portName + " not existent");
                Disconnected?.Invoke(this, null);
            }
            Connected?.Invoke(this, null);
        }

        public void SendData()
        {
            if(SerialMaster.IsOpen)
            {
                //SerialMaster.Write("A");
                SendMultipleLeds(new LedStrip());
            }
        }

        public void SendMultipleLeds(LedStrip ledStrip)
        {
            if(SerialMaster != null && SerialMaster.IsOpen && !WaitingForResponse)
            {
                byte[] command = { 0, 0, Convert.ToByte(ledStrip.NumberOfLeds), COMMAND_SET_MULTIPLE_LEDS };
                byte[] data = ledStrip.ToByteArray();

                SerialMaster.Write(command, 0, command.Count());
                SerialMaster.Write(data, 0, data.Count());
            }
            LedColorChanged?.Invoke(this, ledStrip.Leds);
        }

        public void SetColorGradient(byte r1, byte g1, byte b1, byte r2, byte g2, byte b2)
        {
            FixColorStrip.ApplyColorGradient(r1, g1, b1, r2, g2, b2);
            SendMultipleLeds(FixColorStrip);
        }

        public void SetFixColor(byte r, byte g, byte b)
        {
            FixColorStrip.ApplyFixColor(r, g, b);
            SendMultipleLeds(FixColorStrip);
        }

        public async Task<int> ProgramDevice()
        {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = @"G:\Xilinx\Vivado\2018.3\bin\vivado.bat";
            p.StartInfo.Arguments = @"-mode batch -source G:\Vivado_Projects\Zybo-Z7-20_LED_Strip\Zybo-Z7-20_LED_Strip\Software\Scripts\ProgramFpga.tcl";
            return await RunProcessAsync(p).ConfigureAwait(false);
        }

        public async Task<int> ProgramCore0()
        {
            // Start the child process.
            Process p = new Process();
            // Redirect the output stream of the child process.
            p.StartInfo.UseShellExecute = false;
            p.StartInfo.CreateNoWindow = true;
            p.StartInfo.RedirectStandardOutput = true;
            p.StartInfo.RedirectStandardError = true;
            p.StartInfo.FileName = @"G:\Xilinx\SDK\2018.3\bin\xsct.bat";
            p.StartInfo.Arguments = @"G:\Vivado_Projects\Zybo-Z7-20_LED_Strip\Zybo-Z7-20_LED_Strip\Software\Scripts\ProgramCore0.tcl";
            return await RunProcessAsync(p).ConfigureAwait(false);
        }

        private static Task<int> RunProcessAsync(Process process)
        {
            var tcs = new TaskCompletionSource<int>();

            process.Exited += (s, ea) => tcs.SetResult(process.ExitCode);
            process.OutputDataReceived += (s, ea) => Console.WriteLine(ea.Data);
            process.ErrorDataReceived += (s, ea) => Console.WriteLine(ea.Data);

            bool started = process.Start();
            if (!started)
            {
                //you may allow for the process to be re-used (started = false) 
                //but I'm not sure about the guarantees of the Exited event in such a case
                throw new InvalidOperationException("Could not start process: " + process);
            }

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return tcs.Task;
        }

        void HsvToRgb(double h, double S, double V, out int r, out int g, out int b)
        {
            double H = h;
            while (H < 0) { H += 360; };
            while (H >= 360) { H -= 360; };
            double R, G, B;
            if (V <= 0)
            { R = G = B = 0; }
            else if (S <= 0)
            {
                R = G = B = V;
            }
            else
            {
                double hf = H / 60.0;
                int i = (int)Math.Floor(hf);
                double f = hf - i;
                double pv = V * (1 - S);
                double qv = V * (1 - S * f);
                double tv = V * (1 - S * (1 - f));
                switch (i)
                {

                    // Red is the dominant color

                    case 0:
                        R = V;
                        G = tv;
                        B = pv;
                        break;

                    // Green is the dominant color

                    case 1:
                        R = qv;
                        G = V;
                        B = pv;
                        break;
                    case 2:
                        R = pv;
                        G = V;
                        B = tv;
                        break;

                    // Blue is the dominant color

                    case 3:
                        R = pv;
                        G = qv;
                        B = V;
                        break;
                    case 4:
                        R = tv;
                        G = pv;
                        B = V;
                        break;

                    // Red is the dominant color

                    case 5:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // Just in case we overshoot on our math by a little, we put these here. Since its a switch it won't slow us down at all to put these here.

                    case 6:
                        R = V;
                        G = tv;
                        B = pv;
                        break;
                    case -1:
                        R = V;
                        G = pv;
                        B = qv;
                        break;

                    // The color is not defined, we should throw an error.

                    default:
                        //LFATAL("i Value error in Pixel conversion, Value is %d", i);
                        R = G = B = V; // Just pretend its black/white
                        break;
                }
            }
            r = Clamp((int)(R * 255.0));
            g = Clamp((int)(G * 255.0));
            b = Clamp((int)(B * 255.0));
        }

        /// <summary>
        /// Clamp a value to 0-255
        /// </summary>
        int Clamp(int i)
        {
            if (i < 0) return 0;
            if (i > 255) return 255;
            return i;
        }

    }
}
