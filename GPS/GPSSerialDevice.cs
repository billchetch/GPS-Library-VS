using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Ports;

namespace Chetch.GPS
{
    public class GPSSerialDevice : IDisposable
    {
        public const String GPS_GNSS_GENERIC = "GPS/GNSS";
        public const String UBLOX_7_GPS = "u-blox 7 GPS";

        public delegate void NewSentenceReceivedEventHandler(String sentence);

        public event NewSentenceReceivedEventHandler NewSentenceReceived;

        private String deviceDescription;
        private SerialPort serialPort;
        private String portName;
        private int baudRate;
        private int dataBits;
        private Parity parity;
        private StopBits stopBits;

        public GPSSerialDevice(String deviceDescription, int baudRate = 9600, int dataBits = 8, Parity parity = Parity.None, StopBits stopBits = StopBits.One)
        {
            this.deviceDescription = deviceDescription;
            this.baudRate = baudRate;
            this.dataBits = dataBits;
            this.parity = parity;
            this.stopBits = stopBits;
        }

        public String PortName { get { return this.portName;  } }

        public void Dispose(bool disposing)
        {
            if (disposing && (serialPort != null))
            {
                serialPort.DataReceived -= new SerialDataReceivedEventHandler(SerialPortDataReceived);
            }

            if(serialPort != null)
            {
                if (serialPort.IsOpen)
                    serialPort.Close();

                serialPort.Dispose();
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        public Boolean IsListening
        {
            get {
                if(serialPort == null) return false;
                if (!serialPort.IsOpen) return false;

                String p = getPortNameFromDescription(deviceDescription);
                return p != null;
            }
        }

        private String getPortNameFromDescription(String description)
        {
            List<String> gpsPorts = Utilities.SerialPorts.Find(description);
            if(gpsPorts.Count == 1)
            {
                return gpsPorts[0];
            } else
            {
                return null;
            }
        }

        public void StartListening()
        {
            // Closing serial port if it is open
            if (serialPort != null && serialPort.IsOpen)
                serialPort.Close();
            
            portName = getPortNameFromDescription(deviceDescription);
            if (portName != null)
            {
                serialPort = new SerialPort(portName, baudRate, parity, dataBits, stopBits);

                serialPort.Open();

                // Subscribe to event
                serialPort.DataReceived += new SerialDataReceivedEventHandler(SerialPortDataReceived);
            }
            else
            {
                throw new Exception("No port found for " + deviceDescription);
            }

        }

        /// <summary>
        /// Closes the serial port
        /// </summary>
        public void StopListening()
        {
            if (serialPort != null)
            {
                serialPort.Close();
            }
        }

        
        void SerialPortDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int dataLength = serialPort.BytesToRead;
            byte[] data = new byte[dataLength];
            int nbrDataRead = serialPort.Read(data, 0, dataLength);
            if (nbrDataRead == 0)
                return;

            string str = Encoding.ASCII.GetString(data);
            String[] sentences = str.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.None);
            foreach (String sentence in sentences)
            {
                if(NewSentenceReceived != null)
                    NewSentenceReceived(sentence);
            }
        }
    }
}
