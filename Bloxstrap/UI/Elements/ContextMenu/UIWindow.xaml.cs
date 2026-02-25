using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.NetworkInformation;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Voidstrap.UI.Elements.Overlay
{
    public class OverlayWindow : Window, INotifyPropertyChanged
    {
        private int _frames;
        private int _fps;
        private readonly Stopwatch _fpsStopwatch = Stopwatch.StartNew();

        private TextBlock _fpsTextBlock;
        private TextBlock _pingTextBlock;
        private TextBlock _locationTextBlock;
        private TextBlock _timeTextBlock;

        private readonly DispatcherTimer _updateTimer;

        private readonly bool _showFPS = App.Settings.Prop.FPSCounter;
        private readonly bool _showPing = App.Settings.Prop.ServerPingCounter;
        private readonly bool _showTime = App.Settings.Prop.CurrentTimeDisplay;
        private readonly bool _showLocation = App.Settings.Prop.ShowServerDetailsUI;

        private const double DefaultBrightness = 50;
        private double _brightness = App.Settings.Prop.Brightness;
        private double _lastAppliedBrightness = App.Settings.Prop.Brightness;

        private Border _darkOverlay;
        private Border _brightOverlay;

        private string _serverIp;
        private string _lastServerIp;
        private bool _locationFetching;
        private string _serverLocation = "Location: --";

        private static readonly HttpClient Http;

        static OverlayWindow()
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            Http = new HttpClient(new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate
            })
            {
                Timeout = TimeSpan.FromSeconds(4)
            };

            Http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Voidstrap/1.0 (+https://github.com/voidstrap)"
            );
            Http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        }

        public OverlayWindow()
        {
            Width = SystemParameters.PrimaryScreenWidth;
            Height = SystemParameters.PrimaryScreenHeight;

            Left = 0;
            Top = 0;

            AllowsTransparency = true;
            Background = Brushes.Transparent;
            WindowStyle = WindowStyle.None;
            Topmost = true;
            ShowInTaskbar = false;

            var root = new Grid();

            _darkOverlay = new Border
            {
                Background = Brushes.Black,
                Opacity = 0
            };
            // fakass method to make black and white work ( dont do this shit at home ah )
            _brightOverlay = new Border
            {
                Background = Brushes.White,
                Opacity = 0
            };

            root.Children.Add(_darkOverlay);
            root.Children.Add(_brightOverlay);

            var panel = new StackPanel
            {
                Orientation = Orientation.Vertical,
                Margin = new Thickness(SystemParameters.PrimaryScreenWidth - 260 - -95, 10, 0, 0)
            };

            if (_showFPS)
            {
                _fpsTextBlock = CreateTextBlock(Brushes.Lime);
                panel.Children.Add(_fpsTextBlock);
            }

            if (_showPing)
            {
                _pingTextBlock = CreateTextBlock(Brushes.LightSkyBlue);
                panel.Children.Add(_pingTextBlock);
            }

            if (_showLocation)
            {
                _locationTextBlock = CreateTextBlock(Brushes.LightGreen);
                _locationTextBlock.Text = _serverLocation;
                panel.Children.Add(_locationTextBlock);
            }

            if (_showTime)
            {
                _timeTextBlock = CreateTextBlock(Brushes.Cyan);
                panel.Children.Add(_timeTextBlock);
            }

            root.Children.Add(panel);
            Content = root;

            CompositionTarget.Rendering += OnRendering;

            _updateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(2)
            };
            _updateTimer.Tick += async (_, __) => await UpdateStatsAsync();
            _updateTimer.Start();

            Loaded += (_, __) =>
            {
                MakeClickThrough();
                ApplyBrightness();
            };

            Closing += (_, __) => CompositionTarget.Rendering -= OnRendering;
        }

        public double Brightness
        {
            get => _brightness;
            set
            {
                double clamped = Math.Clamp(value, 0, 100);
                if (_brightness != clamped)
                {
                    _brightness = clamped;
                    App.Settings.Prop.Brightness = clamped;
                    ApplyBrightness();
                    OnPropertyChanged(nameof(Brightness));
                }
            }
        }

        private void ApplyBrightness()
        {
            if (_brightness == DefaultBrightness)
            {
                _darkOverlay.Opacity = 0;
                _brightOverlay.Opacity = 0;
                return;
            }

            if (_brightness < DefaultBrightness)
            {
                double percent = (DefaultBrightness - _brightness) / DefaultBrightness;
                _darkOverlay.Opacity = percent;
                _brightOverlay.Opacity = 0;
            }
            else
            {
                double percent = (_brightness - DefaultBrightness) / DefaultBrightness;
                _brightOverlay.Opacity = percent;
                _darkOverlay.Opacity = 0;
            }
        }

        private void OnRendering(object sender, EventArgs e)
        {
            if (_showFPS)
            {
                _frames++;
                if (_fpsStopwatch.ElapsedMilliseconds >= 1000)
                {
                    _fps = _frames;
                    _frames = 0;
                    _fpsStopwatch.Restart();
                    _fpsTextBlock.Text = $"FPS: {_fps}";
                }
            }

            if (Math.Abs(App.Settings.Prop.Brightness - _lastAppliedBrightness) > 0.01)
            {
                _lastAppliedBrightness = App.Settings.Prop.Brightness;
                Brightness = _lastAppliedBrightness;
            }

            if (!IsRobloxForeground())
            {
                if (IsVisible) Hide();
            }
            else if (!IsVisible)
            {
                Show();
            }
        }

        private async Task UpdateStatsAsync()
        {
            if (_showTime)
                _timeTextBlock.Text = DateTime.Now.ToString("h:mm tt");

            if (!_showPing) return;

            _serverIp = GetRobloxServerIp();

            if (string.IsNullOrEmpty(_serverIp))
            {
                _pingTextBlock.Text = "Ping: --";
                return;
            }

            int ping = await PingServerAsync(_serverIp);
            _pingTextBlock.Text = ping > 0 ? $"Ping: {ping} ms" : "Ping: --";

            if (_showLocation && !_locationFetching && _serverIp != _lastServerIp)
            {
                _lastServerIp = _serverIp;
                _locationFetching = true;
                _locationTextBlock.Text = "Location: --";

                _ = Task.Run(async () =>
                {
                    string loc = await GetServerLocationAsync(_serverIp);
                    Dispatcher.Invoke(() =>
                    {
                        _serverLocation = loc;
                        _locationTextBlock.Text = loc;
                        _locationFetching = false;
                    });
                });
            }
        }

        private string GetRobloxServerIp()
        {
            try
            {
                return IPGlobalProperties.GetIPGlobalProperties()
                    .GetActiveTcpConnections()
                    .FirstOrDefault(c =>
                        c.State == TcpState.Established &&
                        !IPAddress.IsLoopback(c.RemoteEndPoint.Address))
                    ?.RemoteEndPoint.Address.ToString();
            }
            catch { return null; }
        }

        private async Task<int> PingServerAsync(string ip)
        {
            try
            {
                using var ping = new Ping();
                var reply = await ping.SendPingAsync(ip, 1000);
                return reply.Status == IPStatus.Success ? (int)reply.RoundtripTime : -1;
            }
            catch { return -1; }
        }

        private async Task<string> GetServerLocationAsync(string ip)
        {
            try
            {
                using var res = await Http.GetAsync($"https://ipinfo.io/{ip}/json");
                if (!res.IsSuccessStatusCode) return "Location: Unknown";

                string json = await res.Content.ReadAsStringAsync();
                using var doc = JsonDocument.Parse(json);
                var root = doc.RootElement;

                string city = root.TryGetProperty("city", out var c) ? c.GetString() : null;
                string country = root.TryGetProperty("country", out var co) ? co.GetString() : null;

                return !string.IsNullOrEmpty(city)
                    ? $"Location: {city} {CountryToFlag(country)}"
                    : "Location: Unknown";
            }
            catch { return "Location: Unknown"; }
        }

        private static string CountryToFlag(string cc)
        {
            if (string.IsNullOrEmpty(cc) || cc.Length != 2) return "";
            int offset = 0x1F1E6;
            return char.ConvertFromUtf32(offset + cc[0] - 'A') +
                   char.ConvertFromUtf32(offset + cc[1] - 'A');
        }

        private static TextBlock CreateTextBlock(Brush color) => new()
        {
            FontSize = 16,
            FontWeight = FontWeights.SemiBold,
            Foreground = color
        };

        private void MakeClickThrough()
        {
            var hwnd = new WindowInteropHelper(this).Handle;
            int style = GetWindowLong(hwnd, -20);
            SetWindowLong(hwnd, -20, style | 0x20 | 0x80);
        }

        private static bool IsRobloxForeground()
        {
            IntPtr hwnd = GetForegroundWindow();
            GetWindowThreadProcessId(hwnd, out uint pid);

            try
            {
                return Process.GetProcessById((int)pid)
                    .ProcessName.Equals("RobloxPlayerBeta", StringComparison.OrdinalIgnoreCase);
            }
            catch { return false; }
        }

        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        [DllImport("user32.dll")] private static extern int GetWindowLong(IntPtr hWnd, int nIndex);
        [DllImport("user32.dll")] private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
    }
}