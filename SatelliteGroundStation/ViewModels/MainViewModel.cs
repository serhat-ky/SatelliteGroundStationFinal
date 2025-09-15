// PART 1 OF MainViewModel.cs

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

        // Video capture service
        private readonly VideoCaptureService _videoCaptureService;

        // Map service
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

        // Multispektral Filtre Properties
        private string _firstSpectralColor = "Transparent";
        private string _secondSpectralColor = "Transparent";
        private string _mixedSpectralColor = "Transparent";
        private string _firstColorValue = "0";
        private string _secondColorValue = "0";
        private string _spectralInputCode = "";
        private string _receivedSpectralData = "";
        private ICommand? _sendSpectralCodeCommand;

        // Telemetry data
        private TelemetryData? _currentTelemetry;
        private double _currentTemperature;
        private double _currentPressure;
        private double _currentAltitude;
        private double _currentSpeed;
        private double _batteryPercentage;
        private double _gpsLatitude;
        private double _gpsLongitude;

        // Telemetry simulation
        private readonly TelemetrySimulatorService _simService;
        public ICommand StartSimulationCommand { get; }
        public ICommand StopSimulationCommand { get; }
        private bool _isSimulationRunning;
        public bool IsSimulationRunning
        {
            get => _isSimulationRunning;
            set
            {
                if (SetProperty(ref _isSimulationRunning, value))
                {
                    // Sim başlat/durdur butonlarının CanExecute durumlarını yenile
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

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

        // ---- X Axis (zaman) ----
        public Axis[] XAxes { get; } =
        {
            new Axis
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

        // ---- SABİT/DİNAMİK EKSEN: her grafik için ayrı Y axes ----
        public Axis[] TemperatureYAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
        public Axis[] PressureYAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
        public Axis[] SpeedYAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
        public Axis[] BatteryYAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };
        public Axis[] AxisYAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };

        // (Eski YAxes duruyor; XAML'i yeni per-graph YAxes’lere bağlayacaksın)
        public Axis[] YAxes { get; } = { new Axis { TextSize = 10, LabelsPaint = new SolidColorPaint(SKColors.LightGray) } };

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
                    // Mor = Kırmızı (1) + Mavi (3) — yeni iki haneli protokol için örnek
                    await _filterService.ChangeFilterAsync(1, 3);
                });
            }
        }

        public ICommand SendCommandCodeCommand
        {
            get
            {
                return _sendCommandCodeCommand ??= new RelayCommand(
                    SendCommandCode,
                    () => IsConnected && !string.IsNullOrEmpty(CommandCode)
                );
            }
        }

        // Multispektral Filtre için SendSpectralCodeCommand
        public ICommand SendSpectralCodeCommand
        {
            get
            {
                return _sendSpectralCodeCommand ??= new RelayCommand(
                    SendSpectralCode,
                    () => IsConnected && !string.IsNullOrEmpty(SpectralInputCode) && SpectralInputCode.Length == 2
                );
            }
        }

        private TimedFilterService _timedFilterService;

        // ---- SABİT EKSEN KONFİG ----
        private (double min, double max) _axisTemp = (20.0, 30.0);   // °C
        private (double min, double max) _axisPres = (980.0, 1050.0);// hPa
        private (double min, double max) _axisSpeed = (0.0, 15.0);   // m/s
        private (double min, double max) _axisBatt = (3.4, 4.2);     // V
        private (double min, double max) _axisGyro = (-20.0, 20.0);  // °/s

        private bool _useFixedAxes = true; // varsayılan: sabit eksenler açık
        public bool UseFixedAxes
        {
            get => _useFixedAxes;
            set
            {
                if (SetProperty(ref _useFixedAxes, value))
                    ApplyAxisMode();
            }
        }

        // ---- DİNAMİK EKSEN için RollingRange stabilizer’ları ----
        private readonly RollingRange _tempRange = new(180, 0.08);
        private readonly RollingRange _presRange = new(180, 0.03);
        private readonly RollingRange _speedRange = new(180, 0.10);
        private readonly RollingRange _battRange = new(180, 0.02);
        private readonly RollingRange _gyroRange = new(180, 0.15);

        public MainViewModel()
        {
            _serialService = new SerialCommunicationService();
            _parsingService = new TelemetryParsingService();
            _exportService = new DataExportService();
            _videoCaptureService = new VideoCaptureService();
            _mapService = new MapService();
            _mapService.MapError += OnMapError;

            _filterService = new MultispektralFiltreService(_serialService);
            _timedFilterService = new TimedFilterService(_serialService, _filterService);

            _filterService.FilterChanged += OnFilterStateChanged;
            _filterService.CommandSent += OnFilterCommandSentEvent;
            _filterService.ErrorOccurred += OnFilterErrorEvent;

            // --- Telemetri Simülatörü ---
            _simService = new TelemetrySimulatorService();
            _simService.LineGenerated += OnSimLineGenerated;

            // Initialize collections
            TelemetryDataCollection = new ObservableCollection<TelemetryData>();
            TemperatureData = new ObservableCollection<DataPoint>();
            PressureData = new ObservableCollection<DataPoint>();
            SpeedData = new ObservableCollection<DataPoint>();
            BatteryVoltageData = new ObservableCollection<DataPoint>();
            GyroXData = new ObservableCollection<DataPoint>();
            GyroYData = new ObservableCollection<DataPoint>();
            GyroZData = new ObservableCollection<DataPoint>();
            AvailableVideoDevices = new ObservableCollection<VideoDeviceInfo>();
            AvailableComPorts = new ObservableCollection<string>();

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
                    Name = "Basınç (hPa)",
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

            // *** YENİ: Simülasyon komutları ***
            StartSimulationCommand = new RelayCommand(
                StartSimulation,
                () => !IsSimulationRunning);

            StopSimulationCommand = new RelayCommand(
                StopSimulation,
                () => IsSimulationRunning);

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

            // örnek veriler (istersen kapat)
            GenerateSampleData();
            TestFormat2Parsing();

            // SABİT/DİNAMİK eksen modunu uygula
            ApplyAxisMode();
        }

        #region Video Methods

        private void StartVideoCapture()
        {
            if (SelectedVideoDevice != null)
            {
                var success = _videoCaptureService.StartCapture(SelectedVideoDevice.Index, SelectedVideoResolution);
                if (success) IsVideoCapturing = true;
            }
        }

        private bool CanStartVideoCapture() => !IsVideoCapturing && SelectedVideoDevice != null;

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
            foreach (var device in devices) AvailableVideoDevices.Add(device);
            if (AvailableVideoDevices.Any()) SelectedVideoDevice = AvailableVideoDevices.First();
        }

        private void SaveVideoFrame()
        {
            if (CurrentVideoFrame == null) return;

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

        public void ClearGpsTrack() => _mapService.ClearTrack();

        private void OnMapError(object? sender, string error)
        {
            MessageBox.Show($"Harita hatası: {error}", "Harita Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        #endregion

        // Video events
        private void OnVideoFrameReceived(object? sender, BitmapSource frame) => CurrentVideoFrame = frame;

        private void OnVideoErrorOccurred(object? sender, string error)
        {
            MessageBox.Show($"Video hatası: {error}", "Video Hatası", MessageBoxButton.OK, MessageBoxImage.Warning);
            IsVideoCapturing = false;
        }

        #endregion

        // *** YENİ: Simülasyon kontrol ***
        private void StartSimulation()
        {
            _simService.Start();                 // TelemetrySimulatorService içinde Start() olduğunu varsayıyoruz
            IsSimulationRunning = true;
            SatelliteStatus = "Simülasyon Aktif";
        }

        private void StopSimulation()
        {
            _simService.Stop();
            IsSimulationRunning = false;
            SatelliteStatus = "Bekleniyor";
        }

        // *** YENİ: Sim satırı geldiğinde parse edip aynı veri hattına akıt ***
        private void OnSimLineGenerated(object? sender, string line)
        {
            var t = _parsingService.ParseTelemetryData(line);
            if (t != null)
                Application.Current.Dispatcher.Invoke(() => ProcessTelemetryData(t));
        }


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
                CommandManager.InvalidateRequerySuggested();
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
                CommandManager.InvalidateRequerySuggested();
            }
        }

        // Multispektral Filtre Properties (UI için)
        public string FirstSpectralColor
        {
            get => _firstSpectralColor;
            set => SetProperty(ref _firstSpectralColor, value);
        }

        public string SecondSpectralColor
        {
            get => _secondSpectralColor;
            set => SetProperty(ref _secondSpectralColor, value);
        }

        public string MixedSpectralColor
        {
            get => _mixedSpectralColor;
            set => SetProperty(ref _mixedSpectralColor, value);
        }

        public string FirstColorValue
        {
            get => _firstColorValue;
            set => SetProperty(ref _firstColorValue, value);
        }

        // PART 2 OF MainViewModel.cs (fixed)

        public string SecondColorValue
        {
            get => _secondColorValue;
            set => SetProperty(ref _secondColorValue, value);
        }

        public string SpectralInputCode
        {
            get => _spectralInputCode;
            set
            {
                if (SetProperty(ref _spectralInputCode, value))
                {
                    UpdateSpectralColors(value);
                    CommandManager.InvalidateRequerySuggested();
                }
            }
        }

        public string ReceivedSpectralData
        {
            get => _receivedSpectralData;
            set => SetProperty(ref _receivedSpectralData, value);
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

        // Alarm system status properties
        public bool Subsystem1Status { get => _subsystem1Status; set => SetProperty(ref _subsystem1Status, value); }
        public bool Subsystem2Status { get => _subsystem2Status; set => SetProperty(ref _subsystem2Status, value); }
        public bool Subsystem3Status { get => _subsystem3Status; set => SetProperty(ref _subsystem3Status, value); }
        public bool Subsystem4Status { get => _subsystem4Status; set => SetProperty(ref _subsystem4Status, value); }
        public bool Subsystem5Status { get => _subsystem5Status; set => SetProperty(ref _subsystem5Status, value); }
        public bool Subsystem6Status { get => _subsystem6Status; set => SetProperty(ref _subsystem6Status, value); }

        // Video Properties
        public BitmapSource? CurrentVideoFrame { get => _currentVideoFrame; set => SetProperty(ref _currentVideoFrame, value); }
        public bool IsVideoCapturing { get => _isVideoCapturing; set => SetProperty(ref _isVideoCapturing, value); }
        public VideoDeviceInfo? SelectedVideoDevice { get => _selectedVideoDevice; set => SetProperty(ref _selectedVideoDevice, value); }
        public VideoResolution SelectedVideoResolution { get => _selectedVideoResolution; set => SetProperty(ref _selectedVideoResolution, value); }
        public VideoResolution[] AvailableVideoResolutions { get; } = Enum.GetValues<VideoResolution>();

        public FilterData FilterData => _filterService.FilterData;

        #endregion

        #region Command Methods

        private void Connect()
        {
            try
            {
                _serialService.Connect(SelectedComPort, SelectedBaudRate);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Bağlantı hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool CanConnect() => !IsConnected && !string.IsNullOrEmpty(SelectedComPort);

        private void Disconnect() => _serialService.Disconnect();

        private void SendManualRelease() => _serialService.SendCommand("RELEASE");

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
            if (!string.IsNullOrEmpty(CommandCode))
            {
                _serialService.SendCommand(CommandCode);
                CommandCode = "";
            }
            else
            {
                MessageBox.Show("Lütfen bir komut kodu girin!", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendSpectralCode()
        {
            if (!string.IsNullOrEmpty(SpectralInputCode) && SpectralInputCode.Length == 2)
            {
                _serialService.SendCommand($"SPECTRAL:{SpectralInputCode}");
                ReceivedSpectralData = SpectralInputCode;
                UpdateSpectralColors(SpectralInputCode);
            }
            else
            {
                MessageBox.Show("Lütfen 2 karakterlik bir kod girin (örn: 01, 23, 12)",
                               "Hata",
                               MessageBoxButton.OK,
                               MessageBoxImage.Warning);
            }
        }

        private void SendFilterCommand(string filter) => _serialService.SendCommand($"FILTER_{filter}");

        #endregion

        #region Event Handlers

        private void OnDataReceived(object? sender, string data)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                if (data.StartsWith("SPECTRAL_ACK:"))
                {
                    ProcessSpectralData(data);
                }
                else
                {
                    var telemetryData = _parsingService.ParseTelemetryData(data);
                    if (telemetryData != null) ProcessTelemetryData(telemetryData);
                }
            });
        }

        private void OnConnectionChanged(object? sender, bool isConnected)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                IsConnected = isConnected;
                if (!isConnected) SatelliteStatus = "Bağlantı Kesildi";
            });
        }

        // Not: OnSimLineGenerated Part 1'de zaten tanımlı. Tekrarlamamak için burada TANIMLAMADIK.

        #endregion

        #region Helper Methods

        private void ProcessTelemetryData(TelemetryData t)
        {
            _currentTelemetry = t;

            // Update current values
            CurrentTemperature = t.Temperature;
            CurrentPressure = t.Pressure;
            CurrentAltitude = t.Altitude;
            CurrentSpeed = t.Speed;
            BatteryPercentage = t.BatteryPercentage;
            GpsLatitude = t.GpsLatitude;
            GpsLongitude = t.GpsLongitude;

            // Add to collections
            TelemetryDataCollection.Insert(0, t);

            // Add to chart data (keep last 200 points)
            var now = DateTime.Now;
            TemperatureData.Add(new DataPoint(now, t.Temperature));
            PressureData.Add(new DataPoint(now, t.Pressure));
            SpeedData.Add(new DataPoint(now, t.Speed));
            BatteryVoltageData.Add(new DataPoint(now, t.BatteryVoltage));
            GyroXData.Add(new DataPoint(now, t.GyroX));
            GyroYData.Add(new DataPoint(now, t.GyroY));
            GyroZData.Add(new DataPoint(now, t.GyroZ));

            if (TemperatureData.Count > 200) TemperatureData.RemoveAt(0);
            if (PressureData.Count > 200) PressureData.RemoveAt(0);
            if (SpeedData.Count > 200) SpeedData.RemoveAt(0);
            if (BatteryVoltageData.Count > 200) BatteryVoltageData.RemoveAt(0);
            if (GyroXData.Count > 200) GyroXData.RemoveAt(0);
            if (GyroYData.Count > 200) GyroYData.RemoveAt(0);
            if (GyroZData.Count > 200) GyroZData.RemoveAt(0);

            if (TelemetryDataCollection.Count > 1000)
                TelemetryDataCollection.RemoveAt(TelemetryDataCollection.Count - 1);

            // Harita
            if (_isMapInitialized && t.GpsLatitude != 0 && t.GpsLongitude != 0)
            {
                _ = Task.Run(async () =>
                {
                    try { await _mapService.UpdateGpsLocationAsync(t.GpsLatitude, t.GpsLongitude, t.Altitude); }
                    catch { }
                });
            }

            SatelliteStatus = "Veri Alınıyor";

            // ---- Stabilize Y axes ----
            _tempRange.Add(t.Temperature);
            _presRange.Add(t.Pressure);
            _speedRange.Add(t.Speed);
            _battRange.Add(t.BatteryVoltage);
            _gyroRange.Add(t.GyroX);
            _gyroRange.Add(t.GyroY);
            _gyroRange.Add(t.GyroZ);

            if (!UseFixedAxes)
            {
                if (!double.IsNaN(_tempRange.Min))
                {
                    TemperatureYAxes[0].MinLimit = _tempRange.Min;
                    TemperatureYAxes[0].MaxLimit = _tempRange.Max;
                }
                if (!double.IsNaN(_presRange.Min))
                {
                    PressureYAxes[0].MinLimit = _presRange.Min;
                    PressureYAxes[0].MaxLimit = _presRange.Max;
                }
                if (!double.IsNaN(_speedRange.Min))
                {
                    SpeedYAxes[0].MinLimit = _speedRange.Min;
                    SpeedYAxes[0].MaxLimit = _speedRange.Max;
                }
                if (!double.IsNaN(_battRange.Min))
                {
                    BatteryYAxes[0].MinLimit = _battRange.Min;
                    BatteryYAxes[0].MaxLimit = _battRange.Max;
                }
                if (!double.IsNaN(_gyroRange.Min))
                {
                    AxisYAxes[0].MinLimit = _gyroRange.Min;
                    AxisYAxes[0].MaxLimit = _gyroRange.Max;
                }
            }
        }

        // Spektral veri işleme
        private void ProcessSpectralData(string data)
        {
            if (data.StartsWith("SPECTRAL_ACK:"))
            {
                string code = data.Substring(13);
                ReceivedSpectralData = code;
                UpdateSpectralColors(code);
            }
        }

        // Multispektral renk güncelleme metodları
        private void UpdateSpectralColors(string code)
        {
            if (string.IsNullOrEmpty(code) || code.Length != 2) return;

            char firstChar = code[0];
            FirstColorValue = firstChar.ToString();
            FirstSpectralColor = GetColorFromCode(firstChar);

            char secondChar = code[1];
            SecondColorValue = secondChar.ToString();
            SecondSpectralColor = GetColorFromCode(secondChar);

            MixedSpectralColor = CalculateMixedColor(firstChar, secondChar);
        }

        private string GetColorFromCode(char code) =>
            code switch
            {
                '0' => "Transparent",
                '1' => "Red",
                '2' => "Green",
                '3' => "Blue",
                _ => "Gray"
            };

        private string CalculateMixedColor(char code1, char code2)
        {
            if (code1 == '0' && code2 == '0') return "Transparent";
            if (code1 == '0') return GetColorFromCode(code2);
            if (code2 == '0') return GetColorFromCode(code1);
            if (code1 == code2) return GetColorFromCode(code1);

            string combination = new string(new[] { code1, code2 }.OrderBy(c => c).ToArray());
            return combination switch
            {
                "12" => "#FF8800", // Turuncu
                "13" => "#FF00FF", // Magenta
                "23" => "#00FFFF", // Cyan
                _ => "Purple"
            };
        }

        private void RefreshComPorts()
        {
            AvailableComPorts.Clear();
            var ports = _serialService.GetAvailablePorts();
            foreach (var port in ports) AvailableComPorts.Add(port);
            if (AvailableComPorts.Any()) SelectedComPort = AvailableComPorts.First();
        }

        private void GenerateSampleData()
        {
            var random = new Random();
            for (int i = 0; i < 50; i++)
            {
                var sampleData = new TelemetryData
                {
                    PacketNumber = i + 1,
                    Timestamp = DateTime.Now.AddSeconds(-i * 5),
                    Temperature = 22 + random.NextDouble() * 3, // 22..25
                    Pressure = 1000 + random.NextDouble() * 20, // 1000..1020 hPa
                    Altitude = 1000 + random.NextDouble() * 50,
                    Speed = random.NextDouble() * 8,
                    BatteryVoltage = 3.7 + random.NextDouble() * 0.3,
                    GpsLatitude = 39.9334 + (random.NextDouble() - 0.5) * 0.01,
                    GpsLongitude = 32.8597 + (random.NextDouble() - 0.5) * 0.01,
                    GyroX = (random.NextDouble() - 0.5) * 10,
                    GyroY = (random.NextDouble() - 0.5) * 10,
                    GyroZ = (random.NextDouble() - 0.5) * 10
                };

                ProcessTelemetryData(sampleData);
            }
        }

        private void TestFormat2Parsing()
        {
            string testData = "$DATA,12345,25.5,1013.2,1500.0,4.2,3.85,12.5,-8.3,15.7";
            var result = _parsingService.ParseTelemetryData(testData);
            if (result != null) ProcessTelemetryData(result);
        }

        // ---- SABİT/DİNAMİK EKSEN UYGULAMA ----
        private void ApplyAxisMode()
        {
            if (UseFixedAxes) SetFixedAxes();
            else ClearFixedAxes();
        }

        private void SetFixedAxes()
        {
            TemperatureYAxes[0].MinLimit = _axisTemp.min;
            TemperatureYAxes[0].MaxLimit = _axisTemp.max;

            PressureYAxes[0].MinLimit = _axisPres.min;
            PressureYAxes[0].MaxLimit = _axisPres.max;

            SpeedYAxes[0].MinLimit = _axisSpeed.min;
            SpeedYAxes[0].MaxLimit = _axisSpeed.max;

            BatteryYAxes[0].MinLimit = _axisBatt.min;
            BatteryYAxes[0].MaxLimit = _axisBatt.max;

            AxisYAxes[0].MinLimit = _axisGyro.min;
            AxisYAxes[0].MaxLimit = _axisGyro.max;
        }

        private void ClearFixedAxes()
        {
            TemperatureYAxes[0].MinLimit = null;
            TemperatureYAxes[0].MaxLimit = null;

            PressureYAxes[0].MinLimit = null;
            PressureYAxes[0].MaxLimit = null;

            SpeedYAxes[0].MinLimit = null;
            SpeedYAxes[0].MaxLimit = null;

            BatteryYAxes[0].MinLimit = null;
            BatteryYAxes[0].MaxLimit = null;

            AxisYAxes[0].MinLimit = null;
            AxisYAxes[0].MaxLimit = null;
        }

        #endregion

        #region Filter Event Handlers

        private void OnFilterStateChanged(object? sender, string filterCode) { }
        private void OnFilterCommandSentEvent(object? sender, string command) { }
        private void OnFilterErrorEvent(object? sender, string error) { }

        #endregion

        // ---- Basit yuvarlanan min-max penceresi ----
        private sealed class RollingRange
        {
            private readonly int _capacity;
            private readonly double _pad;
            private readonly System.Collections.Generic.Queue<double> _q = new();

            public double Min { get; private set; } = double.NaN;
            public double Max { get; private set; } = double.NaN;

            // capacity: kaç örnek tutulacak; pad: min-max’a yüzde pay (0.08 => %8)
            public RollingRange(int capacity, double pad = 0.05)
            {
                _capacity = Math.Max(8, capacity);
                _pad = Math.Max(0.0, pad);
            }

            public void Add(double v)
            {
                _q.Enqueue(v);
                if (_q.Count > _capacity) _q.Dequeue();

                var min = _q.Min();
                var max = _q.Max();
                var span = Math.Max(1e-6, max - min);
                var padAbs = span * _pad;

                Min = min - padAbs;
                Max = max + padAbs;
            }
        }
    }
}
