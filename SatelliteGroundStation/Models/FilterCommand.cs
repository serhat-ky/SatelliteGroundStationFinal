using System;
using System.Collections.Generic;

namespace SatelliteGroundStation.Models
{
    public class FilterCommand
    {
        public string FilterCode { get; set; } = "";
        public int ServoPosition { get; set; }
        public DateTime Timestamp { get; set; }
        public string CommandString => $"$FILTER,{FilterCode},{ServoPosition}";

        public FilterCommand(string filterCode, int servoPosition)
        {
            FilterCode = filterCode;
            ServoPosition = servoPosition;
            Timestamp = DateTime.Now;
        }

        public static Dictionary<string, int> FilterPositions = new()
        {
            {"N", 0},   // Normal - 0°
            {"R", 45},  // Red - 45°
            {"G", 90},  // Green - 90°
            {"B", 135}, // Blue - 135°
            {"M", 180}, // Maroon - 180°
            {"F", 225}, // Forest - 225°
            {"P", 270}, // Purple - 270°
            {"Y", 315}, // Yellow - 315°
            {"C", 360}  // Cyan - 360° (0°)
        };

        public static FilterCommand CreateCommand(string filterCode)
        {
            if (FilterPositions.TryGetValue(filterCode, out int position))
            {
                return new FilterCommand(filterCode, position);
            }
            throw new ArgumentException($"Geçersiz filtre kodu: {filterCode}");
        }
    }
}