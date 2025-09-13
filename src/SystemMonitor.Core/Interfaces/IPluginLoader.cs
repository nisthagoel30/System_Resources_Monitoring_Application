using SystemMonitor.Core.Interfaces;

namespace SystemMonitor.Core.Interfaces;

/// <summary>
/// Interface for loading and managing plugins
/// </summary>
public interface IPluginLoader
{
    /// <summary>
    /// Loads all available plugins
    /// </summary>
    /// <returns>Collection of loaded plugins</returns>
    Task<IEnumerable<IMonitorPlugin>> LoadPluginsAsync();

    /// <summary>
    /// Loads plugins from specified directory
    /// </summary>
    /// <param name="pluginDirectory">Directory containing plugin assemblies</param>
    /// <returns>Collection of loaded plugins</returns>
    Task<IEnumerable<IMonitorPlugin>> LoadPluginsFromDirectoryAsync(string pluginDirectory);
}
