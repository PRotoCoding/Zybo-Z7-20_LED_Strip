


using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;


namespace ZyboLogic
{
    public class Zybo
    {
        const byte COMMAND_ID_MASK = 0xFF;
        const byte COMMAND_SET_SINGLE_LED = 0x01;
        const byte COMMAND_SET_MULTIPLE_LEDS = 0x02;
        const ushort COMMAND_SET_MULTIPLE_LEDS_NUMBER_MASK = 0xFF00;
        const byte COMMAND_SET_MULTIPLE_LEDS_NUMBER_OFFSET = 8;

        public event EventHandler<List<String>> SerialPortsChanged;
        public event EventHandler Connected;
        public event EventHandler Disconnected;

        public List<String> ports;
        public System.IO.Ports.SerialPort SerialMaster;
        
        public Zybo()
        {
            RefreshSerialPorts();
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
            byte[] command = { 0, 0, Convert.ToByte(ledStrip.NumberOfLeds), COMMAND_SET_MULTIPLE_LEDS };
            byte[] data = ledStrip.ToByteArray();

            SerialMaster.Write(command, 0, command.Count());
            SerialMaster.Write(data, 0, data.Count());
            
        }
    }
}
