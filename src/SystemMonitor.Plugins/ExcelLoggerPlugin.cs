using OfficeOpenXml;
using SystemMonitor.Core.Interfaces;
using SystemMonitor.Core.Models;

namespace SystemMonitor.Plugins;

public class ExcelLoggerPlugin : IMonitorPlugin
{
    public string Name => "Excel Logger";
    public string Description => "Logs system resource data to an Excel file";

    private string _excelFilePath = "logs/system-monitor.xlsx";
    private readonly object _lockObject = new();
    private bool _isInitialized;
    private int _currentRow = 2;
    private int _dataPointsLogged = 0;

    public Task InitializeAsync(IDictionary<string, object>? configuration = null)
    {
        if (_isInitialized) return Task.CompletedTask;

        // Set EPPlus license context for non-commercial use
        ExcelPackage.LicenseContext = LicenseContext.NonCommercial;

        if (configuration?.TryGetValue("excelFilePath", out var excelFilePathObj) == true)
        {
            _excelFilePath = excelFilePathObj.ToString() ?? _excelFilePath;
        }

        var projectRoot = FindProjectRoot();
        _excelFilePath = Path.GetFullPath(Path.Combine(projectRoot, _excelFilePath));

        var directory = Path.GetDirectoryName(_excelFilePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        InitializeExcelFile();
        _isInitialized = true;
        Console.WriteLine($"ExcelLoggerPlugin: ✅ Initialized - Saving to {Path.GetFileName(_excelFilePath)}");
        return Task.CompletedTask;
    }

    private void InitializeExcelFile()
    {
        if (File.Exists(_excelFilePath))
        {
            // If file exists, find the next row to write to
            using var existingPackage = new ExcelPackage(new FileInfo(_excelFilePath));
            var existingWorksheet = existingPackage.Workbook.Worksheets["System Monitor"];
            if (existingWorksheet != null)
            {
                _currentRow = existingWorksheet.Dimension?.End.Row + 1 ?? 2;
                return;
            }
        }

        // Create new Excel file with headers and formatting
        using var package = new ExcelPackage();
        var worksheet = package.Workbook.Worksheets.Add("System Monitor");

        var headers = new[] { "Timestamp", "CPU Usage (%)", "RAM Usage (Used/Total)", "Disk Usage (Used/Total)", "RAM Usage %", "Disk Usage %" };

        // Set up all headers with styling
        for (int i = 0; i < headers.Length; i++)
        {
            worksheet.Cells[1, i + 1].Value = headers[i];
            worksheet.Cells[1, i + 1].Style.Font.Bold = true;
            worksheet.Cells[1, i + 1].Style.Fill.PatternType = OfficeOpenXml.Style.ExcelFillStyle.Solid;
            worksheet.Cells[1, i + 1].Style.Fill.BackgroundColor.SetColor(System.Drawing.Color.LightBlue);
        }

        worksheet.Column(1).Width = 20;  // Timestamp
        worksheet.Column(2).Width = 15;  // CPU Usage (%)
        worksheet.Column(3).Width = 25;  // RAM Usage (Used/Total)
        worksheet.Column(4).Width = 25;  // Disk Usage (Used/Total)
        worksheet.Column(5).Width = 15;  // RAM Usage %
        worksheet.Column(6).Width = 15;  // Disk Usage %

        worksheet.Column(2).Style.Numberformat.Format = "0.00%";
        worksheet.Column(5).Style.Numberformat.Format = "0.00%";
        worksheet.Column(6).Style.Numberformat.Format = "0.00%";

        // Add freeze panes to keep headers visible
        worksheet.View.FreezePanes(2, 1);

        // Save the initial file
        package.SaveAs(new FileInfo(_excelFilePath));
        _currentRow = 2;
    }

    private string FindProjectRoot()
    {
        var currentDir = AppContext.BaseDirectory;
        
        // Look for solution file or src directory to identify project root
        while (!string.IsNullOrEmpty(currentDir))
        {
            if (File.Exists(Path.Combine(currentDir, "SystemMonitor.sln")) ||
                Directory.Exists(Path.Combine(currentDir, "src")))
            {
                return currentDir;
            }
            
            var parent = Directory.GetParent(currentDir);
            if (parent == null) break;
            currentDir = parent.FullName;
        }
        
        // Fallback to current directory if project root not found
        return AppContext.BaseDirectory;
    }

    public async Task OnResourceDataAsync(ResourceData resourceData)
    {
        if (!_isInitialized)
        {
            await InitializeAsync();
        }

        // Use Task.Run for true parallel/async execution that doesn't block the monitoring thread
        await Task.Run(() =>
        {
            lock (_lockObject)
            {
                try
                {
                    using var package = new ExcelPackage(new FileInfo(_excelFilePath));
                    var worksheet = package.Workbook.Worksheets["System Monitor"];

                    if (worksheet == null)
                    {
                        package.Dispose();
                        InitializeExcelFile();
                        return;
                    }

                    worksheet.Cells[_currentRow, 1].Value = resourceData.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
                    worksheet.Cells[_currentRow, 2].Value = resourceData.CpuUsagePercent / 100;
                    worksheet.Cells[_currentRow, 3].Value = $"{resourceData.RamUsedMB:N0}/{resourceData.RamTotalMB:N0} MB";
                    worksheet.Cells[_currentRow, 4].Value = $"{resourceData.DiskUsedMB:N0}/{resourceData.DiskTotalMB:N0} MB";
                    worksheet.Cells[_currentRow, 5].Value = resourceData.RamUsagePercent / 100;
                    worksheet.Cells[_currentRow, 6].Value = resourceData.DiskUsagePercent / 100;

                    if (_currentRow % 5 == 0)
                    {
                        AddConditionalFormatting(worksheet, _currentRow);
                    }

                    if (_currentRow >= 3)
                    {
                        CreateOrUpdateCharts(worksheet, _currentRow);
                    }

                    _currentRow++;
                    _dataPointsLogged++;
                    package.Save();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Excel write error: {ex.Message}");
                }
            }
        }).ConfigureAwait(false);
    }

    private void AddConditionalFormatting(ExcelWorksheet worksheet, int row)
    {
        try
        {
            var cpuRule = worksheet.ConditionalFormatting.AddGreaterThan(worksheet.Cells[2, 2, row, 2]);
            cpuRule.Formula = "0.8";
            cpuRule.Style.Fill.BackgroundColor.Color = System.Drawing.Color.LightCoral;

            var ramRule = worksheet.ConditionalFormatting.AddGreaterThan(worksheet.Cells[2, 5, row, 5]);
            ramRule.Formula = "0.85";
            ramRule.Style.Fill.BackgroundColor.Color = System.Drawing.Color.Orange;

            var diskRule = worksheet.ConditionalFormatting.AddGreaterThan(worksheet.Cells[2, 6, row, 6]);
            diskRule.Formula = "0.9";
            diskRule.Style.Fill.BackgroundColor.Color = System.Drawing.Color.Yellow;
        }
        catch { }
    }

    private void CreateOrUpdateCharts(ExcelWorksheet worksheet, int row)
    {
        try
        {
            // Remove existing charts to avoid duplicates
            for (int i = worksheet.Drawings.Count - 1; i >= 0; i--)
            {
                if (worksheet.Drawings[i] is OfficeOpenXml.Drawing.Chart.ExcelChart)
                {
                    worksheet.Drawings.Remove(worksheet.Drawings[i]);
                }
            }

            // Create CPU Usage Chart - positioned vertically with larger size
            var cpuChart = worksheet.Drawings.AddChart("CPUChart", OfficeOpenXml.Drawing.Chart.eChartType.Line);
            cpuChart.Title.Text = "CPU Usage Over Time";
            cpuChart.SetPosition(2, 0, 8, 0); // Start at row 2, column 8
            cpuChart.SetSize(600, 250); // Increased width and height
            
            var cpuSeries = cpuChart.Series.Add(worksheet.Cells[2, 2, row, 2], worksheet.Cells[2, 1, row, 1]);
            cpuSeries.Header = "CPU Usage %";

            // Create RAM Usage Chart - positioned below CPU chart
            var ramChart = worksheet.Drawings.AddChart("RAMChart", OfficeOpenXml.Drawing.Chart.eChartType.Line);
            ramChart.Title.Text = "RAM Usage Over Time";
            ramChart.SetPosition(18, 0, 8, 0); // Below CPU chart (row 18, column 8)
            ramChart.SetSize(600, 250); // Increased width and height
            var ramSeries = ramChart.Series.Add(worksheet.Cells[2, 5, row, 5], worksheet.Cells[2, 1, row, 1]);
            ramSeries.Header = "RAM Usage %";

            // Create Disk Usage Chart - positioned below RAM chart
            var diskChart = worksheet.Drawings.AddChart("DiskChart", OfficeOpenXml.Drawing.Chart.eChartType.Line);
            diskChart.Title.Text = "Disk Usage Over Time";
            diskChart.SetPosition(34, 0, 8, 0); // Below RAM chart (row 34, column 8)
            diskChart.SetSize(600, 250); // Increased width and height
            var diskSeries = diskChart.Series.Add(worksheet.Cells[2, 6, row, 6], worksheet.Cells[2, 1, row, 1]);
            diskSeries.Header = "Disk Usage %";
        }
        catch { }
    }

    public Task ShutdownAsync()
    {
        if (_dataPointsLogged > 0)
        {
            Console.WriteLine($"ExcelLoggerPlugin: 📊 Saved {_dataPointsLogged} data points with charts to {Path.GetFileName(_excelFilePath)}");
        }
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        // Cleanup completed
    }
}
