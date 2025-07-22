# HwMonitor Architecture Documentation

This document explains the architecture, design patterns, and purpose of each file in the HwMonitor application.

## Table of Contents
- [Overview](#overview)
- [Core Architecture](#core-architecture)
- [File Structure & Purpose](#file-structure--purpose)
- [Data Flow](#data-flow)
- [Design Patterns](#design-patterns)
- [Threading Model](#threading-model)
- [Build System](#build-system)

## Overview

HwMonitor is a Windows WPF application that monitors hardware sensors in real-time. The architecture follows a clear separation of concerns with distinct layers for UI, hardware monitoring, and logging.

```
┌─────────────────────────────────────────────────────────┐
│                    User Interface Layer                  │
│  (MainWindow.xaml + MainWindow.xaml.cs + App.xaml)     │
└─────────────────────┬───────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────┐
│                 Service Layer                           │
│              (SensorService.cs)                         │
└─────────────────────┬───────────────────────────────────┘
                      │
┌─────────────────────▼───────────────────────────────────┐
│              Hardware Abstraction                       │
│           (LibreHardwareMonitorLib)                     │
└─────────────────────────────────────────────────────────┘
```

## Core Architecture

### 1. **Layered Architecture**
- **Presentation Layer**: WPF UI components handle user interactions and display
- **Service Layer**: SensorService abstracts hardware monitoring logic
- **Hardware Abstraction**: LibreHardwareMonitorLib provides low-level hardware access
- **Utility Layer**: LogHelper provides centralized logging

### 2. **Key Design Principles**
- **Separation of Concerns**: Each class has a single responsibility
- **Thread Safety**: UI updates are dispatched to the UI thread
- **Error Resilience**: Graceful handling of hardware access failures
- **Resource Management**: Proper disposal of hardware monitoring resources

## File Structure & Purpose

### Core Application Files

#### `App.xaml` & `App.xaml.cs`
**Purpose**: WPF application entry point and global configuration
- Defines application-wide resources and startup behavior
- Handles unhandled exceptions at the application level
- Sets up the main window initialization

#### `MainWindow.xaml`
**Purpose**: Main UI layout and styling definitions
- **Window Configuration**: Sets up window size, title, and behavior
- **Resource Definitions**: Styles for labels, values, and sections
- **UI Structure**: 
  - Loading view with progress indicator
  - Numeric dashboard with grouped sections (CPU, GPU, Memory, Disks)
  - Data templates for key-value pair display
- **Layout Strategy**: Uses Grid and StackPanel for responsive design

#### `MainWindow.xaml.cs`
**Purpose**: Main window code-behind with UI logic and event handling
- **Key Responsibilities**:
  - Sensor service initialization and management
  - UI update orchestration with DispatcherTimer
  - Thread-safe UI updates via Dispatcher.Invoke
  - Error handling and status display
  - Admin privilege verification

**Key Methods**:
- `InitializeUI()`: Sets up timer and performs initial hardware scan
- `UpdateSensors()`: Background sensor polling with thread safety
- `UpdateUI()`: Thread-safe UI updates
- `CheckForMissedUpdates()`: Watchdog timer implementation

#### `SensorService.cs`
**Purpose**: Hardware monitoring abstraction layer
- **Core Functionality**:
  - Hardware initialization via LibreHardwareMonitor
  - Sensor data collection and caching
  - Thread-safe hardware polling
  - Graceful error handling

**Key Components**:
- `UpdateVisitor`: Implements visitor pattern for hardware traversal
- Hardware caching for performance optimization
- Sensor value retrieval with null safety
- Memory management and disposal

**Supported Sensors**:
- CPU: Temperature, load percentage, fan speed
- GPU: Temperature, load percentage, fan speed
- Memory: Temperature estimation
- Storage: Individual disk temperatures

#### `LogHelper.cs`
**Purpose**: Centralized logging utility
- File-based logging with rotation
- Error logging with stack traces
- Thread-safe log operations
- Configurable log levels and formatting

### Configuration Files

#### `app.manifest`
**Purpose**: Windows application manifest for UAC and compatibility
- **Administrator Privileges**: Requests elevation for hardware access
- **DPI Awareness**: Enables high-DPI display support
- **Compatibility Settings**: Windows version compatibility declarations

#### `HardwareMonitorApp.csproj`
**Purpose**: .NET project configuration and dependencies
- **Target Framework**: .NET 8.0 with Windows-specific features
- **Package Dependencies**: LibreHardwareMonitorLib for hardware monitoring
- **Build Configuration**: Single-file publishing, icon settings
- **Metadata**: Version, author, repository information

#### `HwMon.sln`
**Purpose**: Visual Studio solution file
- Project organization and build configuration
- Developer environment setup

### Build & Development Files

#### `CompileAndRun.ps1`
**Purpose**: PowerShell build automation script
- **Functions**:
  - Automated project discovery and compilation
  - Release build generation
  - Administrator privilege execution
  - Error handling and user feedback

#### `.vscode/tasks.json`
**Purpose**: VS Code build task configuration
- Build, publish, and watch tasks
- Integration with VS Code task runner
- Development workflow automation

### GitHub Integration Files

#### `.github/workflows/build.yml`
**Purpose**: GitHub Actions CI/CD pipeline
- **Automated Testing**: Build verification on push/PR
- **Release Generation**: Automated artifact creation
- **Multi-trigger Support**: Push, PR, and release events
- **Windows-specific**: Uses windows-latest runner environment

#### `.github/ISSUE_TEMPLATE/bug_report.md`
**Purpose**: Structured bug report template
- Ensures consistent issue reporting
- Guides users to provide necessary information
- Includes sections for reproduction steps and system info

#### `.github/PULL_REQUEST_TEMPLATE.md`
**Purpose**: Pull request guidelines template
- Standardizes contribution process
- Ensures code quality checklist compliance
- Links changes to related issues

#### `CONTRIBUTING.md`
**Purpose**: Contributor guidelines and development setup
- Code style guidelines
- Development environment setup
- Contribution workflow explanation

### Resource Files

#### `HwMonitor.ico`
**Purpose**: Application icon
- Windows taskbar and window icon
- File association icon
- Application branding

#### `.gitignore`
**Purpose**: Git version control exclusions
- Build artifacts (bin/, obj/)
- IDE-specific files (.vs/, *.user)
- OS-generated files
- Temporary files and logs

#### `LICENSE`
**Purpose**: MIT license declaration
- Legal usage terms
- Copyright information
- Distribution permissions

#### `README.md`
**Purpose**: Project documentation and usage guide
- Feature overview and system requirements
- Installation and build instructions
- Troubleshooting guide
- Technical architecture summary

### Experimental/Alternative Implementation

#### `HwMonitor_Rust/` Directory
**Purpose**: Rust-based alternative implementation (experimental)
- `Cargo.toml`: Rust project configuration
- `src/main.rs`: Rust implementation entry point
- `target/`: Rust build artifacts
- **Note**: This appears to be an experimental rewrite and is not part of the main C# application

## Data Flow

### 1. Application Startup
```
App.xaml.cs → MainWindow.xaml.cs → SensorService initialization
```

### 2. Hardware Monitoring Loop
```
Timer Tick → SensorService.Update() → Hardware polling → UI Update
```

### 3. UI Update Flow
```
Background Thread → Sensor Collection → Dispatcher.Invoke → UI Element Updates
```

### 4. Error Handling Flow
```
Hardware Error → LogHelper.LogError → UI Status Display → Graceful Degradation
```

## Design Patterns

### 1. **Visitor Pattern**
- `UpdateVisitor` traverses hardware components
- Separates traversal logic from hardware structure
- Enables different update strategies (normal vs thorough)

### 2. **Singleton Pattern**
- `SensorService` manages single hardware monitoring instance
- Prevents resource conflicts and ensures consistency

### 3. **Observer Pattern**
- Timer-based polling with event-driven UI updates
- Loose coupling between monitoring and display

### 4. **Factory Pattern**
- Hardware component creation through LibreHardwareMonitor
- Abstraction of platform-specific hardware access

## Threading Model

### Main Thread (UI Thread)
- WPF UI updates and user interactions
- Timer event handling
- Error display and status updates

### Background Operations
- `Task.Run()` for sensor polling operations
- Thread-safe data collection
- `Dispatcher.Invoke()` for UI updates

### Thread Safety Measures
- Update locks prevent concurrent sensor operations
- Dispatcher ensures UI updates occur on main thread
- Exception handling prevents thread crashes

## Build System

### Development Build
```bash
dotnet build -c Debug
```

### Release Build
```bash
dotnet build -c Release
```

### Self-Contained Deployment
```bash
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

### PowerShell Automation
```powershell
.\CompileAndRun.ps1  # Automated build and run with admin privileges
```

## Performance Considerations

### Hardware Polling Optimization
- **Selective Updates**: Thorough updates only when necessary
- **Hardware Caching**: Reduces repeated hardware discovery
- **Error Resilience**: Failed sensor reads don't block other sensors

### UI Performance
- **Background Threading**: Sensor polling doesn't block UI
- **Efficient Updates**: Only changed values trigger UI updates
- **Resource Management**: Proper disposal of hardware monitoring resources

### Memory Management
- **Sensor Caching**: Prevents memory leaks from repeated hardware queries
- **Dispose Pattern**: Proper cleanup of unmanaged resources
- **GC Optimization**: Minimizes garbage collection pressure

## Error Handling Strategy

### Hardware Access Errors
- Graceful degradation when sensors are unavailable
- Retry mechanisms for transient failures
- User-friendly error messages

### UI Error Handling
- Non-blocking error display
- Continued operation despite partial sensor failures
- Logging of all errors for debugging

### Administrator Privilege Handling
- UAC elevation request through app.manifest
- Clear messaging about privilege requirements
- Fallback behavior for limited access scenarios

This architecture provides a robust, maintainable, and extensible foundation for hardware monitoring while ensuring good user experience and system stability.
