using System;
using System.Collections.Generic;
using System.Linq;

namespace SatelliteGroundStation.Services
{
    /// <summary>
    /// Son N örnekten alt/üst yüzdeliklere göre hedef aralık belirler,
    /// Min/MaxLimit'i EMA ile yumuşatır. Böylece eksen stabil kalır.
    /// </summary>
    public class AxisRangeStabilizer
    {
        private readonly int _window;
        private readonly double _pLo, _pHi;
        private readonly double _minWidth;
        private readonly double _padding;
        private readonly double _alpha; // 0..1, 1 çok hızlı, 0 çok yavaş

        private readonly Queue<double> _buf = new();

        public double Min { get; private set; } = double.NaN;
        public double Max { get; private set; } = double.NaN;

        public AxisRangeStabilizer(
            int window = 120,      // son 120 nokta (1 Hz ise ~2 dk)
            double pLo = 0.10,     // %10 alt bant
            double pHi = 0.90,     // %90 üst bant
            double minWidth = 1.0, // en az eksen genişliği
            double padding = 0.1,  // banda ek kenar boşluğu
            double alpha = 0.15    // EMA yumuşatma
        )
        {
            _window = Math.Max(10, window);
            _pLo = Math.Clamp(pLo, 0.0, 0.49);
            _pHi = Math.Clamp(pHi, 0.51, 1.0);
            _minWidth = Math.Max(1e-6, minWidth);
            _padding = Math.Max(0, padding);
            _alpha = Math.Clamp(alpha, 0.01, 1.0);
        }

        public void Add(double v)
        {
            _buf.Enqueue(v);
            while (_buf.Count > _window) _buf.Dequeue();

            if (_buf.Count < 10) return;

            // yüzdelikler (robust)
            var arr = _buf.OrderBy(x => x).ToArray();
            double Quantile(double q)
            {
                if (q <= 0) return arr.First();
                if (q >= 1) return arr.Last();
                double pos = q * (arr.Length - 1);
                int i = (int)Math.Floor(pos);
                int j = Math.Min(i + 1, arr.Length - 1);
                double f = pos - i;
                return arr[i] * (1 - f) + arr[j] * f;
            }

            var qLo = Quantile(_pLo);
            var qHi = Quantile(_pHi);

            // ped ekleyelim
            double width = Math.Max(qHi - qLo, _minWidth);
            double targetMin = qLo - _padding * width;
            double targetMax = qHi + _padding * width;

            // ilk kez set
            if (double.IsNaN(Min) || double.IsNaN(Max))
            {
                Min = targetMin;
                Max = targetMax;
                return;
            }

            // EMA ile yumuşatma (genişleme hızlı, daralma yavaş)
            // genişlemeyi hızlı yapmak için "target daha dışarıdaysa" alpha'yı artırabiliriz
            double aExpand = Math.Min(1.0, _alpha * 2.0);

            // Min
            if (targetMin < Min) Min = Min + aExpand * (targetMin - Min); // aşağı doğru genişleme
            else Min = Min + _alpha * (targetMin - Min);                  // yukarı daralma

            // Max
            if (targetMax > Max) Max = Max + aExpand * (targetMax - Max); // yukarı doğru genişleme
            else Max = Max + _alpha * (targetMax - Max);                  // aşağı daralma

            // min width garantisi
            if (Max - Min < _minWidth)
            {
                double mid = (Max + Min) * 0.5;
                Min = mid - _minWidth * 0.5;
                Max = mid + _minWidth * 0.5;
            }
        }
    }
}
