#!/bin/bash

# Cross-Platform Build Script for System Resource Monitor
# Builds self-contained executables for Windows, Linux, and macOS

echo "🚀 Building System Resource Monitor for all platforms..."

# Create output directory
mkdir -p dist

# Build for Windows x64
echo "🪟 Building for Windows x64..."
dotnet publish src/SystemMonitor.Console/SystemMonitor.Console.csproj -c Release -r win-x64 --self-contained -o dist/win-x64

# Build for Linux x64
echo "🐧 Building for Linux x64..."
dotnet publish src/SystemMonitor.Console/SystemMonitor.Console.csproj -c Release -r linux-x64 --self-contained -o dist/linux-x64

# Build for macOS x64
echo "🍎 Building for macOS x64..."
dotnet publish src/SystemMonitor.Console/SystemMonitor.Console.csproj -c Release -r osx-x64 --self-contained -o dist/osx-x64

# Build for macOS ARM64 (Apple Silicon)
echo "🍎 Building for macOS ARM64..."
dotnet publish src/SystemMonitor.Console/SystemMonitor.Console.csproj -c Release -r osx-arm64 --self-contained -o dist/osx-arm64

echo "✅ Build complete! Executables available in dist/ directory:"
echo "  📁 dist/win-x64/SystemMonitor.exe"
echo "  📁 dist/linux-x64/SystemMonitor"
echo "  📁 dist/osx-x64/SystemMonitor"
echo "  📁 dist/osx-arm64/SystemMonitor"
echo ""
echo "🎯 To run on your platform:"
echo "  Windows: dist/win-x64/SystemMonitor.exe"
echo "  Linux:   dist/linux-x64/SystemMonitor"
echo "  macOS:   dist/osx-x64/SystemMonitor (Intel) or dist/osx-arm64/SystemMonitor (Apple Silicon)"
