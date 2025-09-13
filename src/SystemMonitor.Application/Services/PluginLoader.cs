using System.Reflection;
using SystemMonitor.Core.Interfaces;

namespace SystemMonitor.Application.Services;

/// <summary>
/// Service for loading plugins from assemblies
/// </summary>
public class PluginLoader : IPluginLoader
{
    public async Task<IEnumerable<IMonitorPlugin>> LoadPluginsAsync()
    {
        // Load plugins from current assembly (built-in plugins)
        var currentAssembly = Assembly.GetExecutingAssembly();
        var pluginAssemblies = new[] { currentAssembly }.ToList();

        // Also load from SystemMonitor.Plugins assembly
        try
        {
            var pluginsAssembly = Assembly.LoadFrom("SystemMonitor.Plugins.dll");
            pluginAssemblies.Add(pluginsAssembly);
        }
        catch
        {
            // Assembly not found, try loading from current directory
            try
            {
                var pluginsPath = Path.Combine(AppContext.BaseDirectory, "SystemMonitor.Plugins.dll");
                if (File.Exists(pluginsPath))
                {
                    var pluginsAssembly = Assembly.LoadFrom(pluginsPath);
                    pluginAssemblies.Add(pluginsAssembly);
                }
            }
            catch
            {
                // Ignore if plugins assembly cannot be loaded
            }
        }

        var plugins = new List<IMonitorPlugin>();

        foreach (var assembly in pluginAssemblies)
        {
            try
            {
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IMonitorPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(pluginType) is IMonitorPlugin plugin)
                        {
                            plugins.Add(plugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create instance of plugin {pluginType.Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load plugins from assembly {assembly.FullName}: {ex.Message}");
            }
        }

        return await Task.FromResult(plugins);
    }

    public async Task<IEnumerable<IMonitorPlugin>> LoadPluginsFromDirectoryAsync(string pluginDirectory)
    {
        if (!Directory.Exists(pluginDirectory))
        {
            return Enumerable.Empty<IMonitorPlugin>();
        }

        var plugins = new List<IMonitorPlugin>();
        var dllFiles = Directory.GetFiles(pluginDirectory, "*.dll", SearchOption.TopDirectoryOnly);

        foreach (var dllFile in dllFiles)
        {
            try
            {
                var assembly = Assembly.LoadFrom(dllFile);
                var pluginTypes = assembly.GetTypes()
                    .Where(t => typeof(IMonitorPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

                foreach (var pluginType in pluginTypes)
                {
                    try
                    {
                        if (Activator.CreateInstance(pluginType) is IMonitorPlugin plugin)
                        {
                            plugins.Add(plugin);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to create instance of plugin {pluginType.Name} from {dllFile}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load assembly {dllFile}: {ex.Message}");
            }
        }

        return await Task.FromResult(plugins);
    }
}
