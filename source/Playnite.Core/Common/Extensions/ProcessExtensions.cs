using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
#if WINDOWS
using Playnite.Native;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
#endif
using System.Text.RegularExpressions;

namespace System.Diagnostics
{
    public static class ProcessExtensions
    {
        public static bool TryGetMainModuleFileName(this Process process, out string fileName, int buffer = 1024)
        {
#if WINDOWS
            fileName = null;
            var handle = Kernel32.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var fileNameBuilder = new StringBuilder(buffer);
                uint bufferLength = (uint)fileNameBuilder.Capacity + 1;
                var result = Kernel32.QueryFullProcessImageName(handle, 0, fileNameBuilder, ref bufferLength);
                fileName = result ? fileNameBuilder.ToString() : null;
                return result;
            }
            finally
            {
                Kernel32.CloseHandle(handle);
            }
#else
            try
            {
                fileName = process.MainModule?.FileName;
                return !string.IsNullOrEmpty(fileName);
            }
            catch (InvalidOperationException)
            {
                fileName = null;
                return false;
            }
            catch (System.ComponentModel.Win32Exception)
            {
                fileName = null;
                return false;
            }
#endif
        }

        public static bool TryGetParentId(this Process process, out int processId)
        {
#if WINDOWS
            processId = 0;
            var handle = Kernel32.OpenProcess(ProcessAccessFlags.QueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero)
            {
                return false;
            }

            try
            {
                var info = new PROCESS_BASIC_INFORMATION();
                int status = Ntdll.NtQueryInformationProcess(handle, 0, ref info, Marshal.SizeOf(info), out var returnLength);
                if (status != 0)
                {
                    return false;
                }

                processId = info.InheritedFromUniqueProcessId.ToInt32();
                return true;
            }
            finally
            {
                Kernel32.CloseHandle(handle);
            }
#else
            processId = 0;
            if (!OperatingSystem.IsLinux())
            {
                return false;
            }

            try
            {
                var stat = File.ReadAllText($"/proc/{process.Id}/stat");
                var commandEnd = stat.LastIndexOf(')');
                if (commandEnd < 0 || commandEnd + 2 >= stat.Length)
                {
                    return false;
                }

                var fields = stat.Substring(commandEnd + 2).Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                return fields.Length > 1 && int.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture, out processId);
            }
            catch (IOException)
            {
                return false;
            }
            catch (UnauthorizedAccessException)
            {
                return false;
            }
#endif
        }

        public static bool IsRunning(string processPattern)
        {
            return Process.GetProcesses().FirstOrDefault(a => Regex.IsMatch(a.ProcessName, processPattern, RegexOptions.IgnoreCase)) != null;
        }

        public static string GetCommandLine(this Process process)
        {
#if WINDOWS
            using (ManagementObjectSearcher searcher = new ManagementObjectSearcher("SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id))
            using (ManagementObjectCollection objects = searcher.Get())
            {
                return objects.Cast<ManagementBaseObject>().SingleOrDefault()?["CommandLine"]?.ToString();
            }
#else
            if (!OperatingSystem.IsLinux())
            {
                return null;
            }

            try
            {
                var commandLine = File.ReadAllText($"/proc/{process.Id}/cmdline");
                return commandLine.TrimEnd('\0').Replace('\0', ' ');
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
#endif
        }
    }
}
