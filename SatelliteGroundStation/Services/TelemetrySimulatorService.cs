using System;
using System.Threading;
using System.Threading.Tasks;

namespace SatelliteGroundStation.Services
{
    public class TelemetrySimulatorService
    {
        public event EventHandler<string>? LineGenerated;

        private CancellationTokenSource? _cts;
        private Task? _task;
        private readonly Random _rng = new Random();
        private int _packet = 0;

        public bool IsRunning => _task != null && !_task.IsCompleted;

        // küçük Gauss gürültüsü
        private double Noise(double mean, double std)
        {
            double u1 = 1.0 - _rng.NextDouble();
            double u2 = 1.0 - _rng.NextDouble();
            double n = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
            return mean + std * n;
        }

        private static double Clamp(double v, double min, double max)
            => v < min ? min : (v > max ? max : v);

        public void Start(double frequencyHz = 1.0) // 1 Hz = gerçek zaman
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            var token = _cts.Token;

            // --- durum (state) ---
            double t = 0.0;                 // s
            double dt = 1.0 / frequencyHz;  // s

            // İrtifa dinamiği (yumuşak)
            double alt = 300;                       // m
            double altTarget = alt;
            double nextAltChange = 30;              // s sonra
            double tauAlt = 45.0;                   // s (zaman sabiti)

            // Hız dinamiği (yumuşak)
            double spd = 10;                        // m/s
            double spdTarget = spd;
            double nextSpdChange = 20;              // s sonra
            double tauSpd = 20.0;                   // s

            // Pil
            double battV = 4.10;                    // V
            double battDropPerSec = 0.02 / 3600.0;  // ~0.02 V/saat
            double battRipplePeriod = 300.0;        // 5 dk
            double battRippleAmp = 0.005;           // 5 mV

            // Gyro drift (çok yavaş)
            double gxBias = 0, gyBias = 0, gzBias = 0;

            _task = Task.Run(async () =>
            {
                while (!token.IsCancellationRequested)
                {
                    t += dt;

                    // --- hedefleri ara ara değiştir ---
                    if (t >= nextAltChange)
                    {
                        // hedef, mevcut irtifaya göre ±150 m oynamalı, 0..2000 m bandında
                        altTarget = Clamp(alt + Noise(0, 120), 0, 2000);
                        nextAltChange = t + _rng.Next(45, 121); // 45..120 s sonra
                    }

                    if (t >= nextSpdChange)
                    {
                        spdTarget = Clamp(spd + Noise(0, 4), 0, 40);
                        nextSpdChange = t + _rng.Next(30, 91); // 30..90 s sonra
                    }

                    // --- ilk-derece (IIR) yaklaşma: x += (x* - x)*(1 - e^-dt/tau) ---
                    double aAlt = 1.0 - Math.Exp(-dt / tauAlt);
                    alt += (altTarget - alt) * aAlt;
                    alt += Noise(0, 0.8);                    // küçük gürültü
                    alt = Clamp(alt, 0, 2000);

                    double aSpd = 1.0 - Math.Exp(-dt / tauSpd);
                    spd += (spdTarget - spd) * aSpd;
                    spd += Noise(0, 0.15);
                    spd = Clamp(spd, 0, 50);

                    // --- türetilen büyüklükler ---
                    // Sıcaklık: 24 - 0.0065*alt + az gürültü
                    double temp = 24.0 - 0.0065 * alt + Noise(0, 0.05);
                    temp = Clamp(temp, -25, 45);

                    // Basınç (hPa): 1013.25 * exp(-h/8434) + az gürültü
                    double pres = 1013.25 * Math.Exp(-alt / 8434.0) + Noise(0, 0.3);

                    // Pil voltajı: yavaş düşüş + hafif ripple
                    battV = Clamp(battV - battDropPerSec, 3.50, 4.20);
                    battV += battRippleAmp * Math.Sin(2 * Math.PI * t / battRipplePeriod);

                    // Gyro: çok yavaş drift + çok ufak noise
                    gxBias += Noise(0, 0.0008);
                    gyBias += Noise(0, 0.0008);
                    gzBias += Noise(0, 0.0008);

                    double gx = gxBias + Noise(0, 0.03);
                    double gy = gyBias + Noise(0, 0.03);
                    double gz = gzBias + Noise(0, 0.03);

                    // Nadiren küçük manevra darbeleri
                    if (_rng.NextDouble() < 0.002)
                    {
                        gx += Noise(0, 1.2);
                        gy += Noise(0, 1.2);
                        gz += Noise(0, 1.2);
                    }

                    _packet++;

                    string line =
                        $"$DATA,{_packet},{temp:F1},{pres:F1},{alt:F1},{spd:F1},{battV:F2},{gx:F1},{gy:F1},{gz:F1}";

                    LineGenerated?.Invoke(this, line);

                    try { await Task.Delay(TimeSpan.FromSeconds(dt), token); }
                    catch (TaskCanceledException) { break; }
                }
            }, token);
        }

        public void Stop()
        {
            if (!IsRunning) return;
            _cts!.Cancel();
            try { _task!.Wait(500); } catch { /* ignore */ }
            _cts.Dispose();
            _cts = null;
            _task = null;
        }
    }
}
