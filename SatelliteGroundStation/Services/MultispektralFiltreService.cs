using System;
using System.Threading.Tasks;
using System.Timers;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class MultispektralFiltreService : IDisposable
    {
        private readonly SerialCommunicationService _serialService;
        private Timer? _autoTimer;

        // Otomatik mod için örnek dizi (tamamen örnek)
        private readonly (int r1, int r2)[] _autoSequence = new[]
        {
            (0,0), (1,0), (2,0), (3,0), (1,3), (0,0)
        };
        private int _currentSequenceIndex = 0;

        public FilterData FilterData { get; } = new FilterData();

        public event EventHandler<string>? FilterChanged;
        public event EventHandler<string>? CommandSent;
        public event EventHandler<string>? ErrorOccurred;

        public MultispektralFiltreService(SerialCommunicationService serialService)
        {
            _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
            _serialService.DataReceived += OnSerialDataReceived;
            Console.WriteLine("🎯 MultispektralFiltreService initialized");
        }

        /// <summary>
        /// Yeni protokol: iki haneli (0..3)(0..3) kodu "SPECTRAL:xy" olarak gönderir.
        /// Arduino cevabı: "SPECTRAL_ACK:xy"
        /// </summary>
        public async Task<bool> ChangeFilterAsync(int renk1, int renk2)
        {
            try
            {
                if (renk1 is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(renk1));
                if (renk2 is < 0 or > 3) throw new ArgumentOutOfRangeException(nameof(renk2));

                var code = $"{renk1}{renk2}";
                var cmd = $"SPECTRAL:{code}";

                FilterData.IsChanging = true;

                _serialService.SendCommand(cmd);
                CommandSent?.Invoke(this, cmd);

                // mekanik için küçük bekleme (isteğe göre ayarla)
                await Task.Delay(1200);

                FilterData.CurrentFilter = code;
                FilterData.LastChangeTime = DateTime.Now;
                FilterData.IsChanging = false;

                FilterChanged?.Invoke(this, code);
                return true;
            }
            catch (Exception ex)
            {
                FilterData.IsChanging = false;
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        public void StartAutoMode(int intervalSeconds = 5)
        {
            try
            {
                StopAutoMode();
                FilterData.AutoMode = true;
                FilterData.AutoInterval = intervalSeconds;

                _autoTimer = new Timer(intervalSeconds * 1000);
                _autoTimer.Elapsed += OnAutoTimerElapsed;
                _autoTimer.AutoReset = true;
                _autoTimer.Start();
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        public void StopAutoMode()
        {
            _autoTimer?.Stop();
            _autoTimer?.Dispose();
            _autoTimer = null;
            FilterData.AutoMode = false;
        }

        private async void OnAutoTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                var (r1, r2) = _autoSequence[_currentSequenceIndex];
                await ChangeFilterAsync(r1, r2);
                _currentSequenceIndex = (_currentSequenceIndex + 1) % _autoSequence.Length;
            }
            catch (Exception ex)
            {
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        // Arduino'dan gelen satırlar
        private void OnSerialDataReceived(object? sender, string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data)) return;
                var line = data.Trim();

                // Beklenen ACK: SPECTRAL_ACK:xy
                if (line.StartsWith("SPECTRAL_ACK:", StringComparison.OrdinalIgnoreCase))
                {
                    var code = line.Substring(13).Trim();
                    if (code.Length == 2)
                    {
                        FilterData.CurrentFilter = code;
                        FilterData.IsChanging = false;
                        FilterData.LastChangeTime = DateTime.Now;
                        FilterChanged?.Invoke(this, code);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Filter data parsing error: {ex.Message}");
            }
        }

        // Kısayollar — ViewModel butonları için (tek filtreyi oynatmak istiyorsan ikinciyi 0 bırakıyoruz)
        public Task<bool> SetRedFilterAsync() => ChangeFilterAsync(1, 0);
        public Task<bool> SetGreenFilterAsync() => ChangeFilterAsync(2, 0);
        public Task<bool> SetBlueFilterAsync() => ChangeFilterAsync(3, 0);
        public Task<bool> SetPurpleFilterAsync() => ChangeFilterAsync(1, 3); // Kırmızı + Mavi
        public Task<bool> ResetToNormalAsync() => ChangeFilterAsync(0, 0);

        public void Dispose()
        {
            StopAutoMode();
            _serialService.DataReceived -= OnSerialDataReceived;
        }
    }
}
