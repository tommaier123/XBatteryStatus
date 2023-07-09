using Microsoft.Toolkit.Uwp.Notifications;
using Microsoft.Win32;
using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Storage.Streams;

namespace XBatteryStatus
{
    public class MyApplicationContext : ApplicationContext
    {
        NotifyIcon notifyIcon = new NotifyIcon();
        private ContextMenuStrip contextMenu;

        private Timer timer1;

        public BluetoothLEDevice pairedGamepad;
        public GattCharacteristic batteryCharacteristic;

        private int lastBattery = 100;

        private bool lightMode = false;

        public MyApplicationContext()
        {
            lightMode = IsLightMode();
            notifyIcon.Icon = GetIcon(Properties.Resources.iconQ, lightMode);
            notifyIcon.Text = "XBatteryStatus: Looking for paired controller";
            notifyIcon.Visible = true;

            contextMenu = new ContextMenuStrip();
            ToolStripMenuItem exitButton = new ToolStripMenuItem("Exit", null, new EventHandler(ExitClicked), "Exit");
            contextMenu.Items.Add(exitButton);
            notifyIcon.ContextMenuStrip = contextMenu;


            FindBleController();

            timer1 = new Timer();
            timer1.Tick += new EventHandler(timer1_Tick);
            timer1.Interval = 10000;
            timer1.Start();
        }

        async private void FindBleController()
        {
            int count = 0;
            foreach (var device in await DeviceInformation.FindAllAsync())
            {
                try
                {
                    BluetoothLEDevice bleDevice = await BluetoothLEDevice.FromIdAsync(device.Id);

                    if (bleDevice != null && bleDevice.Appearance.SubCategory == BluetoothLEAppearanceSubcategories.Gamepad)//get the gamepads
                    {
                        GattDeviceService service = bleDevice.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb"));
                        GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).First();

                        if (service != null && characteristic != null)//get the gamepads with battery status
                        {
                            bleDevice.ConnectionStatusChanged += ConnectionStatusChanged;
                            count++;
                            if (bleDevice.ConnectionStatus == BluetoothConnectionStatus.Connected)
                            {
                                ConnectGamepad(bleDevice);
                            }
                        }
                    }
                }
                catch { }
            }

            if (count == 0)
            {
                notifyIcon.Icon = GetIcon(Properties.Resources.iconE, lightMode);
                notifyIcon.Text = "XBatteryStatus: No paired controller with battery service found";
            }
            else
            {
                Update();
            }
        }

        private async void ReadBattery()
        {
            if (pairedGamepad != null && batteryCharacteristic != null)
            {
                if (pairedGamepad.ConnectionStatus == BluetoothConnectionStatus.Connected)
                {
                    GattReadResult result = await batteryCharacteristic.ReadValueAsync();

                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        var reader = DataReader.FromBuffer(result.Value);
                        int val = reader.ReadByte();
                        string notify = val.ToString() + "% - " + pairedGamepad.Name;
                        notifyIcon.Text = "XBatteryStatus: " + notify;
                        Icon icon = Properties.Resources.icon100;
                        if (val < 5) icon = Properties.Resources.icon00;
                        else if (val < 15) icon = Properties.Resources.icon10;
                        else if (val < 25) icon = Properties.Resources.icon20;
                        else if (val < 35) icon = Properties.Resources.icon30;
                        else if (val < 45) icon = Properties.Resources.icon40;
                        else if (val < 55) icon = Properties.Resources.icon50;
                        else if (val < 65) icon = Properties.Resources.icon60;
                        else if (val < 75) icon = Properties.Resources.icon70;
                        else if (val < 85) icon = Properties.Resources.icon80;
                        else if (val < 95) icon = Properties.Resources.icon90;

                        notifyIcon.Icon = GetIcon(icon, lightMode);

                        if ((lastBattery > 15 && val <= 15) || (lastBattery > 10 && val <= 10) || (lastBattery > 5 && val <= 5))
                        {
                            new ToastContentBuilder().AddText("Low Battery").AddText(notify)
                                .Show();
                        }
                        lastBattery = val;
                    }
                }
            }
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            ConnectGamepad(sender);
            Update();
        }

        public void ConnectGamepad(BluetoothLEDevice device)
        {
            if (pairedGamepad == null || pairedGamepad.ConnectionStatus == BluetoothConnectionStatus.Disconnected)
            {
                try
                {
                    GattDeviceService service = device.GetGattService(new Guid("0000180f-0000-1000-8000-00805f9b34fb"));
                    GattCharacteristic characteristic = service.GetCharacteristics(new Guid("00002a19-0000-1000-8000-00805f9b34fb")).First();

                    if (service != null && characteristic != null)
                    {
                        pairedGamepad = device;
                        batteryCharacteristic = characteristic;
                    }
                }
                catch { }
            }
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            ReadBattery();
        }

        public void Update()
        {
            notifyIcon.Visible = pairedGamepad != null && pairedGamepad.ConnectionStatus == BluetoothConnectionStatus.Connected;
            ReadBattery();
        }

        private void ExitClicked(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
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

        public Icon GetIcon(Icon darkModeIcon, bool lightMode)
        {
            if (!lightMode)
            {
                return darkModeIcon;
            }
            else
            {
                using (Bitmap bitmap = darkModeIcon.ToBitmap())
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

                    IntPtr Hicon = bitmap.GetHicon();
                    return Icon.FromHandle(Hicon);
                }
            }
        }
    }
}
