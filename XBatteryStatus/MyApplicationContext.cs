using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;

namespace XBatteryStatus
{
    public class MyApplicationContext : ApplicationContext
    {
        private string version = "V1.3.3";
        private string releaseUrl = @"https://github.com/tommaier123/XBatteryStatus/releases";

        NotifyIcon notifyIcon = new NotifyIcon();
        private ContextMenuStrip contextMenu;
        private ToolStripMenuItem themeButton;
        private ToolStripMenuItem hideButton;
        private ToolStripMenuItem numbersButton;

        private Timer UpdateTimer;
        private Timer DiscoverTimer;
        private Timer HideTimeoutTimer;
        private Timer SoftwareUpdateTimer;

        public List<BluetoothLEDevice> pairedGamepads = new List<BluetoothLEDevice>();
        public BluetoothLEDevice connectedGamepad;
        public GattCharacteristic batteryCharacteristic;
        public Radio bluetoothRadio;

        private int lastBattery = 100;

        private bool lightMode = false;

        public MyApplicationContext()
        {
            HideTimeoutTimer = new Timer();
            HideTimeoutTimer.Tick += new EventHandler((x, y) => HideTimeout());
            HideTimeoutTimer.Interval = 10000;
            HideTimeoutTimer.Start();

            SoftwareUpdateTimer = new Timer();
            SoftwareUpdateTimer.Tick += new EventHandler((x, y) => { CheckSoftwareUpdate(); });
            SoftwareUpdateTimer.Interval = 30000;
            SoftwareUpdateTimer.Start();

            lightMode = IsLightMode();
            notifyIcon.Icon = GetIcon(-1, "?");
            notifyIcon.Text = "XBatteryStatus: Looking for paired controller";
            notifyIcon.Visible = true;

            contextMenu = new ContextMenuStrip();

            themeButton = new ToolStripMenuItem("Theme");
            themeButton.DropDownItems.Add("Auto", null, ThemeClicked);
            themeButton.DropDownItems.Add("Light", null, ThemeClicked);
            themeButton.DropDownItems.Add("Dark", null, ThemeClicked);
            UpdateThemeButton();
            contextMenu.Items.Add(themeButton);

            hideButton = new ToolStripMenuItem("Auto Hide", null, HideClicked);
            UpdateHideButton();
            contextMenu.Items.Add(hideButton);

            numbersButton = new ToolStripMenuItem("Numeric", null, NumbersClicked);
            UpdateNumbersButton();
            contextMenu.Items.Add(numbersButton);

            ToolStripMenuItem versionButton = new ToolStripMenuItem(version, null, new EventHandler(VersionClicked));
            contextMenu.Items.Add(versionButton);

            ToolStripMenuItem exitButton = new ToolStripMenuItem("Exit", null, new EventHandler(ExitClicked));
            contextMenu.Items.Add(exitButton);

            notifyIcon.ContextMenuStrip = contextMenu;

            var radios = Radio.GetRadiosAsync().GetResults();
            bluetoothRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
            if (bluetoothRadio != null)
            {
                bluetoothRadio.StateChanged += BluetoothRadio_StateChanged;
            }


            FindBleController();

            UpdateTimer = new Timer();
            UpdateTimer.Tick += new EventHandler((x, y) => Update());
            UpdateTimer.Interval = 10000;
            UpdateTimer.Start();

            DiscoverTimer = new Timer();
            DiscoverTimer.Tick += new EventHandler((x, y) => FindBleController());
            DiscoverTimer.Interval = 60000;
            DiscoverTimer.Start();
        }

