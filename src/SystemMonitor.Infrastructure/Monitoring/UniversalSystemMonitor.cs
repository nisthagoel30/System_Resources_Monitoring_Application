using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

/// <summary>
/// Universal system resource monitor that works on all platforms
/// Uses cross-platform .NET APIs with platform-specific optimizations when available
/// </summary>
public class UniversalSystemMonitor : ISystemMonitor
{
    private bool _isInitialized;
    private readonly object _lockObject = new();
    private DateTime _lastCpuCheck = DateTime.MinValue;
    private TimeSpan _lastCpuTime = TimeSpan.Zero;

    public Task InitializeAsync()
    {
        if (_isInitialized) return Task.CompletedTask;

        lock (_lockObject)
        {
            if (_isInitialized) return Task.CompletedTask;

            try
            {
                // This monitor should work on any platform with .NET 6+
                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize universal system monitor: {ex.Message}", ex);
            }
        }

        return Task.CompletedTask;
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

            // Get disk usage
            var (diskUsed, diskTotal) = await GetDiskUsageAsync();
            resourceData.DiskUsedMB = diskUsed;
            resourceData.DiskTotalMB = diskTotal;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to collect resource data: {ex.Message}", ex);
        }

        return resourceData;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                var currentTime = DateTime.UtcNow;
                var currentCpuTime = Process.GetCurrentProcess().TotalProcessorTime;

                if (_lastCpuCheck == DateTime.MinValue)
                {
                    _lastCpuCheck = currentTime;
                    _lastCpuTime = currentCpuTime;
                    return 0.0; // First measurement, return 0
                }

                var timeDifference = currentTime - _lastCpuCheck;
                var cpuTimeDifference = currentCpuTime - _lastCpuTime;

                if (timeDifference.TotalMilliseconds > 0)
                {
                    var cpuUsage = (cpuTimeDifference.TotalMilliseconds / (timeDifference.TotalMilliseconds * Environment.ProcessorCount)) * 100;
                    
                    _lastCpuCheck = currentTime;
                    _lastCpuTime = currentCpuTime;

                    // Return system-wide CPU approximation based on current process load
                    // This is a rough approximation - in production, you'd want platform-specific implementations
                    return Math.Max(0, Math.Min(100, cpuUsage * 10)); // Scale up as rough system estimate
                }

                return 0.0;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UniversalSystemMonitor: Error reading CPU usage: {ex.Message}");
            return 0;
        }
    }

    private async Task<(long used, long total)> GetMemoryUsageAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                // Use GC memory info as a cross-platform fallback
                var gcMemoryInfo = GC.GetGCMemoryInfo();
                var totalMemory = gcMemoryInfo.TotalAvailableMemoryBytes;
                var usedMemory = GC.GetTotalMemory(false);

                // If we can't get system memory, use GC info
                if (totalMemory <= 0)
                {
                    totalMemory = Environment.WorkingSet * 4; // Rough estimate
                }

                return (usedMemory / (1024 * 1024), totalMemory / (1024 * 1024));
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UniversalSystemMonitor: Error reading memory usage: {ex.Message}");
            return (0, 0);
        }
    }

    private async Task<(long used, long total)> GetDiskUsageAsync()
    {
        try
        {
            return await Task.Run(() =>
            {
                try
                {
                    // Try to get the first available fixed drive
                    var drive = DriveInfo.GetDrives()
                        .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

                    if (drive != null)
                    {
                        var totalBytes = drive.TotalSize;
                        var freeBytes = drive.AvailableFreeSpace;
                        var usedBytes = totalBytes - freeBytes;

                        return (usedBytes / (1024 * 1024), totalBytes / (1024 * 1024));
                    }

                    // If no fixed drive found, try the current directory
                    var currentDrive = new DriveInfo(Directory.GetCurrentDirectory());
                    if (currentDrive.IsReady)
                    {
                        var totalBytes = currentDrive.TotalSize;
                        var freeBytes = currentDrive.AvailableFreeSpace;
                        var usedBytes = totalBytes - freeBytes;

                        return (usedBytes / (1024 * 1024), totalBytes / (1024 * 1024));
                    }

                    return (0, 0);
                }
                catch
                {
                    return (0, 0);
                }
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"UniversalSystemMonitor: Error reading disk usage: {ex.Message}");
            return (0, 0);
        }
    }

    public void Dispose()
    {
        _isInitialized = false;
    }
}
