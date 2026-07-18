using System.Collections.Generic;

namespace Playnite.Common
{
    public class SystemInfo
    {
        public bool Is64Bit { get; set; }

        public string WindowsVersion { get; set; }

        public string ActualWindowsVersion { get; set; }

        public string WindowsEdition { get; set; }

        public int WindowsBuildVersion { get; set; }

        public string Cpu { get; set; }

        public int Ram { get; set; }

        public List<string> Gpus { get; set; }

        public List<ComputerScreen> Screens { get; set; }
    }

    // Screen data collection happens in the UI assembly (Computer), which is
    // also why the setters are public here.
    public class ComputerScreen
    {
        public System.Drawing.Rectangle WorkingArea { get; set; }
        public bool Primary { get; set; }
        public string DeviceName { get; set; }
        public System.Drawing.Rectangle Bounds { get; set; }
        public int BitsPerPixel { get; set; }
    }
}
