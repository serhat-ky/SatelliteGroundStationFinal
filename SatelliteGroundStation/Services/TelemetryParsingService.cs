using System;
using System.Globalization;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class TelemetryParsingService
    {
        /// <summary>
        /// Arduino Format 2 parsing: $DATA,timestamp,temp,pressure,altitude,speed,voltage,gyroX,gyroY,gyroZ
        /// Örnek: $DATA,12345,25.5,1013.2,1500,45.2,3.85,12.5,-8.3,15.7
        /// </summary>
        public TelemetryData? ParseTelemetryData(string data)
        {
            try
            {
                // Null veya boş kontrol
                if (string.IsNullOrWhiteSpace(data))
                {
                    Console.WriteLine("Telemetry data is null or empty");
                    return null;
                }

                // Header kontrol
                if (!data.StartsWith("$DATA,"))
                {
                    Console.WriteLine($"Invalid header. Expected '$DATA,' but got: {data.Substring(0, Math.Min(10, data.Length))}");
                    return null;
                }

                // Header'ı çıkar ve split et
                var dataContent = data.Substring(6); // "$DATA," kısmını atla (6 karakter)
                var parts = dataContent.Split(',');

                // Minimum veri kontrolü (timestamp + 8 sensor data = 9 total)
                if (parts.Length < 9)
                {
                    Console.WriteLine($"Invalid data format. Expected 9 parts, got {parts.Length}");
                    return null;
                }

                var telemetryData = new TelemetryData
                {
                    PacketNumber = GetNextPacketNumber(), // Otomatik artan packet number
                    Timestamp = DateTime.Now, // Gerçek zamanlı timestamp

                    // Arduino sensor verileri
                    Temperature = ParseDouble(parts[1]),      // °C
                    Pressure = ParseDouble(parts[2]),         // Pa
                    Altitude = ParseDouble(parts[3]),         // m
                    Speed = ParseDouble(parts[4]),            // m/s
                    BatteryVoltage = ParseDouble(parts[5]),   // V
                    GyroX = ParseDouble(parts[6]),           // °/s
                    GyroY = ParseDouble(parts[7]),           // °/s
                    GyroZ = ParseDouble(parts[8]),           // °/s

                    // Hesaplanan/varsayılan değerler
                    GpsLatitude = 39.9334 + (new Random().NextDouble() - 0.5) * 0.01,  // Test için
                    GpsLongitude = 32.8597 + (new Random().NextDouble() - 0.5) * 0.01, // Test için
                    AccelX = 0, // Arduino'dan gelmiyorsa 0
                    AccelY = 0,
                    AccelZ = 0
                };

                Console.WriteLine($"Parsed telemetry: T={telemetryData.Temperature:F1}°C, P={telemetryData.Pressure:F0}Pa, A={telemetryData.Altitude:F0}m");
                return telemetryData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing telemetry data: {ex.Message}");
                Console.WriteLine($"Raw data: {data}");
                return null;
            }
        }

        private int _packetCounter = 0;

        private int GetNextPacketNumber()
        {
            return ++_packetCounter;
        }

        private double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return 0.0;

            // Türkçe virgül sorunu için InvariantCulture kullan
            if (double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;

            Console.WriteLine($"Failed to parse double: '{value}'");
            return 0.0;
        }

    }
}