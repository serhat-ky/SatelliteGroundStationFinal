using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using AForge.Video;
using AForge.Video.DirectShow;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace SatelliteGroundStation.Services
{
    public class VideoCaptureService : IDisposable
    {
        private VideoCaptureDevice? _videoSource;
        private FilterInfoCollection? _videoDevices;
        private bool _isCapturing = false;
        private readonly Dispatcher _dispatcher;

        public event EventHandler<BitmapSource>? FrameReceived;
        public event EventHandler<string>? ErrorOccurred;

        public bool IsCapturing => _isCapturing;

        public VideoCaptureService()
        {
            _dispatcher = Application.Current.Dispatcher;
            RefreshDeviceList();
        }

        /// <summary>
        /// Mevcut video capture cihazlarını listeler
        /// </summary>
        public List<VideoDeviceInfo> GetAvailableDevices()
        {
            var devices = new List<VideoDeviceInfo>();

            try
            {
                RefreshDeviceList();

                if (_videoDevices != null)
                {
                    for (int i = 0; i < _videoDevices.Count; i++)
                    {
                        devices.Add(new VideoDeviceInfo
                        {
                            Index = i,
                            Name = _videoDevices[i].Name,
                            MonikerString = _videoDevices[i].MonikerString
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Cihaz listesi alınırken hata: {ex.Message}");
            }

            return devices;
        }

        /// <summary>
        /// Belirtilen cihazdan video yakalamayı başlatır
        /// </summary>
        public bool StartCapture(int deviceIndex, VideoResolution resolution = VideoResolution.Resolution_640x480)
        {
            try
            {
                if (_isCapturing)
                {
                    StopCapture();
                }

                RefreshDeviceList();

                if (_videoDevices == null || deviceIndex >= _videoDevices.Count)
                {
                    OnErrorOccurred("Geçersiz cihaz indexi");
                    return false;
                }

                _videoSource = new VideoCaptureDevice(_videoDevices[deviceIndex].MonikerString);

                // Video çözünürlüğünü ayarla
                SetVideoResolution(resolution);

                _videoSource.NewFrame += OnNewFrame;
                _videoSource.VideoSourceError += OnVideoSourceError;

                _videoSource.Start();
                _isCapturing = true;

                return true;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Video yakalama başlatılırken hata: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Video yakalamayı durdurur
        /// </summary>
        public void StopCapture()
        {
            try
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= OnNewFrame;
                    _videoSource.VideoSourceError -= OnVideoSourceError;
                    _videoSource = null;
                }
                _isCapturing = false;
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Video yakalama durdurulurken hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Mevcut frame'i kaydet
        /// </summary>
        public void SaveCurrentFrame(string filePath)
        {
            // Bu metod son frame'i dosyaya kaydeder
            // Implementation gerektiğinde eklenebilir
        }

        private void RefreshDeviceList()
        {
            try
            {
                _videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Cihaz listesi yenilenirken hata: {ex.Message}");
            }
        }

        private void SetVideoResolution(VideoResolution resolution)
        {
            if (_videoSource?.VideoCapabilities != null && _videoSource.VideoCapabilities.Length > 0)
            {
                var (width, height) = GetResolutionDimensions(resolution);

                // En uygun çözünürlüğü bul
                var bestCapability = _videoSource.VideoCapabilities
                    .OrderBy(cap => Math.Abs(cap.FrameSize.Width - width) + Math.Abs(cap.FrameSize.Height - height))
                    .FirstOrDefault();

                if (bestCapability != null)
                {
                    _videoSource.VideoResolution = bestCapability;
                }
            }
        }

        private (int width, int height) GetResolutionDimensions(VideoResolution resolution)
        {
            return resolution switch
            {
                VideoResolution.Resolution_320x240 => (320, 240),
                VideoResolution.Resolution_640x480 => (640, 480),
                VideoResolution.Resolution_800x600 => (800, 600),
                VideoResolution.Resolution_1024x768 => (1024, 768),
                VideoResolution.Resolution_1280x720 => (1280, 720),
                VideoResolution.Resolution_1920x1080 => (1920, 1080),
                _ => (640, 480)
            };
        }

        private void OnNewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                // Bitmap'i BitmapSource'a dönüştür
                var bitmap = (Bitmap)eventArgs.Frame.Clone();

                _dispatcher.BeginInvoke(() =>
                {
                    try
                    {
                        var bitmapSource = ConvertBitmapToBitmapSource(bitmap);
                        FrameReceived?.Invoke(this, bitmapSource);
                    }
                    catch (Exception ex)
                    {
                        OnErrorOccurred($"Frame işlenirken hata: {ex.Message}");
                    }
                    finally
                    {
                        bitmap?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                OnErrorOccurred($"Yeni frame alınırken hata: {ex.Message}");
            }
        }

        private void OnVideoSourceError(object sender, VideoSourceErrorEventArgs eventArgs)
        {
            _dispatcher.BeginInvoke(() =>
            {
                OnErrorOccurred($"Video kaynak hatası: {eventArgs.Description}");
                _isCapturing = false;
            });
        }

        private BitmapSource ConvertBitmapToBitmapSource(Bitmap bitmap)
        {
            var bitmapData = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                System.Drawing.Imaging.ImageLockMode.ReadOnly,
                bitmap.PixelFormat);

            var bitmapSource = BitmapSource.Create(
                bitmapData.Width, bitmapData.Height,
                bitmap.HorizontalResolution, bitmap.VerticalResolution,
                System.Windows.Media.PixelFormats.Bgr24, null,
                bitmapData.Scan0, bitmapData.Stride * bitmapData.Height, bitmapData.Stride);

            bitmap.UnlockBits(bitmapData);
            return bitmapSource;
        }

        private void OnErrorOccurred(string message)
        {
            ErrorOccurred?.Invoke(this, message);
        }

        public void Dispose()
        {
            StopCapture();
        }
    }

    // Video cihaz bilgileri
    public class VideoDeviceInfo
    {
        public int Index { get; set; }
        public string Name { get; set; } = string.Empty;
        public string MonikerString { get; set; } = string.Empty;

        public override string ToString() => Name;
    }

    // Video çözünürlük seçenekleri
    public enum VideoResolution
    {
        Resolution_320x240,
        Resolution_640x480,
        Resolution_800x600,
        Resolution_1024x768,
        Resolution_1280x720,
        Resolution_1920x1080
    }
}