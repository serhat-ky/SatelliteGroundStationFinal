 using System;
using System.ComponentModel;

namespace SatelliteGroundStation.Models
{
    public class TelemetryData : INotifyPropertyChanged
    {
        private int _packetNumber;
        private DateTime _timestamp;
        private double _temperature;
        private double _pressure;
        private double _altitude;
        private double _speed;
        private double _batteryVoltage;
        private double _gpsLatitude;
        private double _gpsLongitude;
        private double _gyroX;
        private double _gyroY;
        private double _gyroZ;
        private double _accelX;
        private double _accelY;
        private double _accelZ;

        public int PacketNumber
        {
            get => _packetNumber;
            set { _packetNumber = value; OnPropertyChanged(nameof(PacketNumber)); }
        }

        public DateTime Timestamp
        {
            get => _timestamp;
            set { _timestamp = value; OnPropertyChanged(nameof(Timestamp)); }
        }

        public double Temperature
        {
            get => _temperature;
            set { _temperature = value; OnPropertyChanged(nameof(Temperature)); }
        }

        public double Pressure
        {
            get => _pressure;
            set { _pressure = value; OnPropertyChanged(nameof(Pressure)); }
        }

        public double Altitude
        {
            get => _altitude;
            set { _altitude = value; OnPropertyChanged(nameof(Altitude)); }
        }

        public double Speed
        {
            get => _speed;
            set { _speed = value; OnPropertyChanged(nameof(Speed)); }
        }

        public double BatteryVoltage
        {
            get => _batteryVoltage;
            set { _batteryVoltage = value; OnPropertyChanged(nameof(BatteryVoltage)); }
        }

        public double GpsLatitude
        {
            get => _gpsLatitude;
            set { _gpsLatitude = value; OnPropertyChanged(nameof(GpsLatitude)); }
        }

        public double GpsLongitude
        {
            get => _gpsLongitude;
            set { _gpsLongitude = value; OnPropertyChanged(nameof(GpsLongitude)); }
        }

        public double GyroX
        {
            get => _gyroX;
            set { _gyroX = value; OnPropertyChanged(nameof(GyroX)); }
        }

        public double GyroY
        {
            get => _gyroY;
            set { _gyroY = value; OnPropertyChanged(nameof(GyroY)); }
        }

        public double GyroZ
        {
            get => _gyroZ;
            set { _gyroZ = value; OnPropertyChanged(nameof(GyroZ)); }
        }

        public double AccelX
        {
            get => _accelX;
            set { _accelX = value; OnPropertyChanged(nameof(AccelX)); }
        }

        public double AccelY
        {
            get => _accelY;
            set { _accelY = value; OnPropertyChanged(nameof(AccelY)); }
        }

        public double AccelZ
        {
            get => _accelZ;
            set { _accelZ = value; OnPropertyChanged(nameof(AccelZ)); }
        }

        public double BatteryPercentage => Math.Max(0, Math.Min(100, ((BatteryVoltage - 3.0) / (4.2 - 3.0)) * 100));

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}