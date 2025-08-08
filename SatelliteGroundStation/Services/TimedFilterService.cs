using System;
using System.Threading;
using System.Threading.Tasks;
using SatelliteGroundStation.Models;

namespace SatelliteGroundStation.Services
{
    public class TimedFilterService : IDisposable
    {
        private readonly SerialCommunicationService _serialService;
        private readonly MultispektralFiltreService _filterService;
        private CancellationTokenSource? _cancellationTokenSource;
        private bool _isRunning = false;

        public event EventHandler<string>? SequenceStarted;
        public event EventHandler<FilterStep>? StepStarted;
        public event EventHandler<string>? SequenceCompleted;
        public event EventHandler<string>? SequenceCancelled;

        public bool IsRunning => _isRunning;

        public TimedFilterService(SerialCommunicationService serialService, MultispektralFiltreService filterService)
        {
            _serialService = serialService ?? throw new ArgumentNullException(nameof(serialService));
            _filterService = filterService ?? throw new ArgumentNullException(nameof(filterService));
        }

        public async Task<bool> ExecuteTimedSequenceAsync(string command)
        {
            try
            {
                Console.WriteLine($"🕐 Parsing timed command: {command}");

                var timedCommand = new TimedFilterCommand(command);
                if (!timedCommand.IsValid())
                {
                    Console.WriteLine($"❌ Invalid timed command format: {command}");
                    return false;
                }

                Console.WriteLine($"✅ Parsed {timedCommand.Steps.Count} steps, total duration: {timedCommand.TotalDuration()}s");

                _serialService.SendCommand(timedCommand.CommandString);
                Console.WriteLine($"📤 Sent to Arduino: {timedCommand.CommandString}");

                await ExecuteSequenceLocallyAsync(timedCommand);
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Timed sequence error: {ex.Message}");
                return false;
            }
        }

        private async Task ExecuteSequenceLocallyAsync(TimedFilterCommand timedCommand)
        {
            _isRunning = true;
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                SequenceStarted?.Invoke(this, timedCommand.OriginalCommand);
                Console.WriteLine($"🚀 Starting timed sequence: {timedCommand.OriginalCommand}");

                foreach (var step in timedCommand.Steps)
                {
                    if (_cancellationTokenSource.Token.IsCancellationRequested)
                    {
                        SequenceCancelled?.Invoke(this, "User cancelled");
                        return;
                    }

                    Console.WriteLine($"🎯 Step: {step} (Position: {step.ServoPosition}°)");
                    StepStarted?.Invoke(this, step);

                    await _filterService.ChangeFilterAsync(step.FilterCode);
                    await Task.Delay(step.Duration * 1000, _cancellationTokenSource.Token);
                }

                SequenceCompleted?.Invoke(this, timedCommand.OriginalCommand);
                Console.WriteLine($"✅ Timed sequence completed: {timedCommand.OriginalCommand}");
            }
            catch (OperationCanceledException)
            {
                SequenceCancelled?.Invoke(this, "Sequence cancelled");
                Console.WriteLine("⏹️ Timed sequence cancelled");
            }
            finally
            {
                _isRunning = false;
                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;
            }
        }

        public void CancelSequence()
        {
            if (_isRunning && _cancellationTokenSource != null)
            {
                Console.WriteLine("⏹️ Cancelling timed sequence...");
                _cancellationTokenSource.Cancel();
            }
        }

        public static bool IsTimedCommand(string command)
        {
            var regex = new System.Text.RegularExpressions.Regex(@"^\d+[rgbpnmfyc]+", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            return regex.IsMatch(command);
        }

        public static string ValidateTimedCommand(string command)
        {
            if (string.IsNullOrWhiteSpace(command))
                return "Komut boş olamaz";

            if (!IsTimedCommand(command))
                return "Geçersiz format. Örnek: 3g5r2b";

            var timedCommand = new TimedFilterCommand(command);
            if (!timedCommand.IsValid())
                return "Komut parse edilemedi";

            if (timedCommand.TotalDuration() > 300)
                return "Toplam süre 300 saniyeyi geçemez";

            return string.Empty;
        }

        public void Dispose()
        {
            CancelSequence();
            Console.WriteLine("🗑️ TimedFilterService disposed");
        }
    }
}