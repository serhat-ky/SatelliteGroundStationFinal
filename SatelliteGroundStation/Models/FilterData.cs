using System;
using System.ComponentModel;

namespace SatelliteGroundStation.Models
{
    public class FilterData : INotifyPropertyChanged
    {
        private string _currentFilter = "N"; // Normal (başlangıç)
        private int _servoPosition = 0;
        private bool _isChanging = false;
        private DateTime _lastChangeTime = DateTime.Now;
        private bool _autoMode = false;
        private int _autoInterval = 5; // saniye

        public string CurrentFilter
        {
            get => _currentFilter;
            set { _currentFilter = value; OnPropertyChanged(nameof(CurrentFilter)); }
        }

        public int ServoPosition
        {
            get => _servoPosition;
            set { _servoPosition = value; OnPropertyChanged(nameof(ServoPosition)); }
        }

        public bool IsChanging
        {
            get => _isChanging;
            set { _isChanging = value; OnPropertyChanged(nameof(IsChanging)); }
        }

        public DateTime LastChangeTime
        {
            get => _lastChangeTime;
            set { _lastChangeTime = value; OnPropertyChanged(nameof(LastChangeTime)); }
        }

        public bool AutoMode
        {
            get => _autoMode;
            set { _autoMode = value; OnPropertyChanged(nameof(AutoMode)); }
        }

        public int AutoInterval
        {
            get => _autoInterval;
            set { _autoInterval = value; OnPropertyChanged(nameof(AutoInterval)); }
        }

        // Hesaplanan özellikler
        public string FilterDescription => GetFilterDescription(CurrentFilter);
        public string StatusText => IsChanging ? "Değiştiriliyor..." : $"Aktif: {FilterDescription}";

        private string GetFilterDescription(string filterCode)
        {
            return filterCode switch
            {
                "R" => "Light Red",
                "G" => "Light Green",
                "B" => "Light Blue",
                "N" => "Normal",
                "M" => "Maroon Red",
                "F" => "Forest/Dark Green",
                "P" => "Purple, Pink",
                "Y" => "Yellow, Brown",
                "C" => "Cyan, Turquoise",
                _ => "Bilinmeyen"
            };
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}