        private void CheckSoftwareUpdate()
        {
            try
            {
                Octokit.GitHubClient github = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("XBatteryStatus"));
                var all = github.Repository.Release.GetAll("tommaier123", "XBatteryStatus").Result.Where(x => x.Prerelease == false).ToList();
                var latest = all.OrderByDescending(x => Int32.Parse(x.TagName.Substring(1).Replace(".", ""))).FirstOrDefault();

                if (latest != null && Int32.Parse(version.Substring(1).Replace(".", "")) < Int32.Parse(latest.TagName.Substring(1).Replace(".", "")))
                {
                    if (Properties.Settings.Default.updateVersion != latest.TagName)
                    {
                        Properties.Settings.Default.updateVersion = latest.TagName;
                        Properties.Settings.Default.reminderCount = 0;
                    }

                    if (Properties.Settings.Default.reminderCount < 3)
                    {
                        Properties.Settings.Default.reminderCount++;
                        Properties.Settings.Default.Save();

                        ToastNotificationManagerCompat.OnActivated += toastArgs =>
                        {
                            ToastArguments args = ToastArguments.Parse(toastArgs.Argument);
                            ValueSet userInput = toastArgs.UserInput;

                            if (args.ToString() == "action=update")
                            {
                                ToastNotificationManagerCompat.Uninstall();
                                ToastNotificationManagerCompat.History.Clear();

                                string path = Path.Combine(Path.GetTempPath(), "XBatteryStatus", "XBatteryStatus.msi");

                                if (!Directory.Exists(Path.GetDirectoryName(path)))
                                {
                                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                                }

                                if (File.Exists(path))
                                {
                                    File.Delete(path);
                                }

                                using (var client = new WebClient())
                                {
                                    client.DownloadFile(latest.Assets.Where(x => x.BrowserDownloadUrl.EndsWith(".msi")).First().BrowserDownloadUrl, path);
                                }

                                Process process = new Process();
                                process.StartInfo.FileName = "msiexec";
                                process.StartInfo.Arguments = " /i " + path + " /qr";
                                process.StartInfo.Verb = "runas";
                                process.Start();

                                Exit();
                            }
                        };

                        new ToastContentBuilder()
                        .AddText("XBatteryStatus")
                        .AddText("New Version Available on GitHub")
                        .AddButton(new ToastButton()
                                .SetContent("Download")
                                .SetProtocolActivation(new Uri(releaseUrl)))
                        .AddButton(new ToastButton()
                                .SetContent("Update")
                                .AddArgument("action", "update"))
                        .AddButton(new ToastButton()
                                .SetContent("Dismiss")
                                .SetDismissActivation())
                        .Show();
                    }
                }
                UpdateTimer.Stop();
            }
            catch (Exception e) { LogError(e); }
        }

        async private void FindBleController()
        {
            if (bluetoothRadio?.State == RadioState.On)
            {
                List<BluetoothLEDevice> foundGamepads = new List<BluetoothLEDevice>();

                foreach (var device in await DeviceInformation.FindAllAsync())
                {
                    try
                    {
                        BluetoothLEDevice bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);

                        if (bleDevice?.Appearance.SubCategory == BluetoothLEAppearanceSubcategories.Gamepad)//get the gamepads
                        {
                            GattDeviceService service = bleDevice.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb"));
                            GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).First();

                            if (service != null && characteristic != null)//get the gamepads with battery status
                            {
                                foundGamepads.Add(bleDevice);
                            }
                        }
                    }
                    catch (Exception e)
                    { //LogError(e);
                    }
                }

                var newGamepads = foundGamepads.Except(pairedGamepads).ToList();
                var removedGamepads = pairedGamepads.Except(foundGamepads).ToList();

                foreach (var gamepad in newGamepads)
                {
                    gamepad.ConnectionStatusChanged += ConnectionStatusChanged;
                }

                foreach (var gamepad in removedGamepads)
                {
                    if (gamepad != null)
                    {
                        gamepad.ConnectionStatusChanged -= ConnectionStatusChanged;
                    }
                }

                pairedGamepads = foundGamepads;

