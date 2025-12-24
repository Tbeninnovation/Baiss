using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Versioning;
using System.Text.Json;
using System.Management;
using Baiss.Application.DTOs;



namespace Baiss.Infrastructure.Services
{
    public class ConfigService
    {
        private readonly ILogger<ConfigService> _logger;

        public ConfigService(ILogger<ConfigService> logger)
        {
            _logger = logger;
        }

        public void CreateConfigFile()
        {
            try
            {
                bool shouldCreateConfig = false;

                // Get the application's base directory
                string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
                string configFilePath = Path.Combine(appDirectory, "baiss_config.json");

                if (File.Exists(configFilePath))
                {
                    string jsonString = File.ReadAllText(configFilePath);

                    // Check if the file is empty or contains only whitespace
                    if (string.IsNullOrWhiteSpace(jsonString))
                    {
                        _logger.LogWarning("Config JSON file exists but is empty. Recreating with default values.");
                        File.Delete(configFilePath);
                        shouldCreateConfig = true;
                    }
                    else
                    {
                        var configg = JsonSerializer.Deserialize<ConfigeDto>(jsonString);
                        _logger.LogInformation("Config JSON file already exists. Skipping creation.");

                        bool configUpdated = false;

                        // Check if PythonPath exists and is not empty, if not, set default path
                        if (string.IsNullOrEmpty(configg.PythonPath) || !Directory.Exists(configg.PythonPath))
                        {
                            configg.PythonPath = GetDefaultPythonPath();
                            _logger.LogInformation($"PythonPath was empty or invalid. Set to default: {configg.PythonPath}");
                            configUpdated = true;
                        }

                        if (string.IsNullOrEmpty(configg.BaissPythonCorePath) || !Directory.Exists(configg.BaissPythonCorePath))
                        {
                            configg.BaissPythonCorePath = GetBaissCorePythonPath();
                            _logger.LogInformation($"BaissPythonCorePath was empty or invalid. Set to: {configg.BaissPythonCorePath}");
                            configUpdated = true;
                        }

                        if (string.IsNullOrEmpty(configg.LlamaCppServerPath) || !File.Exists(configg.LlamaCppServerPath))
                        {
                            configg.LlamaCppServerPath = GetLlamaCppServerPath();
                            _logger.LogInformation($"LlamaCppServerPath was empty or invalid. Set to: {configg.LlamaCppServerPath}");
                            configUpdated = true;
                        }

                        // Check if CPU info exists, if not, get and set it
                        if (configg.CpuInfo == null)
                        {
                            configg.CpuInfo = GetCpuInfo();
                            _logger.LogInformation("CPU info was missing. Added CPU information.");
                            configUpdated = true;
                        }

                        // Check if GPU info exists, if not, get and set it
                        if (configg.GpuInfo == null)
                        {
                            configg.GpuInfo = GetGpuInfo();
                            _logger.LogInformation("GPU info was missing. Added GPU information.");
                            configUpdated = true;
                        }

                        // Check if RAM info exists, if not, get and set it
                        if (configg.RamInfo == null)
                        {
                            configg.RamInfo = GetRamInfo();
                            _logger.LogInformation("RAM info was missing. Added RAM information.");
                            configUpdated = true;
                        }

                        // Save the updated configuration back to file if any changes were made
                        if (configUpdated)
                        {
                            string updatedJson = JsonSerializer.Serialize(configg, new JsonSerializerOptions
                            {
                                WriteIndented = true
                            });
                            File.WriteAllText(configFilePath, updatedJson);
                            _logger.LogInformation("Configuration file updated with missing system information.");
                        }
                        return; // Exit early if we processed an existing valid config
                    }
                }
                else
                {
                    shouldCreateConfig = true;
                }

                // Create new config file if needed (either file doesn't exist or was empty)
                if (shouldCreateConfig)
                {

                    string corePath = GetBaissCorePythonPath();
                    string llamaServerPath = GetLlamaCppServerPath();
                    var osInfo = GetOperatingSystemInfo();
                    var osArchitecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString();
                    // get Cpu and Gpu and RAM
                    var cpuInfo = GetCpuInfo();
                    var gpuInfo = GetGpuInfo();
                    var ramInfo = GetRamInfo();

                    // Get default Python path
                    var defaultPythonPath = GetDefaultPythonPath();

                    // Create additional system info object for debugging
                    var systemConfig = new
                    {
                        CreatedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                        PythonPath = defaultPythonPath,
                        BaissPythonCorePath = corePath,
                        LlamaCppServerPath = llamaServerPath,
                        OSDescription = osInfo,
                        OSArchitecture = osArchitecture,
                        SystemInfo = new
                        {
                            CpuInfo = cpuInfo,
                            GpuInfo = gpuInfo,
                            RamInfo = ramInfo
                        }
                    };

                    // Serialize to JSON with pretty formatting
                    string jsonContent = JsonSerializer.Serialize(systemConfig, new JsonSerializerOptions
                    {
                        WriteIndented = true
                    });

                    File.WriteAllText(configFilePath, jsonContent);

                    _logger.LogInformation($"Config JSON file created successfully at: {configFilePath}");
                    _logger.LogInformation($"Python path set to: {defaultPythonPath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create config JSON file");
            }
        }

        private string GetBaissCorePythonPath()
        {

            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string corePath = Path.Combine(appDirectory, "core");

            _logger.LogInformation($"Core path:  ->>>>          {corePath}");

            if (Directory.Exists(corePath))
            {
                corePath = Path.GetFullPath(corePath);
            }
            else // to handle the case when running in dev
            {
                corePath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "core"));
                if (!Directory.Exists(corePath))
                {
                    corePath = "";
                }
            }
            return corePath;
        }

