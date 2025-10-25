using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Foundation.Collections;
using Windows.Storage.Streams;
using Windows.UI.Notifications;

namespace XBatteryStatus
{
    public class MyApplicationContext : ApplicationContext
    {
        private NuGetVersion version;
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
            if (Properties.Settings.Default.newInstall)
            {
                Properties.Settings.Default.Upgrade();
                Properties.Settings.Default.newInstall = false;
                Properties.Settings.Default.Save();
            }

            string versionString = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "0.0.0";
            version = NuGetVersion.Parse(versionString);


            HideTimeoutTimer = new Timer();
            HideTimeoutTimer.Tick += new EventHandler((x, y) => HideTimeout());
            HideTimeoutTimer.Interval = 10000;
            HideTimeoutTimer.Start();


            if (!IsStoreInstall())
            {
                SoftwareUpdateTimer = new Timer();
                SoftwareUpdateTimer.Tick += new EventHandler((x, y) => { CheckSoftwareUpdate(); });
                SoftwareUpdateTimer.Interval = 30000;
                SoftwareUpdateTimer.Start();
            }

            lightMode = IsLightMode();
            SetIcon(-1, "?");
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

            ToolStripMenuItem versionButton = new ToolStripMenuItem("V" + versionString, null, new EventHandler(VersionClicked));
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
                var latest = all.OrderByDescending(x => NuGetVersion.Parse(x.TagName.Substring(1))).FirstOrDefault();
                if (latest != null && NuGetVersion.Parse(latest.TagName.Substring(1)) > version)
                {
                    if (Properties.Settings.Default.updateVersion != latest.TagName)
                    {
                        Properties.Settings.Default.updateVersion = latest.TagName;
                        Properties.Settings.Default.reminderCount = 0;
                    }

                    if (Properties.Settings.Default.reminderCount < 3)
                    {
                        ToastNotificationManagerCompat.OnActivated += async toastArgs =>
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

                                using (var httpClient = new HttpClient())
                                {
                                    var msiAsset = latest.Assets.FirstOrDefault(x => x.BrowserDownloadUrl.EndsWith(".msi"));
                                    if (msiAsset != null)
                                    {
                                        var response = await httpClient.GetAsync(msiAsset.BrowserDownloadUrl);
                                        response.EnsureSuccessStatusCode();

                                        await using var fileStream = new FileStream(path, FileMode.Create);
                                        await response.Content.CopyToAsync(fileStream);
                                    }
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
                        .Show(toast =>
                        toast.Dismissed += (sender, args) =>
                        {
                            if (args.Reason == ToastDismissalReason.UserCanceled)
                            {
                                Properties.Settings.Default.reminderCount++;
                                Properties.Settings.Default.Save();
                                Log("Dismissed");
                            }
                        });


                    }
                }
                SoftwareUpdateTimer.Stop();
            }
            catch (Exception e)
            {
                SoftwareUpdateTimer.Interval = 90 * 60000;
                LogError(e);
            }
        }

