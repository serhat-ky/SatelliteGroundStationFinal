using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace SatelliteGroundStation.Models
{
    public class TimedFilterCommand
    {
        public string OriginalCommand { get; set; } = "";
        public List<FilterStep> Steps { get; set; } = new();
        public string CommandString => $"$TIMED_FILTER,{OriginalCommand}";

        public TimedFilterCommand(string command)
        {
            OriginalCommand = command;
            ParseCommand(command);
        }

        private void ParseCommand(string command)
        {
            var regex = new Regex(@"(\d+)([rgbpnmfyc])", RegexOptions.IgnoreCase);
            var matches = regex.Matches(command);

            foreach (Match match in matches)
            {
                if (match.Groups.Count == 3)
                {
                    int duration = int.Parse(match.Groups[1].Value);
                    string filterCode = match.Groups[2].Value.ToUpper();

                    Steps.Add(new FilterStep(filterCode, duration));
                }
            }
        }

        public bool IsValid()
        {
            return Steps.Count > 0 && OriginalCommand.Length > 0;
        }

        public int TotalDuration()
        {
            return Steps.Sum(step => step.Duration);
        }
    }

    public class FilterStep
    {
        public string FilterCode { get; set; }
        public int Duration { get; set; }
        public int ServoPosition { get; set; }

        public FilterStep(string filterCode, int duration)
        {
            FilterCode = filterCode;
            Duration = duration;
            ServoPosition = GetServoPosition(filterCode);
        }

        private int GetServoPosition(string filterCode)
        {
            return filterCode.ToUpper() switch
            {
                "R" => 45,   // Red
                "G" => 90,   // Green
                "B" => 135,  // Blue
                "N" => 0,    // Normal
                "P" => 270,  // Purple
                "M" => 180,  // Maroon
                "F" => 225,  // Forest
                "Y" => 315,  // Yellow
                "C" => 360,  // Cyan
                _ => 0
            };
        }

        public string GetFilterName()
        {
            return FilterCode.ToUpper() switch
            {
                "R" => "Kırmızı",
                "G" => "Yeşil",
                "B" => "Mavi",
                "N" => "Normal",
                "P" => "Mor",
                "M" => "Koyu Kırmızı",
                "F" => "Koyu Yeşil",
                "Y" => "Sarı",
                "C" => "Camgöbeği",
                _ => "Bilinmeyen"
            };
        }

        public override string ToString()
        {
            return $"{Duration}s {GetFilterName()}";
        }
    }
}