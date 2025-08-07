using System.Windows;
using SatelliteGroundStation.ViewModels;
using GMap.NET;
using GMap.NET.MapProviders;

namespace SatelliteGroundStation.Views
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();
        }
    }
}