                if (pairedGamepads.Count == 0)
                {
                    notifyIcon.Icon = GetIcon(-1, "!");
                    notifyIcon.Text = "XBatteryStatus: No paired controller with battery service found";
                }
                else
                {
                    var connectedGamepads = pairedGamepads.Where(x => x.ConnectionStatus == BluetoothConnectionStatus.Connected).ToList();

                    if (connectedGamepads.Count == 0)
                    {
                        notifyIcon.Icon = GetIcon(-1, "!");
                        notifyIcon.Text = "XBatteryStatus: No controller is connected";
                    }
                    else
                    {
                        ConnectGamepad(connectedGamepads.First());
                    }
                }
            }
            else
            {
                notifyIcon.Icon = GetIcon(-1, "!");
                notifyIcon.Text = "XBatteryStatus: Bluetooth is turned off";
            }

            Update();
        }

        private void BluetoothRadio_StateChanged(Radio sender, object args)
        {
            FindBleController();
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                ConnectGamepad(sender);
            }
            else if (sender == connectedGamepad)
            {
                FindBleController();//another controller might be connected
            }
        }

        public void ConnectGamepad(BluetoothLEDevice device)
        {
            if (connectedGamepad == null || connectedGamepad.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                try
                {
                    GattDeviceService service = device.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb"));
                    GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).First();

                    if (service != null && characteristic != null)
                    {
                        connectedGamepad = device;
                        batteryCharacteristic = characteristic;
                        Update();
                    }
                }
                catch (Exception e) { LogError(e); }
            }
        }

        public void Update()
        {
            bool enabled = (bluetoothRadio?.State == RadioState.On && connectedGamepad?.ConnectionStatus == BluetoothConnectionStatus.Connected) || HideTimeoutTimer.Enabled;
            notifyIcon.Visible = Properties.Settings.Default.hide ? enabled : true;
            if (enabled)
            {
                ReadBattery();
            }
        }

        private async void ReadBattery()
        {
            if (connectedGamepad?.ConnectionStatus == BluetoothConnectionStatus.Connected && batteryCharacteristic != null)
            {
                GattReadResult result = await batteryCharacteristic.ReadValueAsync();

                if (result.Status == GattCommunicationStatus.Success)
                {
                    var reader = DataReader.FromBuffer(result.Value);
                    int val = reader.ReadByte();
                    string notify = val.ToString() + "% - " + connectedGamepad.Name;
                    notifyIcon.Text = "XBatteryStatus: " + notify;

                    notifyIcon.Icon = GetIcon(val);

                    if ((lastBattery > 15 && val <= 15) || (lastBattery > 10 && val <= 10) || (lastBattery > 5 && val <= 5))
                    {
                        new ToastContentBuilder().AddText("Low Battery").AddText(notify)
                            .Show();
                    }
                    lastBattery = val;
                }
            }
        }

        private void ExitClicked(object sender, EventArgs e)
        {
            Exit();
        }

        private void Exit()
        {
            notifyIcon.Visible = false;
            ToastNotificationManagerCompat.Uninstall();
            ToastNotificationManagerCompat.History.Clear();
            Application.Exit();
        }

        private void ThemeClicked(object sender, EventArgs e)
        {
            if (sender == themeButton.DropDownItems[1]) { Properties.Settings.Default.theme = 1; }
            else if (sender == themeButton.DropDownItems[2]) { Properties.Settings.Default.theme = 2; }
            else { Properties.Settings.Default.theme = 0; }
            Properties.Settings.Default.Save();
            Update();
            UpdateThemeButton();
        }

        private void UpdateThemeButton()
        {
            if (Properties.Settings.Default.theme == 1)
            {
                ((ToolStripMenuItem)themeButton.DropDownItems[0]).Checked = false;
                ((ToolStripMenuItem)themeButton.DropDownItems[1]).Checked = true;
                ((ToolStripMenuItem)themeButton.DropDownItems[2]).Checked = false;
            }
            else if (Properties.Settings.Default.theme == 2)
            {
                ((ToolStripMenuItem)themeButton.DropDownItems[0]).Checked = false;
                ((ToolStripMenuItem)themeButton.DropDownItems[1]).Checked = false;
                ((ToolStripMenuItem)themeButton.DropDownItems[2]).Checked = true;
            }
            else
            {
                ((ToolStripMenuItem)themeButton.DropDownItems[0]).Checked = true;
                ((ToolStripMenuItem)themeButton.DropDownItems[1]).Checked = false;
                ((ToolStripMenuItem)themeButton.DropDownItems[2]).Checked = false;
            }

            FindBleController();
        }

        private void HideClicked(object sender, EventArgs e)
        {
            Properties.Settings.Default.hide = !Properties.Settings.Default.hide;
            Properties.Settings.Default.Save();
            UpdateHideButton();
        }

        private void UpdateHideButton()
        {
            hideButton.Checked = Properties.Settings.Default.hide;

            Update();
        }

        private void HideTimeout()
        {
            HideTimeoutTimer.Stop();
            Update();
        }

        private void NumbersClicked(object sender, EventArgs e)
        {
            Properties.Settings.Default.numbers = !Properties.Settings.Default.numbers;
            Properties.Settings.Default.Save();
            UpdateNumbersButton();
        }

        private void UpdateNumbersButton()
        {
            numbersButton.Checked = Properties.Settings.Default.numbers;
            Update();
        }

        public bool IsLightMode()
        {
            RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");

            if (key != null)
            {
                object registryValueObject = key.GetValue("AppsUseLightTheme");

                if (registryValueObject != null)
                {
                    int registryValue = (int)registryValueObject;
                    return registryValue == 1;
                }
            }

            return true;
        }

        public Icon GetIcon(int val, string s = "")
        {
            var icon = Properties.Resources.icon00;

            if (val >= 0)
            {
                if (Properties.Settings.Default.numbers)
                {
                    if (val >= 100) val = 99;

                    AddDigit(icon, DigitToBitmap(val / 10), false);
                    AddDigit(icon, DigitToBitmap(val % 10), true);
                }
                else
                {
                    AddPercentage(icon, val);
                }
            }
            else
            {
                if (s == "!")
                {
                    AddSymbol(icon, Properties.Resources.symbolE);
                }
                else if (s == "?")
                {
                    AddSymbol(icon, Properties.Resources.symbolQ);
                }
            }

            if ((Properties.Settings.Default.theme == 0 && !lightMode) || Properties.Settings.Default.theme == 1)
            {
                IntPtr Hicon = icon.GetHicon();
                return Icon.FromHandle(Hicon);
            }
            else
            {
                IntPtr Hicon = InvertBitmap(icon).GetHicon();
                return Icon.FromHandle(Hicon);
            }
        }

        public Bitmap DigitToBitmap(int digit)
        {
            return digit switch
            {
                0 => Properties.Resources.number0,
                1 => Properties.Resources.number1,
                2 => Properties.Resources.number2,
                3 => Properties.Resources.number3,
                4 => Properties.Resources.number4,
                5 => Properties.Resources.number5,
                6 => Properties.Resources.number6,
                7 => Properties.Resources.number7,
                8 => Properties.Resources.number8,
                9 => Properties.Resources.number9,
                _ => Properties.Resources.number0
            };
        }

        public Bitmap AddDigit(Bitmap bitmap, Bitmap number, bool bottom)
        {
            int x_start = 21;
            int y_start = bottom ? 17 : 6;

            for (int y = 0; y < number.Height; y++)
            {
                for (int x = 0; x < number.Width; x++)
                {
                    Color pixelColor = number.GetPixel(x, y);
                    if (pixelColor.A > 0)
                    {
                        bitmap.SetPixel(x + x_start, y + y_start, pixelColor);
                    }
                }
            }

            return bitmap;
        }

        public Bitmap AddPercentage(Bitmap bitmap, int val)
        {
            int y_start = 7 + (int)((100 - val) / 5.0 + 0.5);

            for (int y = y_start; y < 27; y++)
            {
                for (int x = 20; x < 28; x++)
                {
                    Color pixelColor = Color.FromArgb(255, 255, 255, 255);
                    if (pixelColor.A > 0)
                    {
                        bitmap.SetPixel(x, y, pixelColor);
                    }
                }
            }

            return bitmap;
        }

        public Bitmap AddSymbol(Bitmap bitmap, Bitmap symbol)
        {
            int x_start = 19;
            int y_start = 6;

            for (int y = 0; y < symbol.Height; y++)
            {
                for (int x = 0; x < symbol.Width; x++)
                {
                    Color pixelColor = symbol.GetPixel(x, y);
                    if (pixelColor.A > 0)
                    {
                        bitmap.SetPixel(x + x_start, y + y_start, pixelColor);
                    }
                }
            }

            return bitmap;
        }

        public Bitmap InvertBitmap(Bitmap bitmap)
        {
            for (int y = 0; y < bitmap.Height; y++)
            {
                for (int x = 0; x < bitmap.Width; x++)
                {
                    Color pixelColor = bitmap.GetPixel(x, y);
                    Color invertedColor = Color.FromArgb(pixelColor.A, 255 - pixelColor.R, 255 - pixelColor.G, 255 - pixelColor.B);
                    bitmap.SetPixel(x, y, invertedColor);
                }
            }
            return bitmap;
        }

        private void VersionClicked(object sender, EventArgs e)
        {
            Process.Start(new ProcessStartInfo(releaseUrl) { UseShellExecute = true });
        }

        private void Log(string s)
        {
#if DEBUG
            Console.WriteLine(s);
#endif
        }

        private void LogError(Exception e)
        {
#if DEBUG
            Log(e.StackTrace);
            Log(e.Message);
            Log("");
#endif
        }
    }
}
