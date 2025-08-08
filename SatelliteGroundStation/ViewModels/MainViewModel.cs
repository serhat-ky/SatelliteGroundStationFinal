using System;
using System.Collections.ObjectModel;
using System.IO.Ports;
using System.Linq;
using System.Windows.Input;
using System.Windows;
using SatelliteGroundStation.Models;
using SatelliteGroundStation.Services;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using LiveChartsCore.SkiaSharpView.Painting;
using SkiaSharp;
using System.Windows.Media.Imaging;
using LiveChartsCore.Kernel;
using Microsoft.Web.WebView2.Wpf;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SatelliteGroundStation.ViewModels
{
    public class MainViewModel : ViewModelBase, IDisposable
    {
        private readonly SerialCommunicationService _serialService;
        private readonly TelemetryParsingService _parsingService;
        private readonly DataExportService _exportService;
        private MultispektralFiltreService _filterService;
        private string _commandCode = "";
        private ICommand? _sendCommandCodeCommand;

        // ↓ Video capture service
        private readonly VideoCaptureService _videoCaptureService;

        // ↓ Map service
        private readonly MapService _mapService;

        // Video properties private fields
        private BitmapSource? _currentVideoFrame;
        private bool _isVideoCapturing = false;
        private VideoDeviceInfo? _selectedVideoDevice;
        private VideoResolution _selectedVideoResolution = VideoResolution.Resolution_640x480;
        private bool _isMapInitialized = false;

        // Connection properties
        private bool _isConnected;
        private string _satelliteStatus = "Bekleniyor";
        private string _connectionStatus = "Bağlı Değil";
        private string _selectedComPort = "";
        private int _selectedBaudRate = 9600;

        // Telemetry data
        private TelemetryData? _currentTelemetry;
        private double _currentTemperature;
        private double _currentPressure;
        private double _currentAltitude;
        private double _currentSpeed;
        private double _batteryPercentage;
        private double _gpsLatitude;
        private double _gpsLongitude;

        // Alarm system indicators
        private bool _subsystem1Status = true;
        private bool _subsystem2Status = true;
        private bool _subsystem3Status = false;
        private bool _subsystem4Status = true;
        private bool _subsystem5Status = false;
        private bool _subsystem6Status = false;

        // Collections for data binding
        public ObservableCollection<TelemetryData> TelemetryDataCollection { get; }
        public ObservableCollection<DataPoint> TemperatureData { get; }
        public ObservableCollection<DataPoint> PressureData { get; }
        public ObservableCollection<DataPoint> SpeedData { get; }
        public ObservableCollection<DataPoint> BatteryVoltageData { get; }
        public ObservableCollection<DataPoint> GyroXData { get; }
        public ObservableCollection<DataPoint> GyroYData { get; }
        public ObservableCollection<DataPoint> GyroZData { get; }
        public ObservableCollection<VideoDeviceInfo> AvailableVideoDevices { get; }

        // Chart series for LiveCharts
        public ISeries[] TemperatureSeries { get; }
        public ISeries[] PressureSeries { get; }
        public ISeries[] SpeedSeries { get; }
        public ISeries[] BatteryVoltageSeries { get; }
        public ISeries[] AxisDataSeries { get; }

        // Available COM ports
        public ObservableCollection<string> AvailableComPorts { get; }
        public int[] AvailableBaudRates { get; } = { 9600, 19200, 38400, 57600, 115200 };

        // Commands
        public ICommand ConnectCommand { get; }
        public ICommand DisconnectCommand { get; }
        public ICommand ManualReleaseCommand { get; }
        public ICommand ExportDataCommand { get; }
        public ICommand StartTelemetryCommand { get; }

        // Video Commands
        public ICommand StartVideoCaptureCommand { get; }
        public ICommand StopVideoCaptureCommand { get; }
        public ICommand RefreshVideoDevicesCommand { get; }
        public ICommand SaveVideoFrameCommand { get; }
        public ICommand InitializeMapCommand { get; }
        public ICommand CenterMapCommand { get; }
        public ICommand ClearTrackCommand { get; }
        public ICommand ChangeMapTypeCommand { get; }

        // Multispectral Filter Commands
        private ICommand? _redFilterCommand;
        private ICommand? _greenFilterCommand;
        private ICommand? _blueFilterCommand;
        private ICommand? _purpleFilterCommand;

        public ICommand RedFilterCommand
        {
            get
            {
                return _redFilterCommand ??= new RelayCommand(async () =>
                {
                    await _filterService.SetRedFilterAsync();
                });
            }
        }

        public ICommand GreenFilterCommand
        {
            get
            {
                return _greenFilterCommand ??= new RelayCommand(async () =>
                {
                    await _filterService.SetGreenFilterAsync();
                });
            }
        }

        public ICommand BlueFilterCommand
        {
            get
            {
                return _blueFilterCommand ??= new RelayCommand(async () =>
                {
                    await _filterService.SetBlueFilterAsync();
                });
            }
        }

        public ICommand PurpleFilterCommand
        {
            get
            {
                return _purpleFilterCommand ??= new RelayCommand(async () =>
                {
                    await _filterService.SetPurpleFilterAsync();
                });
            }
        }

        public ICommand SendCommandCodeCommand
        {
            get
            {
                return _sendCommandCodeCommand ??= new RelayCommand(
                    SendCommandCode,
                    () =>
                    {
                        bool canExecute = IsConnected && !string.IsNullOrEmpty(CommandCode);
                        Console.WriteLine($"🎯 SendCommand CanExecute: {canExecute}");
                        return canExecute;
                    }
                );
            }
        }

        public MainViewModel()
        {
            _serialService = new SerialCommunicationService();
            _parsingService = new TelemetryParsingService();
            _exportService = new DataExportService();
            _videoCaptureService = new VideoCaptureService();
            _mapService = new MapService();
            _mapService.MapError += OnMapError;

            _filterService = new MultispektralFiltreService(_serialService);
            _filterService.FilterChanged += OnFilterStateChanged;      //
            _filterService.CommandSent += OnFilterCommandSentEvent;    // 
            _filterService.ErrorOccurred += OnFilterErrorEvent;        //         

            // Initialize collections
            TelemetryDataCollection = new ObservableCollection<TelemetryData>();
            TemperatureData = new ObservableCollection<DataPoint>();
            PressureData = new ObservableCollection<DataPoint>();
            SpeedData = new ObservableCollection<DataPoint>();
            BatteryVoltageData = new ObservableCollection<DataPoint>();
            GyroXData = new ObservableCollection<DataPoint>();
            GyroYData = new ObservableCollection<DataPoint>();
            GyroZData = new ObservableCollection<DataPoint>();
            AvailableComPorts = new ObservableCollection<string>();
            AvailableVideoDevices = new ObservableCollection<VideoDeviceInfo>();

            // Initialize chart series
            TemperatureSeries = new ISeries[]
            {
                new LineSeries<DataPoint>
                {
                   Values = TemperatureData,
                   Name = "Sıcaklık (°C)",
                   Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                   Fill = null,
                   GeometrySize = 0,
                   Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                }
            };

            PressureSeries = new ISeries[]
            {
                new LineSeries<DataPoint>
                {
                    Values = PressureData,
                    Name = "Basınç (Pa)",
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0,
                    Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                }
            };

            SpeedSeries = new ISeries[]
            {
                new LineSeries<DataPoint>
                {
                    Values = SpeedData,
                    Name = "Hız (m/s)",
                    Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0,
                    Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                }
            };

            BatteryVoltageSeries = new ISeries[]
            {
                new LineSeries<DataPoint>
                {
                    Values = BatteryVoltageData,
                    Name = "Pil Gerilimi (V)",
                    Stroke = new SolidColorPaint(SKColors.Orange) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0,
                    Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                }
            };

            AxisDataSeries = new ISeries[]
            {
                new LineSeries<DataPoint>
                {
                    Values = GyroXData,
                    Name = "Gyro X",
                    Stroke = new SolidColorPaint(SKColors.Red) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0,
                    Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                },
                new LineSeries<DataPoint>
                {
                     Values = GyroYData,
                     Name = "Gyro Y",
                     Stroke = new SolidColorPaint(SKColors.Green) { StrokeThickness = 2 },
                     Fill = null,
                     GeometrySize = 0,
                     Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                },
                new LineSeries<DataPoint>
                {
                    Values = GyroZData,
                    Name = "Gyro Z",
                    Stroke = new SolidColorPaint(SKColors.Blue) { StrokeThickness = 2 },
                    Fill = null,
                    GeometrySize = 0,
                    Mapping = (point, index) => new Coordinate(point.Time.Ticks, point.Value)
                }
            };

            // Initialize commands
            ConnectCommand = new RelayCommand(Connect, CanConnect);
            DisconnectCommand = new RelayCommand(Disconnect, () => IsConnected);
            ManualReleaseCommand = new RelayCommand(SendManualRelease, () => IsConnected);
            ExportDataCommand = new RelayCommand(ExportData);
            StartTelemetryCommand = new RelayCommand(StartTelemetry, () => IsConnected);
            StartVideoCaptureCommand = new RelayCommand(StartVideoCapture, CanStartVideoCapture);
            StopVideoCaptureCommand = new RelayCommand(StopVideoCapture, () => IsVideoCapturing);
            RefreshVideoDevicesCommand = new RelayCommand(RefreshVideoDevices);
            SaveVideoFrameCommand = new RelayCommand(SaveVideoFrame, () => CurrentVideoFrame != null);
            CenterMapCommand = new RelayCommand(async () => await CenterMapOnSatelliteAsync());
            ClearTrackCommand = new RelayCommand(ClearGpsTrack);

            // Subscribe to serial service events
            _serialService.DataReceived += OnDataReceived;
            _serialService.ConnectionChanged += OnConnectionChanged;

            // Video events
            _videoCaptureService.FrameReceived += OnVideoFrameReceived;
            _videoCaptureService.ErrorOccurred += OnVideoErrorOccurred;

            // Load available COM ports
            RefreshComPorts();

            // Load video devices
            RefreshVideoDevices();

            // Generate sample data for demonstration
            GenerateSampleData();

            TestFormat2Parsing();
        }

        #region Video Methods

        private void StartVideoCapture()
        {
            if (SelectedVideoDevice != null)
            {
                var success = _videoCaptureService.StartCapture(SelectedVideoDevice.Index, SelectedVideoResolution);
                if (success)
                {
                    IsVideoCapturing = true;
                }
            }
        }

        private bool CanStartVideoCapture()
        {
            return !IsVideoCapturing && SelectedVideoDevice != null;
        }

        private void StopVideoCapture()
        {
            _videoCaptureService.StopCapture();
            IsVideoCapturing = false;
            CurrentVideoFrame = null;
        }

        private void RefreshVideoDevices()
        {
            AvailableVideoDevices.Clear();
            var devices = _videoCaptureService.GetAvailableDevices();
            foreach (var device in devices)
            {
                AvailableVideoDevices.Add(device);
            }

            if (AvailableVideoDevices.Any())
            {
                SelectedVideoDevice = AvailableVideoDevices.First();
            }
        }

        private void SaveVideoFrame()
        {
            if (CurrentVideoFrame != null)
            {
                var saveDialog = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG files (*.png)|*.png|JPEG files (*.jpg)|*.jpg",
                    DefaultExt = "png"
                };

                if (saveDialog.ShowDialog() == true)
                {
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(CurrentVideoFrame));

                    using var fileStream = new System.IO.FileStream(saveDialog.FileName, System.IO.FileMode.Create);
                    encoder.Save(fileStream);

                    MessageBox.Show("Frame başarıyla kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
                }
            }
        }

        #region Map Methods

        public async Task InitializeMapAsync(WebView2 webView)
        {
            try
            {
                await _mapService.InitializeAsync(webView);
                _isMapInitialized = true;
            }
            catch (Exception ex)
            {
                OnMapError(this, $"Map initialization error: {ex.Message}");
            }
        }

        public async Task CenterMapOnSatelliteAsync()
        {
            if (_isMapInitialized && GpsLatitude != 0 && GpsLongitude != 0)
            {
                await _mapService.SetMapCenterAsync(GpsLatitude, GpsLongitude, 15);
            }
        }

        public void ClearGpsTrack()
        {
            _mapService.ClearTrack();
        }

        private void OnMapError(object? sender, string error)
        {
            MessageBox.Show($"Harita hatası: {error}", "Harita Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        #endregion

        // Video Event handlers
        private void OnVideoFrameReceived(object? sender, BitmapSource frame)
        {
            CurrentVideoFrame = frame;
        }

        private void OnVideoErrorOccurred(object? sender, string error)
        {
            MessageBox.Show($"Video hatası: {error}", "Video Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsVideoCapturing = false;
        }

        #endregion

        #region Disposal

        public void Dispose()
        {
            _videoCaptureService?.Dispose();
            _serialService?.Dispose();
            _mapService?.ClearTrack();
            _filterService?.Dispose();
        }

        #endregion

        #region Properties

        public bool IsConnected
        {
            get => _isConnected;
            set
            {
                SetProperty(ref _isConnected, value);
                ConnectionStatus = value ? "Bağlı" : "Bağlı Değil";
            }
        }

        public string SatelliteStatus
        {
            get => _satelliteStatus;
            set => SetProperty(ref _satelliteStatus, value);
        }

        public string ConnectionStatus
        {
            get => _connectionStatus;
            set => SetProperty(ref _connectionStatus, value);
        }

        public string SelectedComPort
        {
            get => _selectedComPort;
            set => SetProperty(ref _selectedComPort, value);
        }

        public int SelectedBaudRate
        {
            get => _selectedBaudRate;
            set => SetProperty(ref _selectedBaudRate, value);
        }

        public string CommandCode
        {
            get => _commandCode;
            set
            {
                SetProperty(ref _commandCode, value);
                CommandManager.InvalidateRequerySuggested(); // Command'ın CanExecute'unu güncelle
                Console.WriteLine($"🎯 CommandCode changed to: '{value}' (Length: {value?.Length})");
            }
        }

        public double CurrentTemperature
        {
            get => _currentTemperature;
            set => SetProperty(ref _currentTemperature, value);
        }

        public double CurrentPressure
        {
            get => _currentPressure;
            set => SetProperty(ref _currentPressure, value);
        }

        public double CurrentAltitude
        {
            get => _currentAltitude;
            set => SetProperty(ref _currentAltitude, value);
        }

        public double CurrentSpeed
        {
            get => _currentSpeed;
            set => SetProperty(ref _currentSpeed, value);
        }

        public double BatteryPercentage
        {
            get => _batteryPercentage;
            set => SetProperty(ref _batteryPercentage, value);
        }

        public double GpsLatitude
        {
            get => _gpsLatitude;
            set => SetProperty(ref _gpsLatitude, value);
        }

        public double GpsLongitude
        {
            get => _gpsLongitude;
            set => SetProperty(ref _gpsLongitude, value);
        }

        public LiveChartsCore.SkiaSharpView.Axis[] XAxes { get; } = new[]
        {
           new LiveChartsCore.SkiaSharpView.Axis
    {
        Name = "Zaman",
        LabelsRotation = 0,
        TextSize = 10,
        LabelsPaint = new SolidColorPaint(SKColors.LightGray),
        UnitWidth = TimeSpan.FromMinutes(1).Ticks,
        MinStep = TimeSpan.FromMinutes(5).Ticks,
        Labeler = value => new DateTime((long)value).ToString("HH:mm")
    }
};

        public LiveChartsCore.SkiaSharpView.Axis[] YAxes { get; } = new[]
        {
           new LiveChartsCore.SkiaSharpView.Axis
    {
        TextSize = 10,
        LabelsPaint = new SolidColorPaint(SKColors.LightGray)
    }
};

        // Alarm system status properties
        public bool Subsystem1Status
        {
            get => _subsystem1Status;
            set => SetProperty(ref _subsystem1Status, value);
        }

        public bool Subsystem2Status
        {
            get => _subsystem2Status;
            set => SetProperty(ref _subsystem2Status, value);
        }

        public bool Subsystem3Status
        {
            get => _subsystem3Status;
            set => SetProperty(ref _subsystem3Status, value);
        }

        public bool Subsystem4Status
        {
            get => _subsystem4Status;
            set => SetProperty(ref _subsystem4Status, value);
        }

        public bool Subsystem5Status
        {
            get => _subsystem5Status;
            set => SetProperty(ref _subsystem5Status, value);
        }

        public bool Subsystem6Status
        {
            get => _subsystem6Status;
            set => SetProperty(ref _subsystem6Status, value);
        }

        // Video Properties
        public BitmapSource? CurrentVideoFrame
        {
            get => _currentVideoFrame;
            set => SetProperty(ref _currentVideoFrame, value);
        }

        public bool IsVideoCapturing
        {
            get => _isVideoCapturing;
            set => SetProperty(ref _isVideoCapturing, value);
        }

        public VideoDeviceInfo? SelectedVideoDevice
        {
            get => _selectedVideoDevice;
            set => SetProperty(ref _selectedVideoDevice, value);
        }

        public VideoResolution SelectedVideoResolution
        {
            get => _selectedVideoResolution;
            set => SetProperty(ref _selectedVideoResolution, value);
        }

        public VideoResolution[] AvailableVideoResolutions { get; } = Enum.GetValues<VideoResolution>();

        #endregion

        #region Command Methods

        private void Connect()
        {
            try
            {
                Console.WriteLine($"🔌 Attempting to connect to {SelectedComPort} at {SelectedBaudRate} baud...");
                _serialService.Connect(SelectedComPort, SelectedBaudRate);
                Console.WriteLine("✅ Connect method completed successfully");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Connect failed: {ex.Message}");
                MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanConnect()
        {
            return !IsConnected && !string.IsNullOrEmpty(SelectedComPort);
        }

        private void Disconnect()
        {
            _serialService.Disconnect();
        }

        private void SendManualRelease()
        {
            _serialService.SendCommand("RELEASE");
        }

        private void ExportData()
        {
            try
            {
                _exportService.ExportToCsv(TelemetryDataCollection);
                MessageBox.Show("Veriler başarıyla dışa aktarıldı.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Dışa aktarma hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StartTelemetry()
        {
            _serialService.SendCommand("START_TELEMETRY");
            SatelliteStatus = "Telemetri Aktif";
        }

        private void SendCommandCode()
        {
            Console.WriteLine($"📤 SendCommandCode executed - Code: '{CommandCode}'");
            Console.WriteLine($"📤 IsConnected: {IsConnected}");

            if (!string.IsNullOrEmpty(CommandCode))
            {
                Console.WriteLine($"📤 Sending command: {CommandCode}");
                _serialService.SendCommand(CommandCode);
                Console.WriteLine($"✅ Command '{CommandCode}' sent to Arduino");

                CommandCode = "";
                Console.WriteLine("🧹 CommandCode cleared");
            }
            else
            {
                Console.WriteLine("❌ CommandCode is empty or null");
                MessageBox.Show("Lütfen bir komut kodu girin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendFilterCommand(string filter)
        {
            _serialService.SendCommand($"FILTER_{filter}");
        }

        public FilterData FilterData => _filterService.FilterData;

        #endregion

        #region Event Handlers

        private void OnDataReceived(object? sender, string data)
        {
            Console.WriteLine($"📡 Raw data received: '{data}'");

            Application.Current.Dispatcher.Invoke(() =>
            {
                var telemetryData = _parsingService.ParseTelemetryData(data);
                if (telemetryData != null)
                {
                    Console.WriteLine($"✅ Data parsed: T={telemetryData.Temperature}°C");
                    ProcessTelemetryData(telemetryData);
                }
                else
                {
                    Console.WriteLine($"❌ Failed to parse data: '{data}'");
                }
            });
        }

        private void OnConnectionChanged(object? sender, bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = isConnected;
                if (!isConnected)
                {
                    SatelliteStatus = "Bağlantı Kesildi";
                }
            });
        }

        #endregion

        #region Helper Methods

        private void ProcessTelemetryData(TelemetryData telemetryData)
        {
            Console.WriteLine($"🔥 ProcessTelemetryData called: T={telemetryData.Temperature:F1}°C, P={telemetryData.Pressure:F0}Pa");

            _currentTelemetry = telemetryData;

            // Update current values
            CurrentTemperature = telemetryData.Temperature;
            CurrentPressure = telemetryData.Pressure;
            CurrentAltitude = telemetryData.Altitude;
            CurrentSpeed = telemetryData.Speed;
            BatteryPercentage = telemetryData.BatteryPercentage;
            GpsLatitude = telemetryData.GpsLatitude;
            GpsLongitude = telemetryData.GpsLongitude;

            // Add to collections
            TelemetryDataCollection.Insert(0, telemetryData);

            // Add to chart data (keep last 100 points)
            var now = DateTime.Now;
            TemperatureData.Add(new DataPoint(now, telemetryData.Temperature));
            PressureData.Add(new DataPoint(now, telemetryData.Pressure));
            SpeedData.Add(new DataPoint(now, telemetryData.Speed));
            BatteryVoltageData.Add(new DataPoint(now, telemetryData.BatteryVoltage));
            GyroXData.Add(new DataPoint(now, telemetryData.GyroX));
            GyroYData.Add(new DataPoint(now, telemetryData.GyroY));
            GyroZData.Add(new DataPoint(now, telemetryData.GyroZ));

            Console.WriteLine($"📊 Chart points added: Temp={TemperatureData.Count}, Pressure={PressureData.Count}");

            // Keep only last 100 points for performance
            if (TemperatureData.Count > 100) TemperatureData.RemoveAt(0);
            if (PressureData.Count > 100) PressureData.RemoveAt(0);
            if (SpeedData.Count > 100) SpeedData.RemoveAt(0);
            if (BatteryVoltageData.Count > 100) BatteryVoltageData.RemoveAt(0);
            if (GyroXData.Count > 100) GyroXData.RemoveAt(0);
            if (GyroYData.Count > 100) GyroYData.RemoveAt(0);
            if (GyroZData.Count > 100) GyroZData.RemoveAt(0);

            // Limit telemetry data collection to 1000 entries
            if (TelemetryDataCollection.Count > 1000)
            {
                TelemetryDataCollection.RemoveAt(TelemetryDataCollection.Count - 1);
            }

            if (_isMapInitialized && telemetryData.GpsLatitude != 0 && telemetryData.GpsLongitude != 0)
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _mapService.UpdateGpsLocationAsync(
                            telemetryData.GpsLatitude,
                            telemetryData.GpsLongitude,
                            telemetryData.Altitude);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"GPS update error: {ex.Message}");
                    }
                });
            }

            SatelliteStatus = "Veri Alınıyor";
        }

        private void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            var ports = _serialService.GetAvailablePorts();
            foreach (var port in ports)
            {
                AvailableComPorts.Add(port);
            }

            if (AvailableComPorts.Any())
            {
                SelectedComPort = AvailableComPorts.First();
            }
        }

        private void GenerateSampleData()
        {
            // Generate some sample data for demonstration
            var random = new Random();
            for (int i = 0; i < 50; i++)
            {
                var sampleData = new TelemetryData
                {
                    PacketNumber = i + 1,
                    Timestamp = DateTime.Now.AddSeconds(-i * 5),
                    Temperature = 20 + random.NextDouble() * 10,
                    Pressure = 1013 + random.NextDouble() * 100,
                    Altitude = 1000 + random.NextDouble() * 500,
                    Speed = random.NextDouble() * 50,
                    BatteryVoltage = 3.7 + random.NextDouble() * 0.5,
                    GpsLatitude = 39.9334 + (random.NextDouble() - 0.5) * 0.01,
                    GpsLongitude = 32.8597 + (random.NextDouble() - 0.5) * 0.01,
                    GyroX = (random.NextDouble() - 0.5) * 100,
                    GyroY = (random.NextDouble() - 0.5) * 100,
                    GyroZ = (random.NextDouble() - 0.5) * 100
                };

                ProcessTelemetryData(sampleData);
            }
        }

        private void TestFormat2Parsing()
        {
            Console.WriteLine("=== FORMAT 2 PARSING TEST ===");

            // Örnek veri ile grafikleri test et
            string testData = "$DATA,12345,25.5,1013.2,1500.0,45.2,3.85,12.5,-8.3,15.7";
            var result = _parsingService.ParseTelemetryData(testData);

            if (result != null)
            {
                ProcessTelemetryData(result);
                Console.WriteLine("✅ Test data added to charts!");
            }
            else
            {
                Console.WriteLine("❌ Test parsing failed!");
            }
        }
        #endregion

        #region Filter Event Handlers

        private void OnFilterStateChanged(object? sender, string filterCode)
        {
            Console.WriteLine($"🎯 UI: Filter changed to {filterCode}");
        }

        private void OnFilterCommandSentEvent(object? sender, string command)
        {
            Console.WriteLine($"📤 UI: Filter command sent: {command}");
        }

        private void OnFilterErrorEvent(object? sender, string error)
        {
            Console.WriteLine($"❌ UI: Filter error: {error}");
        }

        #endregion
    }
}