using Microsoft.Win32;
using System;
using System.Drawing;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using Titanium.Web.Proxy;
using Titanium.Web.Proxy.Http;
using Titanium.Web.Proxy.Models;

namespace DentOsPrx
{
    public partial class Form1 : Form
    {
        private const string ProxyRegistryKey = "HKEY_CURRENT_USER\\Software\\Microsoft\\Windows\\CurrentVersion\\Internet Settings";

        private const int LocalProxyPort = 8888;
        private const string LocalProxyAddress = "127.0.0.1";
        private ProxyServer proxyServer;
        private string currentProxyUrl = string.Empty;

        public Form1()
        {
            InitializeComponent();
            pictureBox1.SizeMode = PictureBoxSizeMode.Zoom;
            proxyServer = new ProxyServer(true, false, false);
            this.FormClosing += new FormClosingEventHandler(Form1_FormClosing);
            UpdateInitialState();
        }

        [DllImport("wininet.dll")]
        public static extern bool InternetSetOption(IntPtr hInternet, int dwOption, IntPtr lpBuffer, int dwBufferLength);

        private void RefreshInternetSettings()
        {
            const int INTERNET_OPTION_SETTINGS_CHANGED = 39;
            const int INTERNET_OPTION_REFRESH = 37;
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_SETTINGS_CHANGED, IntPtr.Zero, 0);
            InternetSetOption(IntPtr.Zero, INTERNET_OPTION_REFRESH, IntPtr.Zero, 0);
        }

        private void EnableSystemProxy(string address)
        {
            Registry.SetValue(ProxyRegistryKey, "ProxyServer", address);
            Registry.SetValue(ProxyRegistryKey, "ProxyEnable", 1);
            RefreshInternetSettings();
        }

        private void DisableSystemProxy()
        {
            Registry.SetValue(ProxyRegistryKey, "ProxyEnable", 0);
            RefreshInternetSettings();
            currentProxyUrl = string.Empty;
        }

        private void UpdateInitialState()
        {
            int? isEnabled = Registry.GetValue(ProxyRegistryKey, "ProxyEnable", 0) as int?;

            if (isEnabled == 1)
            {
                string address = Registry.GetValue(ProxyRegistryKey, "ProxyServer", "") as string;

                if (address != null && address.StartsWith($"{LocalProxyAddress}:{LocalProxyPort}"))
                {
                    button1.BackColor = Color.LightGreen;
                    button1.Text = "Отключить";
                }
                else
                {
                    button1.BackColor = Color.SteelBlue;
                    button1.Text = "Подключиться";
                }
            }
            else
            {
                button1.BackColor = Color.SteelBlue;
                button1.Text = "Подключиться";
            }
            button1.Enabled = true;
        }
        private async Task StartLocalProxy(Uri proxyUri)
        {

            string[] userInfo = proxyUri.UserInfo.Split(':');
            string username = userInfo.Length > 0 ? userInfo[0] : "";
            string password = userInfo.Length > 1 ? userInfo[1] : "";

            var upstreamProxy = new ExternalProxy(
                proxyUri.Host,
                proxyUri.Port,
                username,
                password
            );

            upstreamProxy.ProxyType = ExternalProxyType.Socks5;
            proxyServer.UpStreamHttpProxy = upstreamProxy;
            proxyServer.UpStreamHttpsProxy = upstreamProxy;
            proxyServer.ProxyEndPoints.Clear();
            var httpListener = new ExplicitProxyEndPoint(IPAddress.Parse(LocalProxyAddress), LocalProxyPort, true);
            proxyServer.AddEndPoint(httpListener);
            proxyServer.Start();
            EnableSystemProxy($"{LocalProxyAddress}:{LocalProxyPort}");
        }

        private async Task TestLocalProxyConnection()
        {
            var webProxy = new WebProxy($"{LocalProxyAddress}:{LocalProxyPort}", true);
            var handler = new HttpClientHandler { Proxy = webProxy, UseProxy = true };

            using (var client = new System.Net.Http.HttpClient(handler))
            {
                client.Timeout = TimeSpan.FromSeconds(15);
                var response = await client.GetAsync("https://api.ipify.org");
                response.EnsureSuccessStatusCode();
            }
        }
        private async void button1_Click(object sender, EventArgs e)
        {
            string proxyUrl = textBox1.Text.Trim();
            if (button1.Text == "Отключить")
            {
                await StopAndCleanUp();
                return;
            }
            if (string.IsNullOrEmpty(proxyUrl))
            {
                MessageBox.Show("Укажите полный URL прокси.", "Ошибка", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            button1.Enabled = false;
            button1.Text = "Настройка...";
            button1.BackColor = Color.LightYellow;

            try
            {
                Uri uri = new Uri(proxyUrl);
                if (uri.Scheme != "socks")
                {
                    throw new FormatException("URL должен начинаться с 'socks://'.");
                }

                currentProxyUrl = proxyUrl;

                await StartLocalProxy(uri);
                await TestLocalProxyConnection();
                if (pictureBox1.Image == DentOsProxy.Properties.Resources.Image1)
                {
                    pictureBox1.Image = DentOsProxy.Properties.Resources.Image2;
                }
                else
                {
                    pictureBox1.Image = DentOsProxy.Properties.Resources.Image1;
                }
                button1.BackColor = Color.LightGreen;
                button1.Text = "Отключить";
            }
            catch (Exception ex)
            {
                await StopAndCleanUp(false);
                HandleError($"Ошибка подключения: проверьте URL или соединение. Детали: {ex.Message}");
            }
            finally
            {
                button1.Enabled = true;
            }
        }

        private async Task StopAndCleanUp(bool showSuccessMessage = true)
        {
            DisableSystemProxy();
            try
            {
                if (proxyServer != null)
                {
                    proxyServer.Stop();
                    proxyServer.ProxyEndPoints.Clear();
                }
            }
            catch { }

            proxyServer = new ProxyServer(true, false, false);
            UpdateInitialState();

            if (showSuccessMessage)
            {
                MessageBox.Show("Прокси успешно отключен.", "Пон", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }

        private async void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (button1.Text == "Отключить")
            {
                e.Cancel = true;
                await StopAndCleanUp(false);
                e.Cancel = false;
                pictureBox1.Image = DentOsProxy.Properties.Resources.Image1;
            }
        }

        private void HandleError(string message)
        {
            button1.BackColor = Color.SteelBlue;
            button1.Text = "Подключиться";
            pictureBox1.Image = DentOsProxy.Properties.Resources.Image1;
            MessageBox.Show(message, "Ошибка подключения", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void linkLabel1_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            System.Diagnostics.Process.Start("https://t.me/jpubl");
        }
    }
}