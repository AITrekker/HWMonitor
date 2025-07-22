#*******************************************************************************
# CompileAndRun.ps1
# 
# Description: Script to build and run the Hardware Monitor app with admin privileges
#*******************************************************************************

# Set working directory to script location
Set-Location $PSScriptRoot

# Build the application
Write-Host "Building application..." -ForegroundColor Cyan

try {
    # Find the project file
    $projectFile = Get-ChildItem -Path . -Filter "*.csproj" -Recurse | Select-Object -First 1
    
    if (-not $projectFile) {
        throw "No .csproj file found in the repository."
    }
    
    # Build the project
    dotnet build $projectFile.FullName -c Release
    
    if ($LASTEXITCODE -ne 0) {
        throw "Build failed with exit code $LASTEXITCODE"
    }
    
    Write-Host "Build successful." -ForegroundColor Green
    
    # Find the executable
    $executable = Get-ChildItem -Path ".\bin" -Filter "HardwareMonitorApp.exe" -Recurse | 
                  Where-Object { $_.DirectoryName -match "Release" } |
                  Select-Object -First 1
    
    if (-not $executable) {
        throw "Executable not found after successful build."
    }
    
    # Run with admin privileges
    Write-Host "Starting with administrator privileges: $($executable.Name)" -ForegroundColor Cyan
    Write-Host "NOTE: You may see 'Publisher: Unknown' because this is not code-signed." -ForegroundColor Yellow
    Start-Process -FilePath $executable.FullName -Verb RunAs
}
catch {
    Write-Host "Error: $_" -ForegroundColor Red
    exit 1
}