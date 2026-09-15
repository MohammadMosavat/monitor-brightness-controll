using System;

namespace BrightnessControl
{
    public class MonitorInfo
    {
        public string Name { get; set; } = "";
        public bool IsInternal { get; set; }
        public IntPtr Handle { get; set; } // only used for external monitors
        public uint MinBrightness { get; set; }
        public uint MaxBrightness { get; set; }
        public uint CurrentBrightness { get; set; }
        public uint DefaultBrightness { get; set; } // value captured when app loaded
    }
}