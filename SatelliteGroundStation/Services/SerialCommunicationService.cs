using System;
using System.IO.Ports;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq; // bu satırı da ekliyoruz

namespace SatelliteGroundStation.Services
{
    public class SerialCommunicationService
    {
        private SerialPort? _serialPort;

        public event EventHandler<string>? DataReceived;
        public event EventHandler<bool>? ConnectionChanged;

        public bool IsConnected => _serialPort?.IsOpen ?? false;

        public void Connect(string portName, int baudRate)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }

                _serialPort = new SerialPort(portName, baudRate, Parity.None, 8, StopBits.One)
                {
                    ReadTimeout = 500,
                    WriteTimeout = 500,
                    NewLine = "\n"
                };

                _serialPort.DataReceived += OnSerialDataReceived;
                _serialPort.Open();

                ConnectionChanged?.Invoke(this, true);
            }
            catch (Exception ex)
            {
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
                    _serialPort.Close();
                }
                _serialPort?.Dispose();
                _serialPort = null;

                ConnectionChanged?.Invoke(this, false);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Disconnect error: {ex.Message}");
            }
        }

        public void SendCommand(string command)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.WriteLine(command);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Send command error: {ex.Message}");
            }
        }

        private void OnSerialDataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    string data = _serialPort.ReadLine();
                    DataReceived?.Invoke(this, data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Data receive error: {ex.Message}");
            }
        }

        public List<string> GetAvailablePorts()
        {
            return SerialPort.GetPortNames().ToList();
        }

        public void Dispose()
        {
            Disconnect();
        }
    }
}
