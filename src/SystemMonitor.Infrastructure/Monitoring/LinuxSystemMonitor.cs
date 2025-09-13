using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

/// <summary>
/// Linux-specific system resource monitor implementation
/// Uses /proc filesystem and system commands for resource monitoring
/// </summary>
public class LinuxSystemMonitor : ISystemMonitor
{
    private bool _isInitialized;
    private readonly object _lockObject = new();
    private long _previousCpuTotal;
    private long _previousCpuIdle;
    private DateTime _lastCpuRead = DateTime.MinValue;

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        lock (_lockObject)
        {
            if (_isInitialized) return;

            try
            {
                // Verify we're on Linux
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    throw new PlatformNotSupportedException("LinuxSystemMonitor can only be used on Linux platforms");
                }

                // Verify required files exist
                if (!File.Exists("/proc/stat"))
                {
                    throw new InvalidOperationException("/proc/stat not found - this system may not support Linux monitoring");
                }

                if (!File.Exists("/proc/meminfo"))
                {
                    throw new InvalidOperationException("/proc/meminfo not found - this system may not support Linux monitoring");
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Linux system monitor: {ex.Message}", ex);
            }
        }

        // Take initial CPU reading outside of lock to initialize baseline
        if (_isInitialized)
        {
            await Task.Run(async () => await GetCpuUsageAsync());
        }
    }

    public async Task<ResourceData> GetResourceDataAsync()
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        var resourceData = new ResourceData();

        try
        {
            // Get CPU usage
            resourceData.CpuUsagePercent = await GetCpuUsageAsync();

            // Get memory usage
            var (ramUsed, ramTotal) = await GetMemoryUsageAsync();
            resourceData.RamUsedMB = ramUsed;
            resourceData.RamTotalMB = ramTotal;

            // Get disk usage (root filesystem)
            var (diskUsed, diskTotal) = await GetDiskUsageAsync();
            resourceData.DiskUsedMB = diskUsed;
            resourceData.DiskTotalMB = diskTotal;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to collect resource data on Linux: {ex.Message}", ex);
        }

        return resourceData;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            var cpuInfo = await File.ReadAllTextAsync("/proc/stat");
            var lines = cpuInfo.Split('\n');
            var cpuLine = lines.FirstOrDefault(l => l.StartsWith("cpu "));

            if (cpuLine == null)
            {
                return 0;
            }

            // Parse CPU times: user, nice, system, idle, iowait, irq, softirq, steal
            var parts = cpuLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 5)
            {
                return 0;
            }

            var user = long.Parse(parts[1]);
            var nice = long.Parse(parts[2]);
            var system = long.Parse(parts[3]);
            var idle = long.Parse(parts[4]);
            var iowait = parts.Length > 5 ? long.Parse(parts[5]) : 0;
            var irq = parts.Length > 6 ? long.Parse(parts[6]) : 0;
            var softirq = parts.Length > 7 ? long.Parse(parts[7]) : 0;
            var steal = parts.Length > 8 ? long.Parse(parts[8]) : 0;

            var totalIdle = idle + iowait;
            var totalNonIdle = user + nice + system + irq + softirq + steal;
            var total = totalIdle + totalNonIdle;

            // Calculate CPU usage percentage
            if (_previousCpuTotal == 0 || DateTime.UtcNow - _lastCpuRead < TimeSpan.FromMilliseconds(500))
            {
                // First reading or too soon since last reading
                _previousCpuTotal = total;
                _previousCpuIdle = totalIdle;
                _lastCpuRead = DateTime.UtcNow;
                return 0;
            }

            var totalDiff = total - _previousCpuTotal;
            var idleDiff = totalIdle - _previousCpuIdle;

            var cpuUsage = totalDiff > 0 ? ((double)(totalDiff - idleDiff) / totalDiff) * 100 : 0;

            _previousCpuTotal = total;
            _previousCpuIdle = totalIdle;
            _lastCpuRead = DateTime.UtcNow;

            return Math.Max(0, Math.Min(100, cpuUsage));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LinuxSystemMonitor: Error reading CPU usage: {ex.Message}");
            return 0;
        }
    }

    private async Task<(long used, long total)> GetMemoryUsageAsync()
    {
        try
        {
            var memInfo = await File.ReadAllTextAsync("/proc/meminfo");
            var lines = memInfo.Split('\n');

            long memTotal = 0;
            long memFree = 0;
            long buffers = 0;
            long cached = 0;
            long sReclaimable = 0;

            foreach (var line in lines)
            {
                if (line.StartsWith("MemTotal:"))
                {
                    memTotal = ParseMemoryLine(line);
                }
                else if (line.StartsWith("MemFree:"))
                {
                    memFree = ParseMemoryLine(line);
                }
                else if (line.StartsWith("Buffers:"))
                {
                    buffers = ParseMemoryLine(line);
                }
                else if (line.StartsWith("Cached:"))
                {
                    cached = ParseMemoryLine(line);
                }
                else if (line.StartsWith("SReclaimable:"))
                {
                    sReclaimable = ParseMemoryLine(line);
                }
            }

            // Calculate used memory (excluding buffers and cache)
            var memUsed = memTotal - memFree - buffers - cached - sReclaimable;

            return (memUsed / 1024, memTotal / 1024); // Convert from KB to MB
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LinuxSystemMonitor: Error reading memory usage: {ex.Message}");
            return (0, 0);
        }
    }

    private static long ParseMemoryLine(string line)
    {
        var match = Regex.Match(line, @"(\d+)");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    private async Task<(long used, long total)> GetDiskUsageAsync()
    {
        try
        {
            // Use 'df' command to get disk usage for root filesystem
            var processInfo = new ProcessStartInfo
            {
                FileName = "df",
                Arguments = "-k /", // -k for KB, / for root filesystem
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return (0, 0);
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return (0, 0);
            }

            // Parse df output (skip header line)
            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length < 2)
            {
                return (0, 0);
            }

            // Get the data line (may be second or third line depending on filesystem name length)
            var dataLine = lines.Skip(1).FirstOrDefault(l => l.Contains('/'));
            if (dataLine == null)
            {
                return (0, 0);
            }

            var parts = dataLine.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length < 4)
            {
                return (0, 0);
            }

            // df output: Filesystem 1K-blocks Used Available Use% Mounted
            var totalKB = long.Parse(parts[1]);
            var usedKB = long.Parse(parts[2]);

            return (usedKB / 1024, totalKB / 1024); // Convert from KB to MB
        }
        catch (Exception ex)
        {
            Console.WriteLine($"LinuxSystemMonitor: Error reading disk usage: {ex.Message}");
            return (0, 0);
        }
    }

    public void Dispose()
    {
        _isInitialized = false;
    }
}
