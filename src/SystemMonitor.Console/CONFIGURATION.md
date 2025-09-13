# Configuration Guide

## appsettings.json Configuration

### Monitoring Section
- **IntervalSeconds**: How often to collect system resource data (in seconds). Default: 5
- **EnableConsoleOutput**: Whether to display monitoring data in the console. Default: true

### Plugins Section
Configure different output methods for system monitoring data.

#### ExcelLoggerPlugin
Saves monitoring data to an Excel file with charts and conditional formatting.
- **Enabled**: Set to `true` to enable Excel logging
- **excelFilePath**: Path where the Excel file will be created (relative to application directory)

#### ApiPostPlugin  
Sends monitoring data to a remote API endpoint via HTTP POST.
- **Enabled**: Set to `true` to enable API posting
- **apiUrl**: The endpoint URL to send monitoring data to

### Logging Section
Controls application logging levels:
- **Default**: General application logging level
- **Microsoft**: Microsoft framework logging level  
- **Microsoft.Hosting.Lifetime**: Application startup/shutdown logging level

Available log levels: Trace, Debug, Information, Warning, Error, Critical, None
