using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Devices.Enumeration;
using Windows.Devices.Radios;
using Windows.Storage.Streams;

namespace XBatteryStatus
{
    internal class BleBattery : IDisposable
    {
        private static readonly Guid serviceUuid = new Guid("0000180f-0000-1000-8000-00805f9b34fb");
        private static readonly Guid characteristicUuid = new Guid("00002a19-0000-1000-8000-00805f9b34fb");

        private readonly Action<Exception> logError;

        private List<BluetoothLEDevice> pairedGamepads = new List<BluetoothLEDevice>();
        private BluetoothLEDevice connectedGamepad;
        private GattDeviceService batteryService;
        private GattCharacteristic batteryCharacteristic;
        private Radio bluetoothRadio;
        private bool discovering;

        public string Status { get; private set; } = "Looking for paired controller";

        public event Action<bool> Changed;

        public bool Connected
        {
            get
            {
                try
                {
                    return connectedGamepad?.ConnectionStatus == BluetoothConnectionStatus.Connected && batteryCharacteristic != null;
                }
                catch (ObjectDisposedException)
                {
                    return false;
                }
            }
        }

        public BleBattery(Action<Exception> logError)
        {
            this.logError = logError;
        }

        public async Task InitAsync()
        {
            try
            {
                var radios = await Radio.GetRadiosAsync();
                bluetoothRadio = radios.FirstOrDefault(radio => radio.Kind == RadioKind.Bluetooth);
                if (bluetoothRadio != null)
                {
                    bluetoothRadio.StateChanged += (x, y) => Changed?.Invoke(true);
                }
            }
            catch (Exception e)
            {
                logError(e);
            }
        }

        public async Task DiscoverAsync()
        {
            if (bluetoothRadio?.State != RadioState.On)
            {
                Status = "Bluetooth is turned off";
                return;
            }

            if (discovering)
            {
                return;
            }

            discovering = true;

            try
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
                            using (GattDeviceService service = bleDevice.GetGattService(serviceUuid))
                            {
                                if (service != null)
                                {
                                    GattCharacteristic characteristic = service.GetCharacteristics(characteristicUuid).FirstOrDefault();

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

                var foundIds = foundGamepads.Select(x => x.DeviceId).ToHashSet();
                var knownIds = pairedGamepads.Select(x => x.DeviceId).ToHashSet();

                foreach (var gamepad in foundGamepads.Where(x => knownIds.Contains(x.DeviceId)))
                {
                    gamepad.Dispose();
                }

                var newGamepads = foundGamepads.Where(x => !knownIds.Contains(x.DeviceId)).ToList();
                var removedGamepads = pairedGamepads.Where(x => !foundIds.Contains(x.DeviceId)).ToList();

                foreach (var gamepad in newGamepads)
                {
                    gamepad.ConnectionStatusChanged += ConnectionStatusChanged;
                }

                foreach (var gamepad in removedGamepads)
                {
                    if (gamepad != null)
                    {
                        if (gamepad == connectedGamepad)
                        {
                            Release();
                        }

                        gamepad.ConnectionStatusChanged -= ConnectionStatusChanged;
                        gamepad.Dispose();
                    }
                }

                pairedGamepads = pairedGamepads.Where(x => foundIds.Contains(x.DeviceId)).Concat(newGamepads).ToList();

                if (pairedGamepads.Count == 0)
                {
                    Status = "No paired controller with battery service found";
                    return;
                }

                var connectedGamepads = pairedGamepads.Where(x => x.ConnectionStatus == BluetoothConnectionStatus.Connected).ToList();

                if (connectedGamepads.Count == 0)
                {
                    Status = "No controller is connected";
                    return;
                }

                Connect(connectedGamepads.First());
            }
            finally
            {
                discovering = false;
            }
        }

        private void ConnectionStatusChanged(BluetoothLEDevice sender, object args)
        {
            if (sender.ConnectionStatus == BluetoothConnectionStatus.Connected)
            {
                Connect(sender);
            }
            else if (sender == connectedGamepad)
            {
                Release();

                var other = pairedGamepads.FirstOrDefault(x => x != sender && x.ConnectionStatus == BluetoothConnectionStatus.Connected);

                if (other != null)
                {
                    Connect(other);
                }
                else
                {
                    Status = "No controller is connected";
                }
            }

            Changed?.Invoke(false);
        }

        private void Release()
        {
            batteryService?.Dispose();
            batteryService = null;
            batteryCharacteristic = null;
            connectedGamepad = null;
        }

        private void Connect(BluetoothLEDevice device)
        {
            try
            {
                if (connectedGamepad != null && connectedGamepad.ConnectionStatus != BluetoothConnectionStatus.Disconnected)
                {
                    return;
                }
            }
            catch (ObjectDisposedException)
            {
                Release();
            }

            try
            {
                GattDeviceService service = device.GetGattService(serviceUuid);
                if (service == null)
                {
                    return;
                }

                GattCharacteristic characteristic = service.GetCharacteristics(characteristicUuid).FirstOrDefault();
                if (characteristic == null)
                {
                    service.Dispose();
                    return;
                }

                batteryService?.Dispose();

                connectedGamepad = device;
                batteryService = service;
                batteryCharacteristic = characteristic;
                Status = null;
            }
            catch (Exception e)
            {
                logError(e);
            }
        }

        public async Task<BatteryInfo?> ReadAsync()
        {
            try
            {
                BluetoothLEDevice gamepad = connectedGamepad;
                GattCharacteristic characteristic = batteryCharacteristic;

                if (bluetoothRadio?.State == RadioState.On
                    && gamepad?.ConnectionStatus == BluetoothConnectionStatus.Connected
                    && characteristic != null)
                {
                    GattReadResult result = await characteristic.ReadValueAsync();

                    if (result.Status == GattCommunicationStatus.Success)
                    {
                        int val = DataReader.FromBuffer(result.Value).ReadByte();
                        Status = null;
                        return new BatteryInfo(val, null, false, gamepad.Name);
                    }
                }
            }
            catch (ObjectDisposedException)
            {
            }
            catch (Exception e)
            {
                logError(e);
            }

            return null;
        }

        public void Dispose()
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
            Release();
        }
    }
}
