using System;
using System.Globalization;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class TelemetryParsingService
    {
        private int _packetCounter = 0;
        private readonly Random _random = new Random();

        /// <summary>
        /// Arduino Format 2 parsing: $DATA,timestamp,temp,pressure,altitude,speed,voltage,gyroX,gyroY,gyroZ
        /// Örnek: $DATA,12345,25.5,1013.2,1500.0,45.2,3.85,12.5,-8.3,15.7
        /// </summary>
        public TelemetryData? ParseTelemetryData(string data)
        {
            try
            {
                Console.WriteLine($"🔍 Parsing telemetry: '{data}'");

                // Null veya boş kontrol
                if (string.IsNullOrWhiteSpace(data))
                {
                    Console.WriteLine("❌ Telemetry data is null or empty");
                    return null;
                }

                // Trim whitespace
                data = data.Trim();

                // Comments ve boş satırları atla
                if (data.StartsWith("#") || data.StartsWith("//") || string.IsNullOrWhiteSpace(data))
                {
                    Console.WriteLine($"📝 Skipping comment/empty line: {data}");
                    return null;
                }

                // Header kontrol - case insensitive
                if (!data.ToUpper().StartsWith("$DATA,"))
                {
                    Console.WriteLine($"❌ Invalid header. Expected '$DATA,' but got: '{data.Substring(0, Math.Min(10, data.Length))}'");
                    return null;
                }

                // Header'ı çıkar ve split et
                var dataContent = data.Substring(6); // "$DATA," kısmını atla (6 karakter)
                var parts = dataContent.Split(',');

                Console.WriteLine($"📊 Split into {parts.Length} parts: [{string.Join("],[", parts)}]");

                // Minimum veri kontrolü (timestamp + 8 sensor data = 9 total)
                if (parts.Length < 9)
                {
                    Console.WriteLine($"❌ Invalid data format. Expected 9 parts, got {parts.Length}");
                    return null;
                }

                // Parse each field with error handling
                var timestamp = ParseLong(parts[0]);
                var temperature = ParseDouble(parts[1]);
                var pressure = ParseDouble(parts[2]);
                var altitude = ParseDouble(parts[3]);
                var speed = ParseDouble(parts[4]);
                var voltage = ParseDouble(parts[5]);
                var gyroX = ParseDouble(parts[6]);
                var gyroY = ParseDouble(parts[7]);
                var gyroZ = ParseDouble(parts[8]);

                var telemetryData = new TelemetryData
                {
                    PacketNumber = GetNextPacketNumber(),
                    Timestamp = DateTime.Now,

                    // Arduino sensor verileri
                    Temperature = temperature,      // °C
                    Pressure = pressure,           // Pa  
                    Altitude = altitude,           // m
                    Speed = speed,                // m/s
                    BatteryVoltage = voltage,     // V
                    GyroX = gyroX,               // °/s
                    GyroY = gyroY,               // °/s
                    GyroZ = gyroZ,               // °/s

                    // Test GPS koordinatları (Ankara yakını)
                    GpsLatitude = 39.9334 + (_random.NextDouble() - 0.5) * 0.01,
                    GpsLongitude = 32.8597 + (_random.NextDouble() - 0.5) * 0.01,

                    // Akselerometre (Arduino'dan gelmiyorsa 0)
                    AccelX = 0,
                    AccelY = 0,
                    AccelZ = 0
                };

                Console.WriteLine($"✅ Successfully parsed: T={telemetryData.Temperature:F1}°C, P={telemetryData.Pressure:F0}Pa, A={telemetryData.Altitude:F0}m, V={telemetryData.BatteryVoltage:F2}V");
                return telemetryData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error parsing telemetry data: {ex.Message}");
                Console.WriteLine($"Raw data: '{data}'");
                return null;
            }
        }

        private int GetNextPacketNumber()
        {
            return ++_packetCounter;
        }

        private double ParseDouble(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                Console.WriteLine($"⚠️ Empty double value, returning 0.0");
                return 0.0;
            }

            value = value.Trim();

            // Türkçe virgül sorunu için InvariantCulture kullan
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
            {
                return result;
            }

            // Nokta/virgül problemi için alternatif deneme
            value = value.Replace(',', '.');
            if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out result))
            {
                Console.WriteLine($"⚠️ Fixed decimal separator for: '{value}' -> {result}");
                return result;
            }

            Console.WriteLine($"❌ Failed to parse double: '{value}'");
            return 0.0;
        }

        private long ParseLong(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DateTimeOffset.Now.ToUnixTimeMilliseconds();

            if (long.TryParse(value.Trim(), out long result))
                return result;

            Console.WriteLine($"❌ Failed to parse long: '{value}', using current timestamp");
            return DateTimeOffset.Now.ToUnixTimeMilliseconds();
        }

        /// <summary>
        /// Test parsing with sample data
        /// </summary>
        public void TestParsing()
        {
            Console.WriteLine("=== TELEMETRY PARSING TEST ===");

            string[] testDataArray = {
                "$DATA,12345,25.5,1013.2,1500.0,45.2,3.85,12.5,-8.3,15.7",
                "$DATA,12346,24.8,1010.5,1520.0,46.1,3.84,11.2,-9.1,14.3",
                "$DATA,12347,23.2,1008.1,1545.0,47.3,3.83,13.8,-7.5,16.2"
            };

            foreach (var testData in testDataArray)
            {
                Console.WriteLine($"\n🧪 Testing: {testData}");
                var result = ParseTelemetryData(testData);

                if (result != null)
                {
                    Console.WriteLine($"✅ SUCCESS:");
                    Console.WriteLine($"   📦 Packet: {result.PacketNumber}");
                    Console.WriteLine($"   🌡️ Temperature: {result.Temperature:F1}°C");
                    Console.WriteLine($"   📊 Pressure: {result.Pressure:F1}Pa");
                    Console.WriteLine($"   📏 Altitude: {result.Altitude:F1}m");
                    Console.WriteLine($"   ⚡ Speed: {result.Speed:F1}m/s");
                    Console.WriteLine($"   🔋 Voltage: {result.BatteryVoltage:F2}V");
                    Console.WriteLine($"   🎯 Gyro: X={result.GyroX:F1}, Y={result.GyroY:F1}, Z={result.GyroZ:F1}");
                }
                else
                {
                    Console.WriteLine($"❌ FAILED to parse");
                }
            }

            Console.WriteLine("\n=== TEST COMPLETE ===");
        }
    }
}