        async private void FindBleController()
        {
            if (bluetoothRadio?.State == RadioState.On)
            {
                List<BluetoothLEDevice> foundGamepads = new List<BluetoothLEDevice>();

                foreach (var device in await DeviceInformation.FindAllAsync())
                {
                    BluetoothLEDevice bleDevice = null;
                    bool keepDevice = false;

                    try
                    {
                        bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);

                        if (bleDevice?.Appearance.SubCategory == BluetoothLEAppearanceSubcategories.Gamepad) //get the gamepads
                        {
                            using (GattDeviceService service = bleDevice.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb")))
                            {
                                if (service != null)
                                {
                                    GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).FirstOrDefault();

                                    if (characteristic != null) //get the gamepads with battery status
                                    {
                                        foundGamepads.Add(bleDevice);
                                        keepDevice = true; //don't dispose this device
                                    }
                                }
                            }
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        if (!keepDevice && bleDevice != null)
                        {
                            bleDevice.Dispose();
                        }
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
                        gamepad.Dispose();
                    }
                }

                pairedGamepads = foundGamepads;

                if (pairedGamepads.Count == 0)
                {
                    SetIcon(-1, "!");
                    notifyIcon.Text = "XBatteryStatus: No paired controller with battery service found";
                }
                else
                {
                    var connectedGamepads = pairedGamepads.Where(x => x.ConnectionStatus == BluetoothConnectionStatus.Connected).ToList();

                    if (connectedGamepads.Count == 0)
                    {
                        SetIcon(-1, "!");
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
                SetIcon(-1, "!");
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
                FindBleController(); //another controller might be connected
            }
        }

        public void ConnectGamepad(BluetoothLEDevice device)
        {
            if (connectedGamepad == null || connectedGamepad.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                try
                {
                    using (GattDeviceService service = device.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb")))
                    {
                        if (service != null)
                        {
                            GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).FirstOrDefault();

                            if (characteristic != null)
                            {
                                connectedGamepad = device;
                                batteryCharacteristic = characteristic;
                                Update();
                            }
                        }
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
            try
            {
                if (connectedGamepad?.ConnectionStatus == BluetoothConnectionStatus.Connected && batteryCharacteristic != null)
                {
                    GattReadResult result = await batteryCharacteristic.ReadValueAsync();

                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        var reader = DataReader.FromBuffer(result.Value);
                        int val = reader.ReadByte();
                        string notify = val.ToString() + "% - " + connectedGamepad?.Name;
                        notifyIcon.Text = "XBatteryStatus: " + notify;

                        SetIcon(val);

                        if ((lastBattery > 15 && val <= 15) || (lastBattery > 10 && val <= 10) || (lastBattery > 5 && val <= 5))
                        {
                            new ToastContentBuilder().AddText("Low Battery").AddText(notify)
                                .Show();
                        }
                        lastBattery = val;
                    }
                }
            }
            catch (Exception e) { LogError(e); }
        }

        private void ExitClicked(object sender, EventArgs e)
        {
            Exit();
        }

        private void Exit()
        {
            foreach (var gamepad in pairedGamepads)
            {
                if (gamepad != null)
                {
                    gamepad.ConnectionStatusChanged -= ConnectionStatusChanged;
                    gamepad.Dispose();
                }
            }
            pairedGamepads.Clear();
            connectedGamepad = null;

            if (notifyIcon.Icon != null)
            {
                DestroyIcon(notifyIcon.Icon.Handle);
                notifyIcon.Icon.Dispose();
                notifyIcon.Icon = null;
            }

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

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = CharSet.Auto)]
        extern static bool DestroyIcon(IntPtr handle);

        public void SetIcon(int val, string s = "")
        {
            if (notifyIcon.Icon != null)
            {
                IntPtr oldHandle = notifyIcon.Icon.Handle;
                notifyIcon.Icon.Dispose();
                DestroyIcon(oldHandle);
            }
            notifyIcon.Icon = GetIcon(val, s);
        }

        public Icon GetIcon(int val, string s = "")
        {
            using (var icon = (Bitmap)Properties.Resources.icon00.Clone())
            {
                try
                {
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

                    if (!((Properties.Settings.Default.theme == 0 && !lightMode) || Properties.Settings.Default.theme == 1))
                    {
                        InvertBitmap(icon);
                    }

                    IntPtr hIcon = icon.GetHicon();
                    try
                    {
                        return (Icon)Icon.FromHandle(hIcon).Clone();
                    }
                    finally
                    {
                        DestroyIcon(hIcon);
                    }
                }
                catch
                {
                    return (Icon)SystemIcons.Application.Clone(); //return a fallback icon
                }
            }
        }

        public Bitmap DigitToBitmap(int digit)
        {
            return (digit switch
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
            });
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

        public static bool IsStoreInstall()
        {
            try
            {
                var package = Package.Current;
                return true;
            }
            catch
            {
                return false;
            }
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
