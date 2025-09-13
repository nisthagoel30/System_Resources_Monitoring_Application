using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Infrastructure.Monitoring;

/// <summary>
/// macOS-specific system resource monitor implementation
/// Uses system commands like 'top', 'vm_stat', and 'df' for resource monitoring
/// </summary>
public class MacOSSystemMonitor : ISystemMonitor
{
    private bool _isInitialized;
    private readonly object _lockObject = new();

    public Task InitializeAsync()
    {
        if (_isInitialized) return Task.CompletedTask;

        lock (_lockObject)
        {
            if (_isInitialized) return Task.CompletedTask;

            try
            {
                // Verify we're on macOS
                if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    throw new PlatformNotSupportedException("MacOSSystemMonitor can only be used on macOS platforms");
                }

                // Verify required commands are available
                if (!IsCommandAvailable("top"))
                {
                    throw new InvalidOperationException("'top' command not found - this system may not support macOS monitoring");
                }

                if (!IsCommandAvailable("vm_stat"))
                {
                    throw new InvalidOperationException("'vm_stat' command not found - this system may not support macOS monitoring");
                }

                if (!IsCommandAvailable("df"))
                {
                    throw new InvalidOperationException("'df' command not found - this system may not support macOS monitoring");
                }

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to initialize macOS system monitor: {ex.Message}", ex);
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

            // Get disk usage (root filesystem)
            var (diskUsed, diskTotal) = await GetDiskUsageAsync();
            resourceData.DiskUsedMB = diskUsed;
            resourceData.DiskTotalMB = diskTotal;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Failed to collect resource data on macOS: {ex.Message}", ex);
        }

        return resourceData;
    }

    private async Task<double> GetCpuUsageAsync()
    {
        try
        {
            // Use 'top' command to get CPU usage
            var processInfo = new ProcessStartInfo
            {
                FileName = "top",
                Arguments = "-l 1 -n 0", // -l 1: one sample, -n 0: no processes listed
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            if (process == null)
            {
                return 0;
            }

            var output = await process.StandardOutput.ReadToEndAsync();
            await process.WaitForExitAsync();

            if (process.ExitCode != 0)
            {
                return 0;
            }

            // Parse CPU usage from top output
            // Look for line like: "CPU usage: 15.25% user, 8.33% sys, 76.41% idle"
            var cpuMatch = Regex.Match(output, @"CPU usage:\s+([\d.]+)%\s+user,\s+([\d.]+)%\s+sys,\s+([\d.]+)%\s+idle");
            if (cpuMatch.Success)
            {
                var userCpu = double.Parse(cpuMatch.Groups[1].Value, CultureInfo.InvariantCulture);
                var sysCpu = double.Parse(cpuMatch.Groups[2].Value, CultureInfo.InvariantCulture);
                var totalUsage = userCpu + sysCpu;
                return Math.Max(0, Math.Min(100, totalUsage));
            }

            return 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MacOSSystemMonitor: Error reading CPU usage: {ex.Message}");
            return 0;
        }
    }

    private async Task<(long used, long total)> GetMemoryUsageAsync()
    {
        try
        {
            // Use 'vm_stat' command to get memory statistics
            var processInfo = new ProcessStartInfo
            {
                FileName = "vm_stat",
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

            // Parse vm_stat output
            var lines = output.Split('\n');
            long pageSize = 4096; // Default page size for macOS
            long freePages = 0;
            long activePages = 0;
            long inactivePages = 0;
            long speculativePages = 0;
            long wiredPages = 0;
            long compressedPages = 0;

            // Extract page size from first line if available
            var pageSizeMatch = Regex.Match(lines.FirstOrDefault() ?? "", @"page size of (\d+) bytes");
            if (pageSizeMatch.Success)
            {
                pageSize = long.Parse(pageSizeMatch.Groups[1].Value);
            }

            foreach (var line in lines)
            {
                if (line.Contains("Pages free:"))
                {
                    freePages = ExtractNumber(line);
                }
                else if (line.Contains("Pages active:"))
                {
                    activePages = ExtractNumber(line);
                }
                else if (line.Contains("Pages inactive:"))
                {
                    inactivePages = ExtractNumber(line);
                }
                else if (line.Contains("Pages speculative:"))
                {
                    speculativePages = ExtractNumber(line);
                }
                else if (line.Contains("Pages wired down:"))
                {
                    wiredPages = ExtractNumber(line);
                }
                else if (line.Contains("Pages stored in compressor:"))
                {
                    compressedPages = ExtractNumber(line);
                }
            }

            // Calculate memory usage
            var totalPages = freePages + activePages + inactivePages + speculativePages + wiredPages + compressedPages;
            var usedPages = totalPages - freePages - speculativePages; // Speculative pages are considered available

            var totalMemory = (totalPages * pageSize) / (1024 * 1024); // Convert to MB
            var usedMemory = (usedPages * pageSize) / (1024 * 1024); // Convert to MB

            return (usedMemory, totalMemory);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MacOSSystemMonitor: Error reading memory usage: {ex.Message}");
            return (0, 0);
        }
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

            // df output: Filesystem 1024-blocks Used Avail Capacity Mounted
            var totalKB = long.Parse(parts[1]);
            var usedKB = long.Parse(parts[2]);

            return (usedKB / 1024, totalKB / 1024); // Convert from KB to MB
        }
        catch (Exception ex)
        {
            Console.WriteLine($"MacOSSystemMonitor: Error reading disk usage: {ex.Message}");
            return (0, 0);
        }
    }

    private static long ExtractNumber(string line)
    {
        var match = Regex.Match(line, @"(\d+)");
        return match.Success ? long.Parse(match.Groups[1].Value) : 0;
    }

    private static bool IsCommandAvailable(string command)
    {
        try
        {
            var processInfo = new ProcessStartInfo
            {
                FileName = "which",
                Arguments = command,
                RedirectStandardOutput = true,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using var process = Process.Start(processInfo);
            process?.WaitForExit();
            return process?.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        _isInitialized = false;
    }
}
