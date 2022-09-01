using Microsoft.Toolkit.Uwp.Notifications;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
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

        private Timer timer1;
        private ContextMenuStrip contextMenu;

        public BluetoothLEDevice pairedGamepad;
        public GattCharacteristic batteryCharacteristic;

        private int lastBattery = 100;

        public MyApplicationContext()
        {
            notifyIcon.Icon = Properties.Resources.iconQ;
            notifyIcon.Text = "XBatteryStatus: Looking for paired controller";
            notifyIcon.Visible = true;

            contextMenu = new ContextMenuStrip();
            contextMenu.ShowImageMargin = false;
            contextMenu.ShowCheckMargin = false;
            contextMenu.ShowItemToolTips = false;
            ToolStripButton settingsButton = new ToolStripButton("Settings", null, new EventHandler(SettingsClicked), "Settings");
            settingsButton.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            settingsButton.AutoSize = true;
            settingsButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            settingsButton.Margin = Padding.Empty;
            contextMenu.Items.Add(settingsButton);
            ToolStripButton exitButton = new ToolStripButton("Exit", null, new EventHandler(ExitClicked), "Exit");
            exitButton.Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
            exitButton.AutoSize = true;
            exitButton.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            exitButton.Margin = Padding.Empty;
            contextMenu.Items.Add(exitButton);

            contextMenu.Dock = DockStyle.Top;

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
                notifyIcon.Icon = Properties.Resources.iconE;
                notifyIcon.Text = "XBatteryStatus: No paired controller with battery service found";
            }
            else
            {
                Update();
            }
        }

        private async void ReadBattery()
        {

            var settings = Properties.Settings.Default;

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
                        if (val < 5) notifyIcon.Icon = Properties.Resources.icon00;
                        else if (val < 15) notifyIcon.Icon = Properties.Resources.icon10;
                        else if (val < 25) notifyIcon.Icon = Properties.Resources.icon20;
                        else if (val < 35) notifyIcon.Icon = Properties.Resources.icon30;
                        else if (val < 45) notifyIcon.Icon = Properties.Resources.icon40;
                        else if (val < 55) notifyIcon.Icon = Properties.Resources.icon50;
                        else if (val < 65) notifyIcon.Icon = Properties.Resources.icon60;
                        else if (val < 75) notifyIcon.Icon = Properties.Resources.icon70;
                        else if (val < 85) notifyIcon.Icon = Properties.Resources.icon80;
                        else if (val < 95) notifyIcon.Icon = Properties.Resources.icon90;
                        else notifyIcon.Icon = Properties.Resources.icon100;

                        if (settings.EnableLowBatteryNotifications &&
                            (lastBattery > 15 && val <= 15) || (lastBattery > 10 && val <= 10) || (lastBattery > 5 && val <= 5))
                        {
                            ToastContentBuilder builder = new ToastContentBuilder()
                                .AddText("Low Battery")
                                .AddText(notify);

                            if (settings.EnableAudioNotifications) 
                            {
                                builder.AddAudio(new ToastAudio() 
                                {
                                    Src = new Uri(settings.LowBatteryAudio),
                                    Loop = false
                                });
                            }
                            builder.Show();
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

        private void ExitClicked(object sender, EventArgs e) 
        {
            Application.Exit();
        }
        private void SettingsClicked(object sender, EventArgs e) 
        {
            if (new SettingsForm().ShowDialog() == DialogResult.OK)
            {
                timer1.Interval = Properties.Settings.Default.UpdateFrequency;
            }
        }

        private void OnApplicationExit(object sender, EventArgs e)
        {
            notifyIcon.Visible = false;
        }

        public void Update()
        {
            notifyIcon.Visible = pairedGamepad != null && pairedGamepad.ConnectionStatus == BluetoothConnectionStatus.Connected;
            ReadBattery();
        }
    }
}
