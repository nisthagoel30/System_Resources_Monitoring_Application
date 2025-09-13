using SystemMonitor.Core.Models;

namespace SystemMonitor.Core.Interfaces;

/// <summary>
/// Interface for monitoring plugins that respond to resource data updates
/// </summary>
public interface IMonitorPlugin
{
    /// <summary>
    /// Plugin name for identification
    /// </summary>
    string Name { get; }

    /// <summary>
    /// Plugin description
    /// </summary>
    string Description { get; }

    /// <summary>
    /// Initializes the plugin with configuration
    /// </summary>
    /// <param name="configuration">Plugin-specific configuration</param>
    Task InitializeAsync(IDictionary<string, object>? configuration = null);

    /// <summary>
    /// Handles resource data updates
    /// </summary>
    /// <param name="resourceData">Current resource data</param>
    Task OnResourceDataAsync(ResourceData resourceData);

    /// <summary>
    /// Gracefully shuts down the plugin and reports summary
    /// </summary>
    Task ShutdownAsync();

    /// <summary>
    /// Disposes plugin resources
    /// </summary>
    void Dispose();
}
