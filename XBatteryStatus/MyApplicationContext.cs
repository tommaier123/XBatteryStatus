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
using System.Threading.Tasks;
using System.Windows.Forms;
using Windows.ApplicationModel;
using Windows.Foundation.Collections;

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

        private BleBattery ble;
        private XInputBattery xinput;
        private System.Threading.SynchronizationContext uiContext;
        private bool bleInitialized;

        private int lastBattery = 100;
        private string lastName;
        private string lastStatusLog;
        private bool updating;

        private bool lightMode = false;
        private readonly string logFilePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "XBatteryStatus", "log.txt");
        private readonly System.Threading.SemaphoreSlim logSemaphore = new System.Threading.SemaphoreSlim(1, 1);

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

            Log($"XBatteryStatus V{versionString} started");

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

            uiContext = System.Threading.SynchronizationContext.Current ?? new WindowsFormsSynchronizationContext();

            ble = new BleBattery(LogError);
            xinput = new XInputBattery(Log);

            ble.Changed += OnBleChanged;

            Discover();

            UpdateTimer = new Timer();
            UpdateTimer.Tick += new EventHandler((x, y) => Update());
            UpdateTimer.Interval = 10000;
            UpdateTimer.Start();

            DiscoverTimer = new Timer();
            DiscoverTimer.Tick += new EventHandler((x, y) => Discover());
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

                            if (args.ToString() == "action=dismiss")
                            {
                                Properties.Settings.Default.reminderCount++;
                                Properties.Settings.Default.Save();
                                return;
                            }

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

                        ToastNotificationManagerCompat.History.Clear();

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
                                .AddArgument("action", "dismiss"))
                        .Show();
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

        private void OnBleChanged(bool rediscover)
        {
            uiContext.Post(x =>
            {
                if (rediscover)
                {
                    Discover();
                }
                else
                {
                    Update();
                }
            }, null);
        }

        private void Discover()
        {
            _ = DiscoverAsync();
        }

        private async Task DiscoverAsync()
        {
            try
            {
                if (!bleInitialized)
                {
                    bleInitialized = true;
                    await ble.InitAsync();
                }

                await ble.DiscoverAsync();
            }
            catch (Exception e) { LogError(e); }

            await UpdateAsync();
        }

        public void Update()
        {
            _ = UpdateAsync();
        }

        private async Task UpdateAsync()
        {
            if (ble == null || updating)
            {
                return;
            }

            updating = true;

            try
            {
                BatteryInfo? info = await ble.ReadAsync();

                if (!info.HasValue)
                {
                    if (ble.Connected)
                    {
                        return;
                    }

                    info = xinput.Read();
                }

                if (info.HasValue)
                {
                    Show(info.Value);
                }
                else
                {
                    ShowUnavailable();
                }

                if (notifyIcon.Text != lastStatusLog)
                {
                    lastStatusLog = notifyIcon.Text;
                    Log(lastStatusLog);
                }
            }
            catch (Exception e) { LogError(e); }
            finally { updating = false; }
        }

        private void Show(BatteryInfo info)
        {
            notifyIcon.Visible = true;

            if (info.Name != lastName)
            {
                lastName = info.Name;
                lastBattery = 100;
            }

            if (info.Waiting)
            {
                SetIcon(-1, "?");
                notifyIcon.Text = "XBatteryStatus: Waiting - " + info.Name;
                lastBattery = 100;
                return;
            }

            if (info.Wired && info.Level == null)
            {
                SetIcon(-1, "w");
                notifyIcon.Text = "XBatteryStatus: Wired - " + info.Name;
                lastBattery = 100;
                return;
            }

            string level = info.Level ?? info.Percent + "%";
            string notify = level + " - " + info.Name;

            notifyIcon.Text = "XBatteryStatus: " + notify;
            SetIcon(info.Percent, "", info.Level != null);

            if (info.Wired)
            {
                lastBattery = 100;
                return;
            }

            bool low = info.Level == null
                ? (lastBattery > 15 && info.Percent <= 15) || (lastBattery > 10 && info.Percent <= 10) || (lastBattery > 5 && info.Percent <= 5)
                : info.Percent <= 40 && info.Percent < lastBattery;

            if (low)
            {
                new ToastContentBuilder().AddText("Low Battery").AddText(notify).Show();
            }

            lastBattery = info.Percent;
        }

        private void ShowUnavailable()
        {
            notifyIcon.Visible = Properties.Settings.Default.hide ? HideTimeoutTimer.Enabled : true;
            SetIcon(-1, "!");
            notifyIcon.Text = "XBatteryStatus: " + (ble.Status ?? "No controller is connected");
        }

        private void ExitClicked(object sender, EventArgs e)
        {
            Exit();
        }

        protected override void ExitThreadCore()
        {
            Exit();
            base.ExitThreadCore();
        }

        private void Exit()
        {
            if (ble != null)
            {
                ble.Changed -= OnBleChanged;
                ble.Dispose();
            }

            if (notifyIcon.Icon != null)
            {
                DestroyIcon(notifyIcon.Icon.Handle);
                notifyIcon.Icon.Dispose();
                notifyIcon.Icon = null;
            }

            notifyIcon.Visible = false;

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

            Update();
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

        public void SetIcon(int val, string s = "", bool graphical = false)
        {
            Icon oldIcon = notifyIcon.Icon;
            IntPtr oldHandle = IntPtr.Zero;
            
            if (oldIcon != null)
            {
                oldHandle = oldIcon.Handle;
            }
            
            notifyIcon.Icon = GetIcon(val, s, graphical);
            
            if (oldIcon != null)
            {
                oldIcon.Dispose();
                if (oldHandle != IntPtr.Zero)
                {
                    DestroyIcon(oldHandle);
                }
            }
        }

        public Icon GetIcon(int val, string s = "", bool graphical = false)
        {
            using (var icon = (Bitmap)Properties.Resources.icon00.Clone())
            {
                try
                {
                    if (val >= 0)
                    {
                        if (Properties.Settings.Default.numbers && !graphical)
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
                        else if (s == "w")
                        {
                            AddSymbol(icon, Properties.Resources.symbolW);
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

        private async void Log(string s)
        {
#if DEBUG
            Console.WriteLine(s);
#else
            await logSemaphore.WaitAsync();
            try
            {
                string logDirectory = Path.GetDirectoryName(logFilePath);
                if (!Directory.Exists(logDirectory))
                {
                    Directory.CreateDirectory(logDirectory);
                }

                string logEntry = $"[{DateTime.Now:dd.MM.yyyy HH:mm:ss}] {s}";
                await File.AppendAllTextAsync(logFilePath, logEntry + Environment.NewLine);
            }
            catch
            {
            }
            finally
            {
                logSemaphore.Release();
            }
#endif
        }

        private void LogError(Exception e)
        {
            Log(e.StackTrace);
            Log(e.Message);
            Log("");
        }
    }
}
