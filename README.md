# HwMonitor - Real-time Hardware Monitoring Application

A lightweight, reliable desktop application for monitoring system hardware metrics in real-time, including CPU and GPU temperatures, fan speeds, drive temperatures, and component loads.

## Features

- **Real-time Hardware Monitoring**: View CPU, GPU, memory, and storage drive metrics with 950ms updates
- **Multi-sensor Support**: Monitors temperatures, fan speeds, load percentages across system components
- **Reliable Updates**: Thread-safe design with watchdog timer prevents application freezes
- **Simplified Drive Naming**: Shows drives as "Disk 1", "Disk 2", etc. for clean display
- **Low Resource Usage**: Optimized to use minimal system resources while providing accurate data
- **Windows Integration**: Optional startup with Windows and proper UAC handling
- **Single File Deployment**: Published as a self-contained application for easy distribution

## System Requirements

- Windows 10/11 64-bit
- .NET 8.0 Runtime (included in self-contained builds)
- Administrator privileges (required for hardware access)

## Installation

### Option 1: Download Release
1. Download the latest release from the [Releases page](https://github.com/yourusername/HwMonitor/releases)
2. Extract the files to a folder
3. Right-click `HardwareMonitorApp.exe` and select "Run as Administrator"

### Option 2: Build from Source
```bash
git clone https://github.com/yourusername/HwMonitor.git
cd HwMonitor
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### Option 3: Quick Build Script
Use the included PowerShell script:
```powershell
.\CompileAndRun.ps1
```

## Usage

The application automatically detects and displays:
- **CPU**: Temperature, load percentage, fan speed
- **GPU**: Temperature, load percentage, fan speed  
- **Memory**: Temperature
- **Storage**: Individual disk temperatures (shown as "Disk 1", "Disk 2", etc.)

The interface updates every 950ms and maintains sensor visibility even when readings are temporarily unavailable (showing "N/A").

## Technical Details

### Built With
- **Framework**: .NET 8.0 with WPF
- **Hardware Monitoring**: LibreHardwareMonitorLib 0.9.5-pre412
- **Architecture**: Single-threaded UI with background sensor polling
- **Deployment**: Self-contained single-file executable

### Key Components
- [`MainWindow.xaml.cs`](MainWindow.xaml.cs): UI implementation and event handling
- [`SensorService.cs`](SensorService.cs): Core hardware monitoring and data collection service
- `UpdateVisitor`: Hardware traversal and sensor update implementation
- [`LogHelper.cs`](LogHelper.cs): Centralized logging functionality

### Architecture Highlights
- Thread-safe sensor updates with update locks
- Hardware cache for improved performance
- Watchdog timer for reliable operation
- Graceful degradation when sensors are unavailable

## Building the Project

### Prerequisites
- Visual Studio 2022 or later
- .NET 8.0 SDK
- Windows 10/11 development environment

### Build Commands
```bash
# Standard build
dotnet build -c Release

# Self-contained single file
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true

# Development build with watch
dotnet watch run
```

## Troubleshooting

### Common Issues

**"Access Denied" or sensors not detected:**
- Ensure you're running as Administrator
- Check Windows permissions for hardware access

**"Unknown Publisher" warning:**
- This is normal for unsigned executables
- The application is safe to run despite this warning

**Fan sensors disappear:**
- Some motherboards report fans as 0 RPM when stopped
- The application will show "N/A" for temporarily unavailable sensors
- GPU fans may show 0 RPM when in eco-mode

**High CPU usage:**
- Normal operation should use <1% CPU
- Check Task Manager for background processes
- Try restarting the application

### Debug Information
Check the log files created in the application directory for detailed error information.

## Contributing

1. Fork the repository
2. Create a feature branch: `git checkout -b feature/amazing-feature`
3. Make your changes
4. Run tests and ensure the application builds
5. Commit your changes: `git commit -m 'Add amazing feature'`
6. Push to the branch: `git push origin feature/amazing-feature`
7. Open a Pull Request

### Code Style
- Follow existing C# conventions
- Use meaningful variable and method names
- Add comments for complex hardware interactions
- Test with different hardware configurations when possible

## License

This project is licensed under the MIT License - see the LICENSE file for details.

## Acknowledgments

- [LibreHardwareMonitor](https://github.com/LibreHardwareMonitor/LibreHardwareMonitor) for hardware monitoring capabilities
- [HidSharp](https://github.com/IntergatedCircuits/HidSharpCore) for hardware interface detection
- Microsoft .NET team for the excellent WPF framework

## Support

If you encounter issues:
1. Check the [Issues page](https://github.com/yourusername/HwMonitor/issues) for known problems
2. Create a new issue with system details and error logs
3. Include your hardware specifications when reporting sensor issues

This project is licensed under the MIT License

## Acknowledgments

- Based on the LibreHardwareMonitor open-source project
