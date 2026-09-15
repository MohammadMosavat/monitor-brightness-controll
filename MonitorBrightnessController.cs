using System;
using System.Collections.Generic;
using System.Management;
using System.Runtime.InteropServices;

namespace BrightnessControl
{
    public static class MonitorBrightnessController
    {
        #region DDC/CI P/Invoke (external monitors)

        [StructLayout(LayoutKind.Sequential)]
        private struct PHYSICAL_MONITOR
        {
            public IntPtr hPhysicalMonitor;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
            public string szPhysicalMonitorDescription;
        }

        [DllImport("user32.dll")]
        private static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, MonitorEnumProc lpfnEnum, IntPtr dwData);

        private delegate bool MonitorEnumProc(IntPtr hMonitor, IntPtr hdcMonitor, IntPtr lprcMonitor, IntPtr dwData);

        [DllImport("dxva2.dll")]
        private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

        [DllImport("dxva2.dll")]
        private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll")]
        private static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, PHYSICAL_MONITOR[] pPhysicalMonitorArray);

        [DllImport("dxva2.dll")]
        private static extern bool GetMonitorBrightness(IntPtr hMonitor, ref uint pdwMinimumBrightness, ref uint pdwCurrentBrightness, ref uint pdwMaximumBrightness);

        [DllImport("dxva2.dll")]
        private static extern bool SetMonitorBrightness(IntPtr hMonitor, uint dwNewBrightness);

        #endregion

        private static readonly List<PHYSICAL_MONITOR[]> _openHandleGroups = new();

        public static List<MonitorInfo> GetAllMonitors()
        {
            var result = new List<MonitorInfo>();

            var hMonitors = new List<IntPtr>();
            EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (hMonitor, hdc, rect, data) =>
            {
                hMonitors.Add(hMonitor);
                return true;
            }, IntPtr.Zero);

            foreach (var hMonitor in hMonitors)
            {
                uint count = 0;
                if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref count) || count == 0)
                    continue;

                var physicalMonitors = new PHYSICAL_MONITOR[count];
                if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, count, physicalMonitors))
                    continue;

                _openHandleGroups.Add(physicalMonitors);

                foreach (var pm in physicalMonitors)
                {
                    uint min = 0, cur = 0, max = 0;
                    if (GetMonitorBrightness(pm.hPhysicalMonitor, ref min, ref cur, ref max))
                    {
                        result.Add(new MonitorInfo
                        {
                            Name = string.IsNullOrWhiteSpace(pm.szPhysicalMonitorDescription)
                                ? "External Monitor"
                                : pm.szPhysicalMonitorDescription,
                            IsInternal = false,
                            Handle = pm.hPhysicalMonitor,
                            MinBrightness = min,
                            MaxBrightness = max,
                            CurrentBrightness = cur,
                            DefaultBrightness = cur
                        });
                    }
                }
            }

            try
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightness");
                foreach (ManagementObject obj in searcher.Get())
                {
                    var cur = Convert.ToUInt32(obj["CurrentBrightness"]);
                    result.Add(new MonitorInfo
                    {
                        Name = "Internal Display",
                        IsInternal = true,
                        MinBrightness = 0,
                        MaxBrightness = 100,
                        CurrentBrightness = cur,
                        DefaultBrightness = cur
                    });
                }
            }
            catch
            {
                // No WMI brightness class present
            }

            return result;
        }

        public static void SetBrightness(MonitorInfo monitor, uint value)
        {
            if (monitor.IsInternal)
            {
                using var searcher = new ManagementObjectSearcher(@"root\wmi", "SELECT * FROM WmiMonitorBrightnessMethods");
                foreach (ManagementObject obj in searcher.Get())
                {
                    obj.InvokeMethod("WmiSetBrightness", new object[] { 1, value });
                }
            }
            else
            {
                SetMonitorBrightness(monitor.Handle, value);
            }
        }

        public static void ReleaseAllHandles()
        {
            foreach (var group in _openHandleGroups)
            {
                DestroyPhysicalMonitors((uint)group.Length, group);
            }
            _openHandleGroups.Clear();
        }
    }
}