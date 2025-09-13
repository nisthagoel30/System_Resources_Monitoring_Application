using SystemMonitor.Core.Models;

namespace SystemMonitor.Core.Interfaces;

/// <summary>
/// Interface for system resource monitoring implementations
/// </summary>
public interface ISystemMonitor
{
    /// <summary>
    /// Collects current system resource data
    /// </summary>
    /// <returns>Current resource data</returns>
    Task<ResourceData> GetResourceDataAsync();

    /// <summary>
    /// Initializes the system monitor
    /// </summary>
    Task InitializeAsync();

    /// <summary>
    /// Disposes resources used by the monitor
    /// </summary>
    void Dispose();
}
