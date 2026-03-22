using System;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using NetworkAnalyzer.Models;

namespace NetworkAnalyzer
{
    public partial class MainWindow : Window
    {
        private DispatcherTimer _speedTimer;
        private long _lastBytesRecv;
        private long _lastBytesSent;

        public MainWindow()
        {
            InitializeComponent();
            LoadNetworkInterfaces();
            InitTimer();
        }

        private void InitTimer()
        {
            _speedTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _speedTimer.Tick += (s, e) => UpdateTrafficSpeed();
            _speedTimer.Start();
        }

        private void LoadNetworkInterfaces()
        {
            var interfaces = NetworkInterface.GetAllNetworkInterfaces()
                .Where(ni => ni.NetworkInterfaceType != NetworkInterfaceType.Loopback);
            foreach (var ni in interfaces) listInterfaces.Items.Add(ni);
            if (listInterfaces.Items.Count > 0) listInterfaces.SelectedIndex = 0;
        }

        private void UpdateTrafficSpeed()
        {
            if (listInterfaces.SelectedItem is NetworkInterface ni && ni.OperationalStatus == OperationalStatus.Up)
            {
                try {
                    var stats = ni.GetIPv4Statistics();
                    if (_lastBytesRecv > 0) {
                        txtDownload.Text = $"{(stats.BytesReceived - _lastBytesRecv) / 1024.0:F1} KB/s";
                        txtUpload.Text = $"{(stats.BytesSent - _lastBytesSent) / 1024.0:F1} KB/s";
                    }
                    _lastBytesRecv = stats.BytesReceived;
                    _lastBytesSent = stats.BytesSent;
                } catch { }
            }
        }

        private void ListInterfaces_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (listInterfaces.SelectedItem is NetworkInterface ni) {
                _lastBytesRecv = 0; _lastBytesSent = 0; // Сброс при смене адаптера
                txtInterfaceName.Text = ni.Description;
                var ip = ni.GetIPProperties().UnicastAddresses.FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                txtIpAddress.Text = ip?.Address.ToString() ?? "N/A";
                txtStatus.Text = ni.OperationalStatus == OperationalStatus.Up ? "ACTIVE" : "DOWN";
                txtSpeed.Text = $"{ni.Speed / 1000000} Mbps";
                txtMacAddress.Text = string.Join(":", ni.GetPhysicalAddress().GetAddressBytes().Select(b => b.ToString("X2")));
            }
        }

        private void BtnAnalyze_Click(object sender, RoutedEventArgs e)
        {
            string input = txtUrl.Text.Trim();
            if (string.IsNullOrEmpty(input)) return;
            try {
                Uri uri = input.Contains("://") ? new Uri(input) : new Uri("http://" + input);
                var item = new HistoryItem(input, uri.Scheme, uri.Host, uri.Port);
                if (!cmbHistory.Items.Cast<HistoryItem>().Any(x => x.Url == item.Url)) cmbHistory.Items.Add(item);
                AddResult($"Анализ: {uri.Host}");
            } catch { AddResult("Ошибка URL", true); }
        }

        private async void BtnPing_Click(object sender, RoutedEventArgs e)
        {
            string host = GetCleanHost();
            if (string.IsNullOrEmpty(host)) return;
            AddResult($"> Пинг {host}...");
            try {
                using var p = new Ping();
                var r = await p.SendPingAsync(host, 2000);
                AddResult(r.Status == IPStatus.Success ? $"✅ {r.Address}: {r.RoundtripTime}ms" : $"❌ {r.Status}", r.Status != IPStatus.Success);
            } catch (Exception ex) { AddResult("Ошибка: " + ex.GetBaseException().Message, true); }
        }

        private async void BtnDns_Click(object sender, RoutedEventArgs e)
        {
            string host = GetCleanHost();
            if (string.IsNullOrEmpty(host)) return;
            try {
                var entry = await Dns.GetHostEntryAsync(host);
                foreach (var ip in entry.AddressList) AddResult($" • IP: {ip}");
            } catch (Exception ex) { AddResult("DNS Error: " + ex.GetBaseException().Message, true); }
        }

        private async void BtnAddressType_Click(object sender, RoutedEventArgs e)
        {
            string host = GetCleanHost();
            if (string.IsNullOrEmpty(host)) return;
            try {
                IPAddress[] addrs = IPAddress.TryParse(host, out var ip) ? new[] { ip } : await Dns.GetHostAddressesAsync(host);
                foreach (var a in addrs) {
                    bool priv = a.AddressFamily == AddressFamily.InterNetwork && (a.GetAddressBytes()[0] == 10 || (a.GetAddressBytes()[0] == 192 && a.GetAddressBytes()[1] == 168));
                    string type = IPAddress.IsLoopback(a) ? "Loopback" : priv ? "Локальный (LAN)" : "Публичный (WAN)";
                    AddResult($" • {a} -> {type}");
                }
            } catch (Exception ex) { AddResult("Ошибка: " + ex.GetBaseException().Message, true); }
        }

        private string GetCleanHost() => txtUrl.Text.Contains("://") ? new Uri(txtUrl.Text).Host : txtUrl.Text.Trim();
        private void AddResult(string m, bool err = false) { txtResults.AppendText($"[{DateTime.Now:HH:mm:ss}] {(err ? "× " : "√ ")}{m}\n"); txtResults.ScrollToEnd(); }
        private void BtnClearHistory_Click(object sender, RoutedEventArgs e) => txtResults.Clear();
        private void CmbHistory_SelectionChanged(object sender, SelectionChangedEventArgs e) { if (cmbHistory.SelectedItem is HistoryItem h) txtUrl.Text = h.Url; }
    }
}