using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace Playnite.Common
{
    public class Explorer
    {
#if WINDOWS
        [DllImport("shell32.dll", SetLastError = true)]
        private static extern int SHOpenFolderAndSelectItems(IntPtr pidlFolder, uint cidl, [In, MarshalAs(UnmanagedType.LPArray)] IntPtr[] apidl, uint dwFlags);

        [DllImport("shell32.dll", SetLastError = true)]
        private static extern void SHParseDisplayName([MarshalAs(UnmanagedType.LPWStr)] string name, IntPtr bindingContext, [Out] out IntPtr pidl, uint sfgaoIn, [Out] out uint psfgaoOut);
#endif

        public static void NavigateToFileSystemEntry(string path)
        {
            var parentFolder = Path.GetDirectoryName(path);
            if (string.IsNullOrEmpty(parentFolder))
            {
                return;
            }

#if WINDOWS
            SHParseDisplayName(parentFolder, IntPtr.Zero, out var nativeFolder, 0, out _);
            if (nativeFolder == IntPtr.Zero)
            {
                return;
            }

            try
            {
                var itemToSelect = Path.GetFileName(path);
                SHParseDisplayName(Path.Combine(parentFolder, itemToSelect), IntPtr.Zero, out var nativeFile, 0, out _);
                try
                {
                    var fileArray = new[] { nativeFile == IntPtr.Zero ? nativeFolder : nativeFile };
                    SHOpenFolderAndSelectItems(nativeFolder, (uint)fileArray.Length, fileArray, 0);
                }
                finally
                {
                    if (nativeFile != IntPtr.Zero)
                    {
                        Marshal.FreeCoTaskMem(nativeFile);
                    }
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(nativeFolder);
            }
#else
            OpenDirectory(parentFolder);
#endif
        }

        public static void OpenDirectory(string path)
        {
            if (OperatingSystem.IsWindows())
            {
                var directoryPath = path.EndsWith("\\", StringComparison.Ordinal) ? path : path + "\\";
                Process.Start(new ProcessStartInfo(directoryPath) { UseShellExecute = true });
                return;
            }

            var opener = OperatingSystem.IsMacOS() ? "open" : "xdg-open";
            var startInfo = new ProcessStartInfo(opener)
            {
                UseShellExecute = false
            };
            startInfo.ArgumentList.Add(path);
            Process.Start(startInfo);
        }
    }
}
