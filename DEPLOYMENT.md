# Deployment Guide

## For Developers (Building Executables)

### Prerequisites
- .NET 6+ SDK installed (works with .NET 6, 7, 8, 9+)
- Git (for cloning)

### Build Process

1. **Clone the repository:**
   ```bash
   git clone https://github.com/yourusername/System_Resources_Monitoring_System.git
   cd System_Resources_Monitoring_System
   ```

2. **Build cross-platform executables:**

   **Windows:**
   ```cmd
   build-all-platforms.bat
   ```

   **Linux/macOS/WSL:**
   ```bash
   chmod +x build-all-platforms.sh
   ./build-all-platforms.sh
   ```

3. **Output:** Self-contained executables in `dist/` folder

## For End Users (Running the Application)

### No .NET Installation Required!

The executables in the `dist/` folder are completely self-contained and include the .NET runtime.

### Platform-Specific Instructions

#### Windows
```cmd
# Navigate to the Windows executable
cd dist/win-x64
# Run the application
SystemMonitor.exe
```

#### Linux
```bash
# Navigate to the Linux executable
cd dist/linux-x64
# Make executable (if needed)
chmod +x SystemMonitor
# Run the application
./SystemMonitor
```

#### macOS Intel
```bash
# Navigate to the macOS Intel executable
cd dist/osx-x64
# Make executable (if needed)
chmod +x SystemMonitor
# Run the application
./SystemMonitor
```

#### macOS Apple Silicon
```bash
# Navigate to the macOS ARM executable
cd dist/osx-arm64
# Make executable (if needed)
chmod +x SystemMonitor
# Run the application
./SystemMonitor
```

## Output Files

The application creates:
- `logs/system-monitor.xlsx` - Excel file with system resource data and charts
- Console output showing real-time system monitoring
- Optional API integration (if configured)

## Features

- ✅ Cross-platform monitoring (Windows, Linux, macOS)
- ✅ Excel export with interactive charts
- ✅ REST API integration
- ✅ Plugin architecture
- ✅ Self-contained executables (no .NET required for end users)

## Distribution

To distribute the application:
1. Run the build script to generate executables
2. Zip the appropriate `dist/platform/` folder
3. Share with users - they can run immediately without any setup
