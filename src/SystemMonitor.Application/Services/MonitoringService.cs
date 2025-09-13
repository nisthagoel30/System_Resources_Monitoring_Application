using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SystemMonitor.Application.Configuration;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Application.Services;

/// <summary>
/// Main monitoring service that orchestrates system monitoring and plugin notifications
/// </summary>
public class MonitoringService
{
    private readonly ISystemMonitor _systemMonitor;
    private readonly IPluginLoader _pluginLoader;
    private readonly ILogger<MonitoringService> _logger;
    private readonly MonitoringSettings _settings;
    private readonly List<IMonitorPlugin> _plugins = new();
    private readonly CancellationTokenSource _cancellationTokenSource = new();
    private Task? _monitoringTask;
    private bool _isRunning;

    public MonitoringService(
        ISystemMonitor systemMonitor,
        IPluginLoader pluginLoader,
        ILogger<MonitoringService> logger,
        IOptions<MonitoringSettings> settings)
    {
        _systemMonitor = systemMonitor ?? throw new ArgumentNullException(nameof(systemMonitor));
        _pluginLoader = pluginLoader ?? throw new ArgumentNullException(nameof(pluginLoader));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _settings = settings.Value ?? throw new ArgumentNullException(nameof(settings));
    }

    /// <summary>
    /// Starts the monitoring service
    /// </summary>
    public async Task StartAsync()
    {
        if (_isRunning)
        {
            _logger.LogWarning("Monitoring service is already running");
            return;
        }

        _logger.LogInformation("Starting System Resource Monitoring Service...");

        try
        {
            // Initialize system monitor
            await _systemMonitor.InitializeAsync();
            _logger.LogInformation("System monitor initialized successfully");

            // Load and initialize plugins
            await LoadAndInitializePluginsAsync();

            // Start monitoring task
            _monitoringTask = MonitoringLoopAsync(_cancellationTokenSource.Token);
            _isRunning = true;

            _logger.LogInformation($"Monitoring started with {_plugins.Count} active plugins. Interval: {_settings.IntervalSeconds} seconds");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start monitoring service");
            throw;
        }
    }

    /// <summary>
    /// Stops the monitoring service
    /// </summary>
    public async Task StopAsync()
    {
        if (!_isRunning)
        {
            return;
        }

        _logger.LogInformation("Stopping monitoring service...");

        _cancellationTokenSource.Cancel();

        if (_monitoringTask != null)
        {
            try
            {
                await _monitoringTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when cancellation is requested
            }
        }

        // Shutdown plugins gracefully
        foreach (var plugin in _plugins)
        {
            try
            {
                await plugin.ShutdownAsync();
                plugin.Dispose();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error shutting down plugin {plugin.Name}");
            }
        }
        _plugins.Clear();

        // Dispose system monitor
        _systemMonitor.Dispose();

        _isRunning = false;
        _logger.LogInformation("Monitoring service stopped");
    }

    private async Task LoadAndInitializePluginsAsync()
    {
        _logger.LogInformation("Loading plugins...");

        var availablePlugins = await _pluginLoader.LoadPluginsAsync();

        foreach (var plugin in availablePlugins)
        {
            try
            {
                var pluginKey = plugin.GetType().Name;
                
                // Check if plugin is enabled in configuration
                if (_settings.Plugins.TryGetValue(pluginKey, out var pluginSettings) && !pluginSettings.Enabled)
                {
                    _logger.LogInformation($"Plugin {plugin.Name} is disabled in configuration");
                    continue;
                }

                // Initialize plugin with configuration
                var config = pluginSettings?.Configuration ?? new Dictionary<string, object>();
                await plugin.InitializeAsync(config);

                _plugins.Add(plugin);
                _logger.LogInformation($"Loaded plugin: {plugin.Name} - {plugin.Description}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to initialize plugin {plugin.Name}");
            }
        }
    }

    private async Task MonitoringLoopAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                // Collect resource data
                var resourceData = await _systemMonitor.GetResourceDataAsync();

                // Output to console if enabled
                if (_settings.EnableConsoleOutput)
                {
                    var timestamp = resourceData.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    Console.WriteLine($"[{timestamp}] {resourceData}");
                }

                // Notify all plugins
                var pluginTasks = _plugins.Select(plugin => NotifyPluginSafely(plugin, resourceData));
                await Task.WhenAll(pluginTasks);

                // Wait for next interval
                await Task.Delay(TimeSpan.FromSeconds(_settings.IntervalSeconds), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when stopping the service
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in monitoring loop");
                
                // Wait a bit before retrying to avoid tight error loops
                await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
            }
        }
    }

    private async Task NotifyPluginSafely(IMonitorPlugin plugin, ResourceData resourceData)
    {
        try
        {
            await plugin.OnResourceDataAsync(resourceData);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Plugin {plugin.Name} failed to process resource data");
        }
    }

    public void Dispose()
    {
        StopAsync().GetAwaiter().GetResult();
        _cancellationTokenSource.Dispose();
    }
}