        private object GetCpuInfo()
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return GetWindowsCpuInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    return GetMacCpuInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    return GetLinuxCpuInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get CPU information");
            }

            return new
            {
                Name = "Unable to retrieve CPU information",
                Cores = Environment.ProcessorCount.ToString()
            };
        }

        private object GetGpuInfo()
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return GetWindowsGpuInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    return GetMacGpuInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    return GetLinuxGpuInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get GPU information");
            }

            return new[] { new { Name = "Unable to retrieve GPU information" } };
        }

        private object GetRamInfo()
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return GetWindowsRamInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    return GetMacRamInfo();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    return GetLinuxRamInfo();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get RAM information");
            }

            return new { TotalRAM = "Unable to retrieve RAM information" };
        }

        private string GetOperatingSystemInfo()
        {
            try
            {
                var osDescription = System.Runtime.InteropServices.RuntimeInformation.OSDescription;

                // Check for Windows 11 (build 22000 or higher)
                if (osDescription.Contains("Microsoft Windows") && osDescription.Contains("10.0"))
                {
                    // Extract build number
                    var parts = osDescription.Split('.');
                    if (parts.Length >= 3 && int.TryParse(parts[2], out int buildNumber))
                    {
                        if (buildNumber >= 22000)
                        {
                            return $"Microsoft Windows 11.0.{buildNumber}";
                        }
                    }
                }

                return osDescription;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get OS information");
                return "Unknown Operating System";
            }
        }

        // Windows-specific implementations
        [SupportedOSPlatform("windows")]
        private object GetWindowsCpuInfo()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("select * from Win32_Processor"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        return new
                        {
                            Name = obj["Name"]?.ToString() ?? "Unknown",
                            Manufacturer = obj["Manufacturer"]?.ToString() ?? "Unknown",
                            NumberOfCores = obj["NumberOfCores"]?.ToString() ?? "Unknown",
                            NumberOfLogicalProcessors = obj["NumberOfLogicalProcessors"]?.ToString() ?? "Unknown",
                            MaxClockSpeed = obj["MaxClockSpeed"]?.ToString() + " MHz" ?? "Unknown"
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Windows CPU information");
            }

            return new { Name = "Unknown Windows CPU", Cores = Environment.ProcessorCount.ToString() };
        }

        [SupportedOSPlatform("windows")]
        private object GetWindowsGpuInfo()
        {
            try
            {
                var gpuList = new List<object>();
                using (var searcher = new ManagementObjectSearcher("select * from Win32_VideoController"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        var name = obj["Name"]?.ToString();
                        if (!string.IsNullOrEmpty(name) && !name.Contains("Microsoft Basic"))
                        {
                            gpuList.Add(new
                            {
                                Name = name,
                                AdapterRAM = obj["AdapterRAM"] != null ?
                                    (Convert.ToUInt64(obj["AdapterRAM"]) / (1024 * 1024)).ToString() + " MB" :
                                    "Unknown",
                                DriverVersion = obj["DriverVersion"]?.ToString() ?? "Unknown"
                            });
                        }
                    }
                }
                return gpuList.Count > 0 ? gpuList : new[] { new { Name = "No discrete GPU found" } };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Windows GPU information");
            }

            return new[] { new { Name = "Unknown Windows GPU" } };
        }

        [SupportedOSPlatform("windows")]
        private object GetWindowsRamInfo()
        {
            try
            {
                var totalRam = 0UL;
                var availableRam = 0UL;

                // Get total physical memory
                using (var searcher = new ManagementObjectSearcher("select * from Win32_ComputerSystem"))
                {
                    foreach (ManagementObject obj in searcher.Get())
                    {
                        totalRam = Convert.ToUInt64(obj["TotalPhysicalMemory"]);
                        break;
                    }
                }

                // Get available memory using PerformanceCounter
                var pc = new System.Diagnostics.PerformanceCounter("Memory", "Available Bytes");
                availableRam = (ulong)pc.NextValue();

                return new
                {
                    TotalRAM = (totalRam / (1024 * 1024)).ToString() + " MB",
                    AvailableRAM = (availableRam / (1024 * 1024)).ToString() + " MB",
                    UsedRAM = ((totalRam - availableRam) / (1024 * 1024)).ToString() + " MB"
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Windows RAM information");
            }

            return new { TotalRAM = "Unknown Windows RAM" };
        }

        // macOS-specific implementations
        private object GetMacCpuInfo()
        {
            try
            {
                // Use system_profiler command to get CPU info
                var processInfo = new ProcessStartInfo
                {
                    FileName = "system_profiler",
                    Arguments = "SPHardwareDataType -json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // Parse the JSON output to get CPU information
                        // For now, return basic info with processor count
                        return new
                        {
                            Name = "macOS CPU (system_profiler)",
                            NumberOfCores = Environment.ProcessorCount.ToString(),
                            Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get macOS CPU information");
            }

            return new
            {
                Name = "macOS CPU",
                Cores = Environment.ProcessorCount.ToString(),
                Architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture.ToString()
            };
        }

        private object GetMacGpuInfo()
        {
            try
            {
                // Use system_profiler to get GPU info
                var processInfo = new ProcessStartInfo
                {
                    FileName = "system_profiler",
                    Arguments = "SPDisplaysDataType -json",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // For now, return basic info
                        return new[] { new { Name = "macOS GPU (system_profiler)" } };
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get macOS GPU information");
            }

            return new[] { new { Name = "macOS GPU" } };
        }

        private object GetMacRamInfo()
        {
            try
            {
                // Use sysctl to get memory info
                var processInfo = new ProcessStartInfo
                {
                    FileName = "sysctl",
                    Arguments = "hw.memsize",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        // Parse output like "hw.memsize: 17179869184"
                        var parts = output.Split(':');
                        if (parts.Length > 1 && ulong.TryParse(parts[1].Trim(), out ulong totalBytes))
                        {
                            var totalMB = totalBytes / (1024 * 1024);
                            return new
                            {
                                TotalRAM = totalMB.ToString() + " MB",
                                Platform = "macOS"
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get macOS RAM information");
            }

            return new { TotalRAM = "macOS RAM (unknown)" };
        }

        // Linux-specific implementations
        private object GetLinuxCpuInfo()
        {
            try
            {
                // Read /proc/cpuinfo
                if (File.Exists("/proc/cpuinfo"))
                {
                    var cpuInfo = File.ReadAllText("/proc/cpuinfo");
                    var lines = cpuInfo.Split('\n');

                    var modelName = lines.FirstOrDefault(l => l.StartsWith("model name"))
                        ?.Split(':').LastOrDefault()?.Trim() ?? "Unknown Linux CPU";

                    return new
                    {
                        Name = modelName,
                        NumberOfCores = Environment.ProcessorCount.ToString(),
                        Platform = "Linux"
                    };
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Linux CPU information");
            }

            return new
            {
                Name = "Linux CPU",
                Cores = Environment.ProcessorCount.ToString()
            };
        }

        private object GetLinuxGpuInfo()
        {
            try
            {
                // Try to use lspci to get GPU info
                var processInfo = new ProcessStartInfo
                {
                    FileName = "lspci",
                    Arguments = "-v | grep -i vga",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd();
                        process.WaitForExit();

                        if (!string.IsNullOrEmpty(output))
                        {
                            return new[] { new { Name = "Linux GPU: " + output.Trim() } };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Linux GPU information");
            }

            return new[] { new { Name = "Linux GPU" } };
        }

        private object GetLinuxRamInfo()
        {
            try
            {
                // Read /proc/meminfo
                if (File.Exists("/proc/meminfo"))
                {
                    var memInfo = File.ReadAllText("/proc/meminfo");
                    var lines = memInfo.Split('\n');

                    var totalLine = lines.FirstOrDefault(l => l.StartsWith("MemTotal:"));
                    var availableLine = lines.FirstOrDefault(l => l.StartsWith("MemAvailable:"));

                    if (totalLine != null)
                    {
                        var totalKb = totalLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1];
                        if (ulong.TryParse(totalKb, out ulong totalKbValue))
                        {
                            var totalMB = totalKbValue / 1024; // Convert KB to MB

                            var availableMB = "Unknown";
                            if (availableLine != null)
                            {
                                var availableKb = availableLine.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries)[1];
                                if (ulong.TryParse(availableKb, out ulong availableKbValue))
                                {
                                    availableMB = (availableKbValue / 1024).ToString() + " MB";
                                }
                            }

                            return new
                            {
                                TotalRAM = totalMB.ToString() + " MB",
                                AvailableRAM = availableMB,
                                Platform = "Linux"
                            };
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Linux RAM information");
            }

            return new { TotalRAM = "Linux RAM (unknown)" };
        }

        private string GetDefaultPythonPath()
        {
            try
            {
                if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    return GetWindowsPythonPath();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
                {
                    return GetMacPythonPath();
                }
                else if (System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux))
                {
                    return GetLinuxPythonPath();
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get default Python path");
            }

            return string.Empty;
        }

        [SupportedOSPlatform("windows")]
        private string GetWindowsPythonPath()
        {
            try
            {
                // Check common Python installation locations

                var localPythonPath = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory), "python.exe", SearchOption.AllDirectories).FirstOrDefault();
                // var localPythonPath = Path.GetFullPath("python-amd64");

                if (File.Exists(localPythonPath))
                {
                     var pythonDirectory = Path.GetDirectoryName(localPythonPath);
                    _logger.LogInformation($"Found local Python at: {localPythonPath}");
                    return pythonDirectory;
                }
                // else
                // {
                //     localPythonPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "..", "..", "python-amd64"));
                //     if (Directory.Exists(localPythonPath))
                //     {
                //         _logger.LogInformation($"Found local Python at: {localPythonPath}");
                //         return localPythonPath;
                //     }
                // }

                var commonPaths = new[]
                {
                    @"C:\Python39\python.exe",
                    @"C:\Python310\python.exe",
                    @"C:\Python311\python.exe",
                    @"C:\Python312\python.exe",
                    @"C:\Python313\python.exe",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python39\python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python310\python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python311\python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python312\python.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Python\Python313\python.exe"),
                    @"C:\Program Files\Python39\python.exe",
                    @"C:\Program Files\Python310\python.exe",
                    @"C:\Program Files\Python311\python.exe",
                    @"C:\Program Files\Python312\python.exe",
                    @"C:\Program Files\Python313\python.exe"
                };

                // Check if any of the common paths exist
                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        var pythonDirectory = Path.GetDirectoryName(path);
                        _logger.LogInformation($"Found Python at: {pythonDirectory}");
                        return pythonDirectory;
                    }
                }

                // Try to find python.exe in PATH
                var pathFromEnvironment = FindPythonInPath();
                if (!string.IsNullOrEmpty(pathFromEnvironment))
                {
                    var pythonDirectory = Path.GetDirectoryName(pathFromEnvironment);
                    return pythonDirectory;
                }

                // Check if python is available via 'py' launcher
                var pyLauncherPath = FindPythonViaLauncher();
                if (!string.IsNullOrEmpty(pyLauncherPath))
                {
                    var pythonDirectory = Path.GetDirectoryName(pyLauncherPath);
                    return pythonDirectory;
                }

                _logger.LogWarning("Python installation not found in common Windows locations");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Windows Python path");
                return string.Empty;
            }
        }

        private string GetMacPythonPath()
        {
            try
            {

                string pathToPython = Directory.GetFiles(Path.Combine(AppDomain.CurrentDomain.BaseDirectory), "python", SearchOption.AllDirectories).FirstOrDefault();


                // var pathToPython = Path.Combine(localPythonPath, "baiss_venv", "bin", "python3.12");
                // Check if the local Python exists
                if (File.Exists(pathToPython))
                {
                    var pythonDirectory = Path.GetDirectoryName(pathToPython);
                    if (!pythonDirectory.EndsWith("/"))
                    {
                        pythonDirectory += "/";
                    }
                    _logger.LogInformation($"Found local Python at: {pathToPython}");

                    // Make all files in the python directory executable
                    try
                    {
                        var chmodProcess = new ProcessStartInfo
                        {
                            FileName = "/bin/bash",
                            Arguments = $"-c \"find '{pythonDirectory}' -type f -exec chmod +x {{}} \\;\"",
                            UseShellExecute = false,
                            CreateNoWindow = true
                        };
                        using (var process = Process.Start(chmodProcess))
                        {
                            process?.WaitForExit();
                        }

                        // Create symbolic links for python and python3
                        try
                        {
                            var python312Path = Path.Combine(pythonDirectory, "python3.12");
                            var pythonLinkPath = Path.Combine(pythonDirectory, "python");
                            var python3LinkPath = Path.Combine(pythonDirectory, "python3");

                            if (File.Exists(python312Path))
                            {
                                // Create python symlink
                                if (File.Exists(pythonLinkPath))
                                {
                                    var rmPythonProcess = new ProcessStartInfo
                                    {
                                        FileName = "/bin/bash",
                                        Arguments = $"-c \"rm -f '{pythonLinkPath}' '{python3LinkPath}'\"",
                                        UseShellExecute = false,
                                        CreateNoWindow = true
                                    };
                                    using (var process = Process.Start(rmPythonProcess))
                                    {
                                        process?.WaitForExit();
                                    }
                                    _logger.LogInformation($"Removed existing python symlink");
                                }


                                var lnProcess = new ProcessStartInfo
                                {
                                    FileName = "/bin/bash",
                                    Arguments = $"-c \"ln -s '{python312Path}' '{pythonLinkPath}'\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using (var process = Process.Start(lnProcess))
                                {
                                    process?.WaitForExit();
                                }
                                _logger.LogInformation($"Created symlink: python -> python3.12");

                                // Create python3 symlink
                                var ln3Process = new ProcessStartInfo
                                {
                                    FileName = "/bin/bash",
                                    Arguments = $"-c \"ln -s '{python312Path}' '{python3LinkPath}'\"",
                                    UseShellExecute = false,
                                    CreateNoWindow = true
                                };
                                using (var process = Process.Start(ln3Process))
                                {
                                    process?.WaitForExit();
                                }
                                _logger.LogInformation($"Created symlink: python3 -> python3.12");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogWarning(ex, "Failed to create python symbolic links");
                        }

                        _logger.LogInformation($"Applied chmod +x to all files in: {pythonDirectory}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, $"Failed to apply chmod +x to files in: {pythonDirectory}");
                    }

                    return pythonDirectory;
                }

                // Check common macOS Python locations
                var commonPaths = new[]
                {
                    "/usr/local/bin/python3",
                    "/usr/bin/python3",
                    "/opt/homebrew/bin/python3",
                    "/usr/local/bin/python",
                    "/usr/bin/python"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        var pythonDirectory = Path.GetDirectoryName(path);
                        if (!pythonDirectory.EndsWith("/"))
                        {
                            pythonDirectory += "/";
                        }
                        _logger.LogInformation($"Found Python at: {path}, returning directory: {pythonDirectory}");
                        return pythonDirectory;
                    }
                }

                // Try to find python3 in PATH
                var pathFromEnvironment = FindPythonInPath("python3");
                if (!string.IsNullOrEmpty(pathFromEnvironment))
                {
                    var pythonDirectory = Path.GetDirectoryName(pathFromEnvironment);
                    if (!pythonDirectory.EndsWith("/"))
                    {
                        pythonDirectory += "/";
                    }
                    return pythonDirectory;
                }

                _logger.LogWarning("Python installation not found in common macOS locations");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get macOS Python path");
                return string.Empty;
            }
        }

        private string GetLinuxPythonPath()
        {
            try
            {
                // Check common Linux Python locations
                var commonPaths = new[]
                {
                    "/usr/bin/python3",
                    "/usr/local/bin/python3",
                    "/usr/bin/python",
                    "/usr/local/bin/python"
                };

                foreach (var path in commonPaths)
                {
                    if (File.Exists(path))
                    {
                        _logger.LogInformation($"Found Python at: {path}");
                        return path;
                    }
                }

                // Try to find python3 in PATH
                var pathFromEnvironment = FindPythonInPath("python3");
                if (!string.IsNullOrEmpty(pathFromEnvironment))
                {
                    return pathFromEnvironment;
                }

                _logger.LogWarning("Python installation not found in common Linux locations");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get Linux Python path");
                return string.Empty;
            }
        }

        private string FindPythonInPath(string pythonCommand = "python")
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "where",
                    Arguments = pythonCommand,
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                // Use 'which' on Unix-like systems
                if (!System.Runtime.InteropServices.RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
                {
                    processInfo.FileName = "which";
                }

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output))
                        {
                            // Take the first line if multiple paths are returned
                            var firstPath = output.Split('\n')[0].Trim();
                            if (File.Exists(firstPath))
                            {
                                _logger.LogInformation($"Found Python in PATH: {firstPath}");
                                return firstPath;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to find {pythonCommand} in PATH");
            }

            return string.Empty;
        }

        [SupportedOSPlatform("windows")]
        private string FindPythonViaLauncher()
        {
            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = "py",
                    Arguments = "-c \"import sys; print(sys.executable)\"",
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(processInfo))
                {
                    if (process != null)
                    {
                        var output = process.StandardOutput.ReadToEnd().Trim();
                        process.WaitForExit();

                        if (process.ExitCode == 0 && !string.IsNullOrEmpty(output) && File.Exists(output))
                        {
                            _logger.LogInformation($"Found Python via py launcher: {output}");
                            return output;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to find Python via py launcher");
            }

            return string.Empty;
        }

        private string GetLlamaCppServerPath()
        {
            string appDirectory = AppDomain.CurrentDomain.BaseDirectory;
            string serverName = OperatingSystem.IsWindows() ? "llama-server.exe" : "llama-server";

            // Check if it's in the app directory (production/copied)
            string serverPath = Path.Combine(appDirectory, "llama-cpp", "bin", serverName);
            if (File.Exists(serverPath))
            {
                return serverPath;
            }

            // Check dev path relative to Baiss.UI project
            // Assuming we are in Baiss/Baiss.UI/bin/Debug/net8.0/
            // We need to go up to root

            // Try to find the root by looking for Baiss.sln
            string currentDir = appDirectory;
            while (!string.IsNullOrEmpty(currentDir) && !File.Exists(Path.Combine(currentDir, "Baiss.sln")))
            {
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }

            if (!string.IsNullOrEmpty(currentDir))
            {
                // We found Baiss/ folder. The root is one level up.
                string rootDir = Directory.GetParent(currentDir)?.FullName;
                if (!string.IsNullOrEmpty(rootDir))
                {
                    serverPath = Path.Combine(rootDir, "llama-cpp", "bin", serverName);
                    if (File.Exists(serverPath))
                    {
                        return serverPath;
                    }
                }
            }

            return string.Empty;
        }
    }
}
