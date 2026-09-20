using System;
using System.Runtime.InteropServices;
using System.Text;

namespace XBatteryStatus
{
    internal class XInputBattery
    {
        private const int ERROR_SUCCESS = 0;
        private const byte BATTERY_DEVTYPE_GAMEPAD = 0x00;
        private const uint XINPUT_FLAG_GAMEPAD = 0x00000001;
        private const ushort XINPUT_CAPS_WIRELESS = 0x0002;
        private const byte BATTERY_TYPE_DISCONNECTED = 0x00;
        private const byte BATTERY_TYPE_WIRED = 0x01;
        private const byte BATTERY_LEVEL_EMPTY = 0x00;

        private readonly Action<string> log;

        private bool available = true;
        private string lastReport;

        public XInputBattery(Action<string> log)
        {
            this.log = log;
        }

        public BatteryInfo? Read()
        {
            if (!available)
            {
                return null;
            }

            StringBuilder report = new StringBuilder();
            BatteryInfo? result = null;
            int candidate = -1;
            bool candidateWireless = false;

            for (uint i = 0; i < 4; i++)
            {
                XINPUT_BATTERY_INFORMATION info;
                uint batteryResult;

                try
                {
                    batteryResult = XInputGetBatteryInformation(i, BATTERY_DEVTYPE_GAMEPAD, out info);
                }
                catch (Exception e)
                {
                    available = false;
                    log("xinput unavailable: " + e.Message);
                    return null;
                }

                if (batteryResult != ERROR_SUCCESS)
                {
                    report.Append($"[{i} empty rc={batteryResult}] ");
                    continue;
                }

                bool hasLevel = info.BatteryType != BATTERY_TYPE_DISCONNECTED
                    && info.BatteryType != BATTERY_TYPE_WIRED
                    && info.BatteryLevel != BATTERY_LEVEL_EMPTY;

                XINPUT_CAPABILITIES caps;
                uint capsResult = XInputGetCapabilities(i, XINPUT_FLAG_GAMEPAD, out caps);
                bool wireless = capsResult == ERROR_SUCCESS && (caps.Flags & XINPUT_CAPS_WIRELESS) != 0;

                report.Append($"[{i} type={info.BatteryType} level={info.BatteryLevel} hasLevel={hasLevel} capsRc={capsResult} devType={caps.Type} subType={caps.SubType} flags=0x{caps.Flags:X4} wireless={wireless}] ");

                if (hasLevel)
                {
                    if (result == null)
                    {
                        result = new BatteryInfo(LevelToPercent(info.BatteryLevel), LevelToName(info.BatteryLevel), !wireless, "Controller " + (i + 1));
                    }
                }
                else if (candidate < 0)
                {
                    candidate = (int)i;
                    candidateWireless = wireless;
                }
            }

            //a wireless controller can't run without a battery, no level means not reported yet
            if (result == null && candidate >= 0)
            {
                result = new BatteryInfo(0, null, !candidateWireless, "Controller " + (candidate + 1), candidateWireless);
            }

            report.Append("-> " + (result.HasValue ? (result.Value.Waiting ? "waiting" : result.Value.Level ?? "wired") + " \"" + result.Value.Name + "\"" : "nothing"));

            string line = report.ToString();
            if (line != lastReport)
            {
                lastReport = line;
                log("xinput: " + line);
            }

            return result;
        }

        private static string LevelToName(byte level)
        {
            switch (level)
            {
                case 0: return "Empty";
                case 1: return "Low";
                case 2: return "Medium";
                case 3: return "Full";
                default: return "Unknown";
            }
        }

        private static int LevelToPercent(byte level)
        {
            switch (level)
            {
                case 0: return 10;  //EMPTY
                case 1: return 40;  //LOW
                case 2: return 70;  //MEDIUM
                case 3: return 100; //FULL
                default: return 10;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_BATTERY_INFORMATION
        {
            public byte BatteryType;
            public byte BatteryLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_GAMEPAD
        {
            public ushort wButtons;
            public byte bLeftTrigger;
            public byte bRightTrigger;
            public short sThumbLX;
            public short sThumbLY;
            public short sThumbRX;
            public short sThumbRY;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_VIBRATION
        {
            public ushort wLeftMotorSpeed;
            public ushort wRightMotorSpeed;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct XINPUT_CAPABILITIES
        {
            public byte Type;
            public byte SubType;
            public ushort Flags;
            public XINPUT_GAMEPAD Gamepad;
            public XINPUT_VIBRATION Vibration;
        }

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetBatteryInformation")]
        private static extern uint XInputGetBatteryInformation(uint dwUserIndex, byte devType, out XINPUT_BATTERY_INFORMATION pBatteryInformation);

        [DllImport("xinput1_4.dll", EntryPoint = "XInputGetCapabilities")]
        private static extern uint XInputGetCapabilities(uint dwUserIndex, uint dwFlags, out XINPUT_CAPABILITIES pCapabilities);
    }
}
