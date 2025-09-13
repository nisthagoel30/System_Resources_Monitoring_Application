using System.Text;
using System.Text.Json;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Plugins;

public class ApiPostPlugin : IMonitorPlugin
{
    public string Name => "API Post";
    public string Description => "Posts system resource data to a REST API endpoint";

    private string _apiUrl = string.Empty;
    private readonly HttpClient _httpClient = new();
    private bool _isInitialized;
    private int _successCount = 0;
    private int _failureCount = 0;

    public Task InitializeAsync(IDictionary<string, object>? configuration = null)
    {
        if (_isInitialized) return Task.CompletedTask;

        if (configuration?.TryGetValue("apiUrl", out var apiUrlObj) == true)
        {
            _apiUrl = apiUrlObj.ToString() ?? string.Empty;
        }

        if (string.IsNullOrEmpty(_apiUrl))
        {
            throw new InvalidOperationException("API URL is required for ApiPostPlugin. Configure 'apiUrl' in plugin settings.");
        }

        _httpClient.Timeout = TimeSpan.FromSeconds(30);
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "SystemMonitor/1.0");

        _isInitialized = true;
        Console.WriteLine($"ApiPostPlugin: ✅ Initialized - Posting to {_apiUrl}");
        return Task.CompletedTask;
    }

    public async Task OnResourceDataAsync(ResourceData resourceData)
    {
        if (!_isInitialized)
        {
            throw new InvalidOperationException("Plugin not initialized. Call InitializeAsync first.");
        }

        try
        {
            var payload = new
            {
                cpu = Math.Round(resourceData.CpuUsagePercent, 2),
                ram_used = resourceData.RamUsedMB,
                disk_used = resourceData.DiskUsedMB
            };

            var json = JsonSerializer.Serialize(payload);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var response = await _httpClient.PostAsync(_apiUrl, content);
            
            if (response.IsSuccessStatusCode)
            {
                Console.WriteLine($"Api Loaded with: {json}");
                _successCount++;
            }
            else
            {
                _failureCount++;
                if (_failureCount == 1) // Only log first failure
                {
                    Console.WriteLine($"ApiPostPlugin: ❌ API calls failing - Status: {response.StatusCode}");
                }
            }
        }
        catch (Exception ex)
        {
            _failureCount++;
            if (_failureCount == 1) // Only log first failure
            {
                Console.WriteLine($"ApiPostPlugin: ❌ API connection error: {ex.Message}");
            }
        }
    }

    public Task ShutdownAsync()
    {
        var total = _successCount + _failureCount;
        if (total > 0)
        {
            Console.WriteLine($"ApiPostPlugin: 📊 Summary - {_successCount}/{total} API calls successful ({(_successCount * 100.0 / total):F1}%)");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
