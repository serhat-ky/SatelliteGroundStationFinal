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
        private readonly string[] _autoSequence = { "R", "G", "B", "N" };
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

        // Manuel filtre değiştirme
        public async Task<bool> ChangeFilterAsync(string filterCode)
        {
            try
            {
                Console.WriteLine($"🎯 Changing filter to: {filterCode}");

                if (!FilterCommand.FilterPositions.ContainsKey(filterCode))
                {
                    throw new ArgumentException($"Geçersiz filtre kodu: {filterCode}");
                }

                FilterData.IsChanging = true;

                var command = FilterCommand.CreateCommand(filterCode);
                _serialService.SendCommand(command.CommandString);

                CommandSent?.Invoke(this, command.CommandString);
                Console.WriteLine($"📤 Filter command sent: {command.CommandString}");

                // Servo hareket süresini bekle
                await Task.Delay(2000);

                FilterData.CurrentFilter = filterCode;
                FilterData.ServoPosition = command.ServoPosition;
                FilterData.LastChangeTime = DateTime.Now;
                FilterData.IsChanging = false;

                FilterChanged?.Invoke(this, filterCode);
                Console.WriteLine($"✅ Filter changed successfully to: {FilterData.FilterDescription}");

                return true;
            }
            catch (Exception ex)
            {
                FilterData.IsChanging = false;
                Console.WriteLine($"❌ Filter change error: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
                return false;
            }
        }

        // Otomatik mod kontrolü
        public void StartAutoMode(int intervalSeconds = 5)
        {
            try
            {
                StopAutoMode(); // Mevcut timer'ı durdur

                FilterData.AutoMode = true;
                FilterData.AutoInterval = intervalSeconds;

                _autoTimer = new Timer(intervalSeconds * 1000);
                _autoTimer.Elapsed += OnAutoTimerElapsed;
                _autoTimer.AutoReset = true;
                _autoTimer.Start();

                Console.WriteLine($"🔄 Auto mode started with {intervalSeconds}s interval");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Auto mode start error: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        public void StopAutoMode()
        {
            if (_autoTimer != null)
            {
                _autoTimer.Stop();
                _autoTimer.Dispose();
                _autoTimer = null;
            }

            FilterData.AutoMode = false;
            Console.WriteLine("⏹️ Auto mode stopped");
        }

        private async void OnAutoTimerElapsed(object? sender, ElapsedEventArgs e)
        {
            try
            {
                string nextFilter = _autoSequence[_currentSequenceIndex];
                await ChangeFilterAsync(nextFilter);

                _currentSequenceIndex = (_currentSequenceIndex + 1) % _autoSequence.Length;
                Console.WriteLine($"🔄 Auto sequence: {nextFilter} -> Next: {_autoSequence[_currentSequenceIndex]}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Auto timer error: {ex.Message}");
                ErrorOccurred?.Invoke(this, ex.Message);
            }
        }

        // Arduino'dan gelen filtre durum verilerini işle
        private void OnSerialDataReceived(object? sender, string data)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(data))
                    return;

                data = data.Trim().ToUpper();

                // Filtre durum mesajları: $FILTER_STATUS,R,45,OK
                if (data.StartsWith("$FILTER_STATUS,"))
                {
                    ParseFilterStatus(data);
                }
                // Filtre onay mesajları: $FILTER_ACK,G,90
                else if (data.StartsWith("$FILTER_ACK,"))
                {
                    ParseFilterAck(data);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Filter data parsing error: {ex.Message}");
            }
        }

        private void ParseFilterStatus(string data)
        {
            try
            {
                // Format: $FILTER_STATUS,R,45,OK
                var parts = data.Substring(15).Split(','); // "$FILTER_STATUS," kısmını atla

                if (parts.Length >= 3)
                {
                    string filterCode = parts[0];
                    int position = int.Parse(parts[1]);
                    string status = parts[2];

                    FilterData.CurrentFilter = filterCode;
                    FilterData.ServoPosition = position;
                    FilterData.IsChanging = status != "OK";

                    Console.WriteLine($"📡 Filter status: {filterCode} at {position}° - {status}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Filter status parsing error: {ex.Message}");
            }
        }

        private void ParseFilterAck(string data)
        {
            try
            {
                // Format: $FILTER_ACK,G,90
                var parts = data.Substring(12).Split(','); // "$FILTER_ACK," kısmını atla

                if (parts.Length >= 2)
                {
                    string filterCode = parts[0];
                    int position = int.Parse(parts[1]);

                    FilterData.CurrentFilter = filterCode;
                    FilterData.ServoPosition = position;
                    FilterData.IsChanging = false;
                    FilterData.LastChangeTime = DateTime.Now;

                    Console.WriteLine($"✅ Filter ACK: {filterCode} confirmed at {position}°");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Filter ACK parsing error: {ex.Message}");
            }
        }

        // Hızlı erişim metodları
        public Task<bool> SetRedFilterAsync() => ChangeFilterAsync("R");
        public Task<bool> SetGreenFilterAsync() => ChangeFilterAsync("G");
        public Task<bool> SetBlueFilterAsync() => ChangeFilterAsync("B");
        public Task<bool> SetNormalFilterAsync() => ChangeFilterAsync("N");
        public Task<bool> SetPurpleFilterAsync() => ChangeFilterAsync("P");
        public Task<bool> SetYellowFilterAsync() => ChangeFilterAsync("Y");
        public Task<bool> SetCyanFilterAsync() => ChangeFilterAsync("C");

        // Sıfırlama
        public async Task<bool> ResetToNormalAsync()
        {
            Console.WriteLine("🔄 Resetting filter to normal position...");
            return await ChangeFilterAsync("N");
        }

        // Test fonksiyonu
        public async Task RunFilterTestAsync()
        {
            Console.WriteLine("🧪 Starting filter test sequence...");

            string[] testSequence = { "N", "R", "G", "B", "N" };

            foreach (string filter in testSequence)
            {
                Console.WriteLine($"🧪 Testing filter: {filter}");
                await ChangeFilterAsync(filter);
                await Task.Delay(3000); // Her filtre için 3 saniye bekle
            }

            Console.WriteLine("🧪 Filter test completed!");
        }

        public void Dispose()
        {
            StopAutoMode();

            if (_serialService != null)
            {
                _serialService.DataReceived -= OnSerialDataReceived;
            }

            Console.WriteLine("🎯 MultispektralFiltreService disposed");
        }
    }
}