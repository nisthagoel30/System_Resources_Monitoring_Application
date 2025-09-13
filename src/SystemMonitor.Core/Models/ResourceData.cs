namespace SystemMonitor.Core.Models;

/// <summary>
/// Represents system resource data collected from monitoring
/// </summary>
public class ResourceData
{
    /// <summary>
    /// CPU usage percentage (0-100)
    /// </summary>
    public double CpuUsagePercent { get; set; }

    /// <summary>
    /// Used RAM in megabytes
    /// </summary>
    public long RamUsedMB { get; set; }

    /// <summary>
    /// Total RAM in megabytes
    /// </summary>
    public long RamTotalMB { get; set; }

    /// <summary>
    /// Used disk space in megabytes
    /// </summary>
    public long DiskUsedMB { get; set; }

    /// <summary>
    /// Total disk space in megabytes
    /// </summary>
    public long DiskTotalMB { get; set; }

    /// <summary>
    /// Timestamp when the data was collected (Indian Standard Time)
    /// </summary>
    public DateTime Timestamp { get; set; } = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.FindSystemTimeZoneById("India Standard Time"));

    /// <summary>
    /// RAM usage percentage
    /// </summary>
    public double RamUsagePercent => RamTotalMB > 0 ? (double)RamUsedMB / RamTotalMB * 100 : 0;

    /// <summary>
    /// Disk usage percentage
    /// </summary>
    public double DiskUsagePercent => DiskTotalMB > 0 ? (double)DiskUsedMB / DiskTotalMB * 100 : 0;

    public override string ToString()
    {
        return $"CPU: {CpuUsagePercent:F1}% | RAM: {RamUsedMB}MB/{RamTotalMB}MB ({RamUsagePercent:F1}%) | Disk: {DiskUsedMB}MB/{DiskTotalMB}MB ({DiskUsagePercent:F1}%)";
    }
}
