using System.Diagnostics;

namespace Terminate
{
    internal class Program
    {
        static void Main(string[] args)
        {
            Process[] processes = Process.GetProcessesByName("XBatteryStatus");

            foreach (var process in processes)
            {
                try
                {
                    process.Kill();
                }
                catch { }
            }
        }
    }
}