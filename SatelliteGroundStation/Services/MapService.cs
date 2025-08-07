using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SatelliteGroundStation.Services
{
    public class MapService
    {
        private WebView2? _webView;
        private readonly List<GpsPoint> _gpsTrack = new();
        private bool _isInitialized = false;

        public event EventHandler<string>? MapError;

        public async Task InitializeAsync(WebView2 webView)
        {
            _webView = webView;

            try
            {
                await _webView.EnsureCoreWebView2Async();

                // HTML harita sayfasını yükle
                string mapHtml = GenerateMapHtml();
                _webView.NavigateToString(mapHtml);

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                MapError?.Invoke(this, $"Harita başlatma hatası: {ex.Message}");
            }
        }

        public async Task UpdateGpsLocationAsync(double latitude, double longitude, double altitude = 0)
        {
            if (!_isInitialized || _webView == null)
                return;

            try
            {
                // GPS noktasını listeye ekle
                _gpsTrack.Add(new GpsPoint
                {
                    Latitude = latitude,
                    Longitude = longitude,
                    Altitude = altitude,
                    Timestamp = DateTime.Now
                });

                // Son 100 noktayı tut (performans için)
                if (_gpsTrack.Count > 100)
                    _gpsTrack.RemoveAt(0);

                // JavaScript ile haritayı güncelle
                string jsCommand = $@"
                    updateSatellitePosition({latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 
                                           {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)},
                                           {altitude});
                ";

                await _webView.CoreWebView2.ExecuteScriptAsync(jsCommand);
            }
            catch (Exception ex)
            {
                MapError?.Invoke(this, $"GPS güncelleme hatası: {ex.Message}");
            }
        }

        public async Task DrawFlightPathAsync()
        {
            if (!_isInitialized || _webView == null || !_gpsTrack.Any())
                return;

            try
            {
                // GPS track'ini JavaScript array'ine çevir
                var pathPoints = _gpsTrack.Select(p =>
                    $"[{p.Latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, {p.Longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}]"
                );

                string pathArray = "[" + string.Join(", ", pathPoints) + "]";

                string jsCommand = $"drawFlightPath({pathArray});";
                await _webView.CoreWebView2.ExecuteScriptAsync(jsCommand);
            }
            catch (Exception ex)
            {
                MapError?.Invoke(this, $"Rota çizme hatası: {ex.Message}");
            }
        }

        public async Task SetMapCenterAsync(double latitude, double longitude, int zoom = 15)
        {
            if (!_isInitialized || _webView == null)
                return;

            try
            {
                string jsCommand = $@"
                    map.setView([{latitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}, 
                               {longitude.ToString(System.Globalization.CultureInfo.InvariantCulture)}], {zoom});
                ";

                await _webView.CoreWebView2.ExecuteScriptAsync(jsCommand);
            }
            catch (Exception ex)
            {
                MapError?.Invoke(this, $"Harita merkezi ayarlama hatası: {ex.Message}");
            }
        }

        private string GenerateMapHtml()
        {
            return @"
<!DOCTYPE html>
<html>
<head>
    <title>Satellite GPS Tracking</title>
    <meta charset=""utf-8"" />
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <link rel=""stylesheet"" href=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.css"" />
    <script src=""https://unpkg.com/leaflet@1.9.4/dist/leaflet.js""></script>
    <style>
        body { margin: 0; padding: 0; background: #000; }
        #map { height: 100vh; width: 100%; }
        .satellite-info {
            background: rgba(0,0,0,0.8);
            color: white;
            padding: 8px;
            border-radius: 4px;
            font-family: 'Consolas', monospace;
            font-size: 12px;
        }
    </style>
</head>
<body>
    <div id=""map""></div>

    <script>
        // Harita başlat (Türkiye - Ankara merkez)
        var map = L.map('map').setView([39.9334, 32.8597], 10);

        // OpenStreetMap tile layer ekle
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors'
        }).addTo(map);

        // Satellite marker
        var satelliteIcon = L.divIcon({
            className: 'satellite-marker',
            html: '🛰️',
            iconSize: [20, 20],
            iconAnchor: [10, 10]
        });

        var satelliteMarker = null;
        var flightPath = null;
        var pathCoordinates = [];

        // Satellite pozisyonunu güncelle
        function updateSatellitePosition(lat, lng, altitude) {
            var position = [lat, lng];
            
            // Marker'ı güncelle
            if (satelliteMarker) {
                satelliteMarker.setLatLng(position);
            } else {
                satelliteMarker = L.marker(position, {icon: satelliteIcon}).addTo(map);
            }

            // Info popup güncelle
            var infoText = 
                '<div class=""satellite-info"">' +
                '<b>Uydu Konumu</b><br>' +
                'Enlem: ' + lat.toFixed(6) + '°<br>' +
                'Boylam: ' + lng.toFixed(6) + '°<br>' +
                'Yükseklik: ' + altitude.toFixed(1) + ' m<br>' +
                'Zaman: ' + new Date().toLocaleTimeString('tr-TR') +
                '</div>';
            
            satelliteMarker.bindPopup(infoText).openPopup();

            // Path koordinatlarına ekle
            pathCoordinates.push(position);
            
            // Son 50 noktayı tut
            if (pathCoordinates.length > 50) {
                pathCoordinates.shift();
            }
            
            // Flight path'i çiz
            drawFlightPath(pathCoordinates);
        }

        // Uçuş rotasını çiz
        function drawFlightPath(coordinates) {
            if (flightPath) {
                map.removeLayer(flightPath);
            }
            
            if (coordinates.length > 1) {
                flightPath = L.polyline(coordinates, {
                    color: 'red',
                    weight: 3,
                    opacity: 0.8,
                    dashArray: '5, 10'
                }).addTo(map);
            }
        }

        // Harita tipini değiştir
        function changeMapType(type) {
            // Satellite, terrain, etc. için farklı tile server'lar
            switch(type) {
                case 'satellite':
                    L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}').addTo(map);
                    break;
                case 'terrain':
                    L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png').addTo(map);
                    break;
                default:
                    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png').addTo(map);
            }
        }

        console.log('Map initialized successfully');
    </script>
</body>
</html>";
        }

        public void ClearTrack()
        {
            _gpsTrack.Clear();
        }

        public List<GpsPoint> GetGpsTrack()
        {
            return _gpsTrack.ToList();
        }
    }

    public class GpsPoint
    {
        public double Latitude { get; set; }
        public double Longitude { get; set; }
        public double Altitude { get; set; }
        public DateTime Timestamp { get; set; }
    }
}

