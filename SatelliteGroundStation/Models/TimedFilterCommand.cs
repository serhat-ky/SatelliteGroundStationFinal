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
            // Örn: 3g5r2b1n...
            var regex = new Regex(@"(\d+)([rgbpnmyc])", RegexOptions.IgnoreCase);
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

        public bool IsValid() => Steps.Count > 0 && OriginalCommand.Length > 0;
        public int TotalDuration() => Steps.Sum(step => step.Duration);
    }

    public class FilterStep
    {
        // Eski alanlar
        public string FilterCode { get; set; }
        public int Duration { get; set; }

        // Yeni: iki haneli protokol için
        public int Renk1 { get; set; } // 0:şeffaf, 1:K, 2:Y, 3:M
        public int Renk2 { get; set; }

        // (İsteğe bağlı) geriye dönük kullanım için
        public int ServoPosition { get; set; }

        public FilterStep(string filterCode, int duration)
        {
            FilterCode = filterCode;
            Duration = duration;

            // Harf -> (renk1, renk2) map
            // Tek filtre hareketi için ikinciyi 0 bırakıyoruz.
            (Renk1, Renk2) = MapLetterToPair(filterCode);

            // Geriye dönük: tek servo tekeri varsayımı (artık kullanılmıyor ama dursun)
            ServoPosition = GetLegacyServoPosition(filterCode);
        }

        private static (int, int) MapLetterToPair(string code)
        {
            // İzinli renkler: 0 şeffaf, 1 kırmızı, 2 yeşil, 3 mavi
            // Karışımlar: Y=1+2, P=1+3, C=2+3
            switch (code.ToUpper())
            {
                case "N": return (0, 0);      // Normal/Şeffaf
                case "R": return (1, 0);
                case "G": return (2, 0);
                case "B": return (3, 0);
                case "Y": return (1, 2);      // Sarı
                case "P": return (1, 3);      // Mor/Magenta
                case "C": return (2, 3);      // Camgöbeği
                case "M": return (1, 1);      // Koyu kırmızı ≈ kırmızı+yine kırmızı (yaklaştırma)
                case "F": return (2, 2);      // Koyu yeşil ≈ yeşil+yeşil (yaklaştırma)
                default: return (0, 0);
            }
        }

        private static int GetLegacyServoPosition(string filterCode) =>
            filterCode.ToUpper() switch
            {
                "R" => 45,
                "G" => 90,
                "B" => 135,
                "N" => 0,
                "P" => 180,
                "M" => 180,
                "F" => 225,
                "Y" => 270,
                "C" => 315,
                _ => 0
            };

        public string GetFilterName() =>
            FilterCode.ToUpper() switch
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

        public override string ToString() => $"{Duration}s {GetFilterName()} -> ({Renk1}{Renk2})";
    }
}
