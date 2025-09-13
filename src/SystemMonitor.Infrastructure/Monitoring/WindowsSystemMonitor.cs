using System.Diagnostics;
using System.Management;
using System.Runtime.InteropServices;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

/// <summary>
/// Windows-specific system resource monitor implementation
/// Uses System.Diagnostics.PerformanceCounter and WMI for accurate resource monitoring
/// </summary>
public class WindowsSystemMonitor : ISystemMonitor
{
    private PerformanceCounter? _cpuCounter;
    private bool _isInitialized;
    private readonly object _lockObject = new();

    public async Task InitializeAsync()
    {
        if (_isInitialized) return;

        lock (_lockObject)
        {
            if (_isInitialized) return;

            try
            {
                // Initialize CPU performance counter
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                    // First call returns 0, so we call it once to initialize
                    _cpuCounter.NextValue();
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize Windows system monitor: {ex.Message}", ex);
            }
        }

        // Wait a bit for counters to stabilize
        await Task.Delay(100);
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

            // Get disk usage (C: drive)
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
                if (_cpuCounter != null && RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
        #if WINDOWS
                    // Performance counter requires a small delay for accurate reading
                    return await Task.Run(() => _cpuCounter.NextValue());
        #else
                    throw new PlatformNotSupportedException("PerformanceCounter is only supported on Windows.");
        #endif
                }

        // Fallback method using Process.GetCurrentProcess() - less accurate but cross-platform
        return await Task.Run(() =>
        {
            var startTime = DateTime.UtcNow;
            var startCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;
            Thread.Sleep(500);

            var endTime = DateTime.UtcNow;
            var endCpuUsage = Process.GetCurrentProcess().TotalProcessorTime;

            var cpuUsedMs = (endCpuUsage - startCpuUsage).TotalMilliseconds;
            var totalMsPassed = (endTime - startTime).TotalMilliseconds;
            var cpuUsageTotal = cpuUsedMs / (Environment.ProcessorCount * totalMsPassed);

            return Math.Min(cpuUsageTotal * 100, 100);
        });
    }

    private async Task<(long used, long total)> GetMemoryUsageAsync()
    {
        return await Task.Run(() =>
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    // Use WMI for accurate memory information
                    using var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem");
                    using var results = searcher.Get();
                    
                    long totalMemory = 0;
                    foreach (ManagementObject result in results)
                    {
                        totalMemory = Convert.ToInt64(result["TotalPhysicalMemory"]);
                        break;
                    }

                    // Get available memory
                    using var availableSearcher = new ManagementObjectSearcher("SELECT AvailableBytes FROM Win32_PerfRawData_PerfOS_Memory");
                    using var availableResults = availableSearcher.Get();
                    
                    long availableMemory = 0;
                    foreach (ManagementObject result in availableResults)
                    {
                        availableMemory = Convert.ToInt64(result["AvailableBytes"]);
                        break;
                    }

                    long usedMemory = totalMemory - availableMemory;
                    return (usedMemory / (1024 * 1024), totalMemory / (1024 * 1024));
                }
                catch
                {
                    // Fallback to GC memory info
                    var gcInfo = GC.GetGCMemoryInfo();
                    var totalMemory = gcInfo.TotalAvailableMemoryBytes;
                    var usedMemory = GC.GetTotalMemory(false);
                    return (usedMemory / (1024 * 1024), totalMemory / (1024 * 1024));
                }
            }

            // Cross-platform fallback
            var gc = GC.GetGCMemoryInfo();
            return (GC.GetTotalMemory(false) / (1024 * 1024), gc.TotalAvailableMemoryBytes / (1024 * 1024));
        });
    }

    private async Task<(long used, long total)> GetDiskUsageAsync()
    {
        return await Task.Run(() =>
        {
            try
            {
                var drive = DriveInfo.GetDrives()
                    .FirstOrDefault(d => d.IsReady && d.DriveType == DriveType.Fixed);

                if (drive != null)
                {
                    var totalBytes = drive.TotalSize;
                    var freeBytes = drive.AvailableFreeSpace;
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

    public void Dispose()
    {
        _cpuCounter?.Dispose();
        _cpuCounter = null;
        _isInitialized = false;
    }
}
