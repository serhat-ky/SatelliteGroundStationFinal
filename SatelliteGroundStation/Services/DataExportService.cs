using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class DataExportService
    {
        public void ExportToCsv(IEnumerable<TelemetryData> data)
        {
            var saveFileDialog = new SaveFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = $"TelemetryData_{DateTime.Now:yyyyMMdd_HHmmss}.csv"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                ExportToCsv(data, saveFileDialog.FileName);
            }
        }

        public void ExportToCsv(IEnumerable<TelemetryData> data, string filePath)
        {
            var csv = new StringBuilder();

            // Add header
            csv.AppendLine("Packet Number,Timestamp,Temperature (°C),Pressure (Pa),Altitude (m),Speed (m/s),Battery Voltage (V),GPS Latitude,GPS Longitude,Gyro X,Gyro Y,Gyro Z,Accel X,Accel Y,Accel Z,Battery Percentage");

            // Add data rows
            foreach (var item in data.OrderBy(d => d.PacketNumber))
            {
                csv.AppendLine($"{item.PacketNumber}," +
                              $"{item.Timestamp:yyyy-MM-dd HH:mm:ss}," +
                              $"{item.Temperature:F2}," +
                              $"{item.Pressure:F2}," +
                              $"{item.Altitude:F2}," +
                              $"{item.Speed:F2}," +
                              $"{item.BatteryVoltage:F2}," +
                              $"{item.GpsLatitude:F6}," +
                              $"{item.GpsLongitude:F6}," +
                              $"{item.GyroX:F2}," +
                              $"{item.GyroY:F2}," +
                              $"{item.GyroZ:F2}," +
                              $"{item.AccelX:F2}," +
                              $"{item.AccelY:F2}," +
                              $"{item.AccelZ:F2}," +
                              $"{item.BatteryPercentage:F1}");
            }

            File.WriteAllText(filePath, csv.ToString(), Encoding.UTF8);
        }
    }
}