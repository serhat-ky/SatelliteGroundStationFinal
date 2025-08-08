using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace SatelliteGroundStation.Services
{
    public class SerialCommunicationService : IDisposable
    {
        private SerialPort? _serialPort;
        private StringBuilder _dataBuffer = new StringBuilder();

        public event EventHandler<string>? DataReceived;
        public event EventHandler<bool>? ConnectionChanged;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public void Connect(string portName, int baudRate)
        {
            try
            {
                Console.WriteLine($"🔧 SerialService: Connecting to {portName} at {baudRate} baud...");

                if (_serialPort?.IsOpen == true)
                {
                    Console.WriteLine("🔧 SerialService: Closing existing connection...");
                    _serialPort.Close();
                    _serialPort.Dispose();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 1000,
                    WriteTimeout = 1000,
                    Encoding = Encoding.ASCII,
                    DtrEnable = true,  // Important for Arduino R4
                    RtsEnable = true   // Important for Arduino R4
                };

                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.ErrorReceived += OnSerialErrorReceived;
                _serialPort.Open();

                // Clear any existing data
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
                _dataBuffer.Clear();

                Console.WriteLine("✅ SerialService: Port opened successfully");
                ConnectionChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ SerialService: Connection failed - {ex.Message}");
                ConnectionChanged?.Invoke(this, false);
                throw new InvalidOperationException($"COM port bağlantı hatası: {ex.Message}", ex);
            }
        }

        public void Disconnect()
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.DataReceived -= OnSerialDataReceived;
                    _serialPort.ErrorReceived -= OnSerialErrorReceived;
                    _serialPort.Close();
                }
                _serialPort?.Dispose();
                _serialPort = null;
                _dataBuffer.Clear();

                Console.WriteLine("🔧 SerialService: Disconnected successfully");
                ConnectionChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Disconnect error: {ex.Message}");
            }
        }

        public void SendCommand(string command)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    Console.WriteLine($"📤 Sending command: {command}");
                    _serialPort.WriteLine(command);
                }
                else
                {
                    Console.WriteLine("❌ Cannot send command: Port not open");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Send command error: {ex.Message}");
            }
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    // Read all available data
                    string incomingData = _serialPort.ReadExisting();
                    Console.WriteLine($"📡 Raw received: '{incomingData}'");

                    // Add to buffer
                    _dataBuffer.Append(incomingData);

                    // Process complete lines
                    string bufferContent = _dataBuffer.ToString();
                    string[] lines = bufferContent.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);

                    if (bufferContent.EndsWith("\r") || bufferContent.EndsWith("\n"))
                    {
                        // All lines are complete
                        _dataBuffer.Clear();
                        ProcessCompleteLines(lines);
                    }
                    else if (lines.Length > 1)
                    {
                        // All but the last line are complete
                        _dataBuffer.Clear();
                        _dataBuffer.Append(lines[lines.Length - 1]); // Keep the incomplete line
                        ProcessCompleteLines(lines.Take(lines.Length - 1).ToArray());
                    }
                    // If only one line and not ending with newline, keep in buffer
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Data receive error: {ex.Message}");
            }
        }

        private void ProcessCompleteLines(string[] lines)
        {
            foreach (string line in lines)
            {
                if (!string.IsNullOrWhiteSpace(line))
                {
                    Console.WriteLine($"📋 Processing line: '{line.Trim()}'");
                    DataReceived?.Invoke(this, line.Trim());
                }
            }
        }

        private void OnSerialErrorReceived(object sender, SerialErrorReceivedEventArgs e)
        {
            Console.WriteLine($"❌ Serial error: {e.EventType}");
        }

        public List<string> GetAvailablePorts()
        {
            try
            {
                var ports = SerialPort.GetPortNames().ToList();
                Console.WriteLine($"📍 Available ports: {string.Join(", ", ports)}");
                return ports;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error getting ports: {ex.Message}");
                return new List<string>();
            }
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}