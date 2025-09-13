namespace SystemMonitor.Application.Configuration;

/// <summary>
/// Application configuration settings
/// </summary>
public class MonitoringSettings
{
    /// <summary>
    /// Monitoring interval in seconds
    /// </summary>
    public int IntervalSeconds { get; set; } = 5;

    /// <summary>
    /// Enable console output
    /// </summary>
    public bool EnableConsoleOutput { get; set; } = true;

    /// <summary>
    /// Plugin configurations
    /// </summary>
    public Dictionary<string, PluginSettings> Plugins { get; set; } = new();
}

/// <summary>
/// Plugin-specific settings
/// </summary>
public class PluginSettings
{
    /// <summary>
    /// Whether the plugin is enabled
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Plugin-specific configuration parameters
    /// </summary>
    public Dictionary<string, object> Configuration { get; set; } = new();
}
