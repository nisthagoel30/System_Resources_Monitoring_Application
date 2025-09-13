using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SystemMonitor.Application.Configuration;
using SystemMonitor.Application.Services;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Infrastructure.Monitoring;
using SystemMonitor.Infrastructure.Services;

namespace SystemMonitor.Console;

/// <summary>
/// Main entry point for the System Resource Monitor console application
/// </summary>
public class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            System.Console.WriteLine("System Resource Monitor v1.0 - Cross-Platform Edition");
            System.Console.WriteLine("======================================================");
            System.Console.WriteLine();

            // Build configuration - use the directory where the executable is located
            var configuration = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddEnvironmentVariables()
                .AddCommandLine(args)
                .Build();

            // Build host with dependency injection
            var host = CreateHostBuilder(args, configuration).Build();

            // Handle Ctrl+C gracefully
            using var cancellationTokenSource = new CancellationTokenSource();
            System.Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
                System.Console.WriteLine("\nShutdown requested...");
            };

            // Display platform information
            var systemMonitor = host.Services.GetRequiredService<ISystemMonitor>();
            if (systemMonitor is CrossPlatformSystemMonitor crossPlatformMonitor)
            {
                System.Console.WriteLine($"🖥️  {crossPlatformMonitor.GetImplementationInfo()}");
                System.Console.WriteLine();
            }

            // Start the monitoring service
            var monitoringService = host.Services.GetRequiredService<MonitoringService>();
            await monitoringService.StartAsync();

            System.Console.WriteLine("Press Ctrl+C to stop monitoring...");
            System.Console.WriteLine();

            // Wait for cancellation
            try
            {
                await Task.Delay(Timeout.Infinite, cancellationTokenSource.Token);
            }
            catch (OperationCanceledException)
            {
                // Expected when Ctrl+C is pressed
            }

            // Stop the service
            await monitoringService.StopAsync();
            
            System.Console.WriteLine("Application stopped successfully.");
            return 0;
        }
        catch (Exception ex)
        {
            System.Console.WriteLine($"Application failed to start: {ex.Message}");
            System.Console.WriteLine($"Details: {ex}");
            return 1;
        }
    }

    private static IHostBuilder CreateHostBuilder(string[] args, IConfiguration configuration)
    {
        return Host.CreateDefaultBuilder(args)
            .ConfigureServices((context, services) =>
            {
                // Configure settings
                services.Configure<MonitoringSettings>(configuration.GetSection("Monitoring"));

                // Register core services with cross-platform support
                services.AddSingleton<ISystemMonitorFactory, SystemMonitorFactory>();
                services.AddSingleton<ISystemMonitor, CrossPlatformSystemMonitor>();
                services.AddSingleton<IPluginLoader, PluginLoader>();
                services.AddSingleton<MonitoringService>();

                // Configure logging
                services.AddLogging(builder =>
                {
                    builder.ClearProviders();
                    builder.AddConsole(options =>
                    {
                        options.LogToStandardErrorThreshold = LogLevel.Error;
                    });
                    builder.SetMinimumLevel(LogLevel.Information);
                });
            })
            .UseConsoleLifetime();
    }
}
