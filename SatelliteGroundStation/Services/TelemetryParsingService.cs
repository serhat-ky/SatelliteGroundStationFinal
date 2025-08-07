using System;
using System.Globalization;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class TelemetryParsingService
    {
        /// <summary>
        /// Parses telemetry data from a comma-separated string
        /// Expected format: $PACKET_NUM,TEMP,PRESSURE,ALTITUDE,SPEED,VOLTAGE,GPS_LAT,GPS_LON,GYRO_X,GYRO_Y,GYRO_Z,ACCEL_X,ACCEL_Y,ACCEL_Z
        /// </summary>
        public TelemetryData? ParseTelemetryData(string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data) || !data.StartsWith("$"))
                {
                    return null;
                }

                // Remove the '$' prefix and split by comma
                var parts = data.Substring(1).Split(',');

                if (parts.Length < 14)
                {
                    Console.WriteLine($"Invalid telemetry data format. Expected 14 parts, got {parts.Length}");
                    return null;
                }

                var telemetryData = new TelemetryData
                {
                    PacketNumber = ParseInt(parts[0]),
                    Timestamp = DateTime.Now,
                    Temperature = ParseDouble(parts[1]),
                    Pressure = ParseDouble(parts[2]),
                    Altitude = ParseDouble(parts[3]),
                    Speed = ParseDouble(parts[4]),
                    BatteryVoltage = ParseDouble(parts[5]),
                    GpsLatitude = ParseDouble(parts[6]),
                    GpsLongitude = ParseDouble(parts[7]),
                    GyroX = ParseDouble(parts[8]),
                    GyroY = ParseDouble(parts[9]),
                    GyroZ = ParseDouble(parts[10]),
                    AccelX = ParseDouble(parts[11]),
                    AccelY = ParseDouble(parts[12]),
                    AccelZ = ParseDouble(parts[13])
                };

                return telemetryData;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error parsing telemetry data: {ex.Message}");
                return null;
            }
        }

        private int ParseInt(string value)
        {
            return int.TryParse(value.Trim(), out int result) ? result : 0;
        }

        private double ParseDouble(string value)
        {
            return double.TryParse(value.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result) ? result : 0.0;
        }
    }
}