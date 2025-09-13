using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;
using SystemMonitor.Infrastructure.Services;

namespace SystemMonitor.Infrastructure.Monitoring;

/// <summary>
/// Cross-platform system monitor that automatically detects the platform
/// and delegates to the appropriate platform-specific implementation
/// </summary>
public class CrossPlatformSystemMonitor : ISystemMonitor
{
    private readonly ISystemMonitor _platformMonitor;
    private readonly ISystemMonitorFactory _factory;
    private bool _disposed;

    public CrossPlatformSystemMonitor(ISystemMonitorFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
        _platformMonitor = _factory.CreateSystemMonitor();
    }

    public async Task InitializeAsync()
    {
        ThrowIfDisposed();
        await _platformMonitor.InitializeAsync();
    }

    public async Task<ResourceData> GetResourceDataAsync()
    {
        ThrowIfDisposed();
        return await _platformMonitor.GetResourceDataAsync();
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _platformMonitor?.Dispose();
            _disposed = true;
        }
    }

    private void ThrowIfDisposed()
    {
        if (_disposed)
        {
            throw new ObjectDisposedException(nameof(CrossPlatformSystemMonitor));
        }
    }

    /// <summary>
    /// Gets information about the current platform and monitor implementation
    /// </summary>
    public string GetImplementationInfo()
    {
        var platformInfo = _factory.GetPlatformInfo();
        var implementationType = _platformMonitor.GetType().Name;
        return $"Platform: {platformInfo}, Implementation: {implementationType}";
    }
}
