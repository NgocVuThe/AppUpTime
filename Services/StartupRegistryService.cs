using Microsoft.Win32;
using System;
using System.Diagnostics;
using System.IO;

namespace DailyUptimeWidget.Services
{
    public interface IStartupRegistryService
    {
        bool IsAutoStartEnabled { get; }
        void ToggleAutoStart(bool enable);
    }

    public class StartupRegistryService : IStartupRegistryService
    {
        private const string AppName = "DailyUptimeWidget";
        private const string RegistryPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

        public bool IsAutoStartEnabled
        {
            get
            {
                using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, false);
                return key?.GetValue(AppName) != null;
            }
        }

        public void ToggleAutoStart(bool enable)
        {
            using var key = Registry.CurrentUser.OpenSubKey(RegistryPath, true);
            if (key == null) return;

            if (enable)
            {
                // Get absolute path of the executable
                string? exePath = Environment.ProcessPath;
                
                // LOGGING
                try {
                    var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyUptimeWidget", "debug_log.txt");
                    File.AppendAllText(logFile, $"{DateTime.Now}: [Registry] Initial exePath: {exePath}\n");
                } catch {}

                if (exePath == null) return;

                // Handle "dotnet run" or VS Debugging (where ProcessPath is dotnet.exe)
                if (exePath.EndsWith("dotnet.exe", StringComparison.OrdinalIgnoreCase))
                {
                    string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                    // Use the entry assembly name to find the .exe (e.g. DailyUptimeWidget.exe)
                    string? assemblyName = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Name;
                    
                    if (!string.IsNullOrEmpty(assemblyName))
                    {
                        var potentialExe = Path.Combine(baseDir, $"{assemblyName}.exe");
                        try {
                             var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyUptimeWidget", "debug_log.txt");
                             File.AppendAllText(logFile, $"{DateTime.Now}: [Registry] Detected dotnet.exe. Checking potential: {potentialExe}\n");
                        } catch {}

                        if (File.Exists(potentialExe))
                        {
                            exePath = potentialExe;
                        }
                    }
                }
                
                // Quote the path to handle spaces
                try {
                     var logFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "DailyUptimeWidget", "debug_log.txt");
                     File.AppendAllText(logFile, $"{DateTime.Now}: [Registry] Writing Path: {exePath}\n");
                } catch {}

                key.SetValue(AppName, $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue(AppName, false);
            }
        }
    }
}
