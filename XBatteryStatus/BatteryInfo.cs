namespace XBatteryStatus
{
    internal readonly struct BatteryInfo
    {
        public readonly int Percent;

        public readonly string Level;

        public readonly bool Wired;

        public readonly bool Waiting;

        public readonly string Name;

        public BatteryInfo(int percent, string level, bool wired, string name, bool waiting = false)
        {
            Percent = percent;
            Level = level;
            Wired = wired;
            Name = name;
            Waiting = waiting;
        }
    }
}
