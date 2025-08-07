using System;

namespace SatelliteGroundStation.Models
{
    public class DataPoint
    {
        public DateTime Time { get; set; }
        public double Value { get; set; }

        public DataPoint(DateTime time, double value)
        {
            Time = time;
            Value = value;
        }
    }
}