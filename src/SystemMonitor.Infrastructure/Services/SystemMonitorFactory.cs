using System.Runtime.InteropServices;
using SystemMonitor.Core.Interfaces;

namespace SystemMonitor.Infrastructure.Services;

/// <summary>
/// Factory service for creating platform-specific system monitors
/// </summary>
public interface ISystemMonitorFactory
{
    /// <summary>
    /// Creates the appropriate system monitor for the current platform
    /// </summary>
    /// <returns>Platform-specific system monitor implementation</returns>
    ISystemMonitor CreateSystemMonitor();

    /// <summary>
    /// Gets the current platform information
    /// </summary>
    /// <returns>Platform information string</returns>
    string GetPlatformInfo();
}

/// <summary>
/// Platform detection and system monitor factory implementation
/// </summary>
public class SystemMonitorFactory : ISystemMonitorFactory
{
    public ISystemMonitor CreateSystemMonitor()
    {
        // For maximum compatibility, use the Universal monitor for all platforms
        // This ensures the application works everywhere without platform-specific issues
        return new Monitoring.UniversalSystemMonitor();
        
        // Note: Platform-specific implementations are available for better accuracy:
        // - WindowsSystemMonitor (uses PerformanceCounter and WMI)
        // - LinuxSystemMonitor (uses /proc filesystem)  
        // - MacOSSystemMonitor (uses system commands)
        // These can be enabled by modifying this factory method
    }

    public string GetPlatformInfo()
    {
        var platformName = "Unknown";
        
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            platformName = "Windows";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            platformName = "Linux";
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            platformName = "macOS";
        }

        var architecture = RuntimeInformation.ProcessArchitecture.ToString();
        var frameworkVersion = RuntimeInformation.FrameworkDescription;
        var osDescription = RuntimeInformation.OSDescription;
        
        return $"{platformName} ({architecture}) - {frameworkVersion} - {osDescription}";
    }
}
