/*******************************************************************************
 * MainWindow.xaml.cs
 * 
 * Description:
 *     Main user interface for the HwMonitor application that displays real-time
 *     hardware metrics in textual format.
 * 
 * Key Features:
 *     - Real-time hardware sensor monitoring with 1-second refresh rate
 *     - Temperature, load tracking
 *     - Optional autostart with Windows
 * 
 * Dependencies:
 *     - SensorService for hardware data collection
 *******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Threading.Tasks;
using System.Windows.Threading;
using System.Security.Principal;
using System.IO;
using Microsoft.Web.WebView2.Core;
using System.Text.Json;

namespace HardwareMonitorApp
{
    public partial class MainWindow : Window
    {
        // Core services and components
        private SensorService _sensorService;
        private DispatcherTimer _updateTimer;
        private DateTime _lastUpdateTime = DateTime.Now;
        
        // Dashboard components
        private bool _isDashboardView = true;  // Start in dashboard view
        private bool _dashboardInitialized = false;
        
        // Thread safety
        private bool _updateInProgress = false;
        private readonly object _updateLock = new object();

        public MainWindow()
        {
            try
            {
                InitializeComponent();
                this.Loaded += MainWindow_Loaded;
                ShowView("LoadingView");
            }
            catch (Exception ex)
            {
                HandleError("Initialization", ex);
            }
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Initialize sensor service in background
            Task.Run(() => 
            {
                try
                {
                    _sensorService = new SensorService();
                    
                    Dispatcher.Invoke(() => 
                    {
                        UpdateAdminStatus();
                        ShowView("DashboardView");
                        InitializeDashboard();
                        InitializeUI();
                    });
                }
                catch (Exception ex)
                {
                    Dispatcher.Invoke(() => ShowErrorStatus(ex.Message));
                }
            });
        }
        
        // Show a specific view and hide others
        private void ShowView(string viewName)
        {
            if (FindName("NumericView") is Grid numericView) 
                numericView.Visibility = viewName == "NumericView" ? Visibility.Visible : Visibility.Collapsed;
            
            if (FindName("LoadingView") is Grid loadingView) 
                loadingView.Visibility = viewName == "LoadingView" ? Visibility.Visible : Visibility.Collapsed;
                
            if (FindName("DashboardView") is Grid dashboardView) 
                dashboardView.Visibility = viewName == "DashboardView" ? Visibility.Visible : Visibility.Collapsed;
        }
        
        // Initialize UI components
        private void InitializeUI()
        {
            // Setup update timer
            _updateTimer = new DispatcherTimer(DispatcherPriority.Normal);
            _updateTimer.Interval = TimeSpan.FromMilliseconds(950);
            _updateTimer.Tick += UpdateSensors;
            
            // Initial update and start timer
            UpdateSensors(null, null);
            _updateTimer.Start();
            
            // Setup watchdog timer
            var watchdogTimer = new System.Threading.Timer(
                _ => CheckForMissedUpdates(),
                null,
                TimeSpan.FromSeconds(2),
                TimeSpan.FromSeconds(1));
        }
        
        // Update sensor data and UI
        private async void UpdateSensors(object sender, EventArgs e)
        {
            if (!AcquireUpdateLock())
                return;
            
            try
            {
                await Task.Run(() =>
                {
                    try
                    {
                        var cpuTemp = _sensorService.GetCpuTemperature();
                        var gpuTemp = _sensorService.GetGpuTemperature();
                        var memoryTemp = _sensorService.GetMemoryTemperature();
                        var cpuLoad = _sensorService.GetCpuLoad();
                        var gpuLoad = _sensorService.GetGpuLoad();
                        var diskTemps = _sensorService.GetDiskTemperatures();
                        
                        UpdateUI(cpuTemp, gpuTemp, memoryTemp, cpuLoad, gpuLoad, diskTemps);
                    }
                    catch (Exception ex)
                    {
                        Dispatcher.Invoke(() => ShowErrorStatus($"Sensor update error: {ex.Message}"));
                    }
                });
                
                _lastUpdateTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                HandleError("UpdateSensors", ex);
            }
            finally
            {
                ReleaseUpdateLock();
            }
        }
        
        // Thread safety helpers
        private bool AcquireUpdateLock()
        {
            lock (_updateLock)
            {
                if (_updateInProgress) return false;
                _updateInProgress = true;
                return true;
            }
        }
        
        private void ReleaseUpdateLock()
        {
            lock (_updateLock)
            {
                _updateInProgress = false;
            }
        }
        
        // Update UI with sensor data
        private void UpdateUI(float? cpuTemp, float? gpuTemp, float? memoryTemp,
                             float? cpuLoad, float? gpuLoad,
                             Dictionary<string, float?> diskTemps)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateTextValue("CpuTempText", cpuTemp, "{0:F1} °C");
                UpdateTextValue("GpuTempText", gpuTemp, "{0:F1} °C");
                UpdateTextValue("MemoryTempText", memoryTemp, "{0:F1} °C");
                UpdateTextValue("CpuLoadText", cpuLoad, "{0:F1} %");
                UpdateTextValue("GpuLoadText", gpuLoad, "{0:F1} %");
                UpdateItemsControl("DiskTempsItemsControl", diskTemps, "{0:F1} °C");
                
                // Send data to dashboard if it's active
                SendDataToDashboard(cpuTemp, gpuTemp, memoryTemp, cpuLoad, gpuLoad, diskTemps);
            });
        }
        
        private void UpdateTextValue(string elementName, float? value, string format)
        {
            if (FindName(elementName) is TextBlock textBlock)
                textBlock.Text = value.HasValue ? string.Format(format, value.Value) : "N/A";
        }
        
        private void UpdateItemsControl(string elementName, Dictionary<string, float?> values, string format)
        {
            if (FindName(elementName) is ItemsControl itemsControl)
            {
                // Get existing items to preserve them if they're not in the new values
                var existingItems = itemsControl.ItemsSource as IEnumerable<KeyValuePair<string, string>>;
                var existingDict = existingItems?.ToDictionary(pair => pair.Key, pair => pair.Value) ?? new Dictionary<string, string>();
                
                // Update existing values and add new ones
                foreach (var item in values)
                {
                    existingDict[item.Key] = item.Value.HasValue ? string.Format(format, item.Value.Value) : "N/A";
                }
                
                // Set the updated collection as the ItemsSource
                itemsControl.ItemsSource = existingDict.Select(pair => 
                    new KeyValuePair<string, string>(pair.Key, pair.Value));
            }
        }
        
        // Watchdog to ensure updates happen regularly
        private void CheckForMissedUpdates()
        {
            if (DateTime.Now - _lastUpdateTime > TimeSpan.FromSeconds(1.5))
            {
                Dispatcher.Invoke(() =>
                {
                    _updateTimer?.Stop();
                    _updateTimer?.Start();
                    UpdateSensors(null, null);
                });
            }
        }
        
        // Admin status display
        private void UpdateAdminStatus()
        {
            if (FindName("AdminStatusText") is TextBlock adminStatusText)
            {
                bool isAdmin = IsRunningAsAdministrator();
                adminStatusText.Text = isAdmin 
                    ? "Running with administrator privileges" 
                    : "WARNING: Not running with administrator privileges";
                adminStatusText.Foreground = isAdmin 
                    ? System.Windows.Media.Brushes.Green 
                    : System.Windows.Media.Brushes.Red;
            }
        }
        
        // Show error status in UI
        private void ShowErrorStatus(string message)
        {
            ShowView("NumericView");
            
            if (FindName("AdminStatusText") is TextBlock statusText)
            {
                statusText.Text = $"ERROR: {message}";
                statusText.Foreground = System.Windows.Media.Brushes.Red;
            }
            
            MessageBox.Show($"Error initializing hardware sensors: {message}\n\n" +
                            "The application may have limited functionality.",
                            "Hardware Monitor Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            
            InitializeUI();
        }
        
        // Cleanup when window is closed
        protected override void OnClosed(EventArgs e)
        {
            _updateTimer?.Stop();
            _sensorService?.Dispose();
            base.OnClosed(e);
        }
        
        // Check admin privileges
        private bool IsRunningAsAdministrator()
        {
            try
            {
                WindowsIdentity identity = WindowsIdentity.GetCurrent();
                WindowsPrincipal principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
        
        // Centralized error handling
        private void HandleError(string context, Exception ex)
        {
            LogHelper.LogError($"{context}: {ex.Message}", ex);
        }

        // Dashboard view toggle
        private void ToggleView_Click(object sender, RoutedEventArgs e)
        {
            _isDashboardView = !_isDashboardView;
            
            if (_isDashboardView)
            {
                ShowView("DashboardView");
                ToggleViewButton.Content = "📋 Classic View";
                if (!_dashboardInitialized)
                {
                    InitializeDashboard();
                }
            }
            else
            {
                ShowView("NumericView");
                ToggleViewButton.Content = "📊 Dashboard View";
                this.WindowState = WindowState.Normal;
            }
        }

        // Initialize the dashboard WebView2
        private void InitializeDashboard()
        {
            if (_dashboardInitialized) return;
            
            try
            {
                var dashboardPath = Path.Combine(AppContext.BaseDirectory, "dashboard.html");
                if (File.Exists(dashboardPath))
                {
                    DashboardWebView.Source = new Uri($"file:///{dashboardPath.Replace('\\', '/')}");
                    _dashboardInitialized = true;
                }
                else
                {
                    LogHelper.LogError("Dashboard file not found", new FileNotFoundException($"Could not find dashboard.html at {dashboardPath}"));
                }
            }
            catch (Exception ex)
            {
                HandleError("Dashboard initialization", ex);
            }
        }

        // Handle WebView2 navigation completion
        private async void DashboardWebView_NavigationCompleted(object sender, CoreWebView2NavigationCompletedEventArgs e)
        {
            if (e.IsSuccess && _dashboardInitialized)
            {
                LogHelper.Log("Dashboard WebView2 navigation completed successfully");
                
                // Wait a moment for the page to fully load
                await Task.Delay(1000);
                
                // Add JavaScript for WebView2 message handling
                await DashboardWebView.CoreWebView2.AddScriptToExecuteOnDocumentCreatedAsync(@"
                    console.log('C# script injection successful');
                    
                    window.chrome.webview.addEventListener('message', function(event) {
                        console.log('Message received from C#:', event.data);
                        try {
                            let messageData = event.data;
                            if (typeof messageData === 'string') {
                                messageData = JSON.parse(messageData);
                            }
                            
                            if (messageData.type === 'sensorData') {
                                // Update the global hardware monitor instance with real data
                                if (window.hardwareMonitorInstance) {
                                    const data = messageData.data;
                                    data.timestamp = new Date(data.timestamp);
                                    window.hardwareMonitorInstance.updateUI(data);
                                } else {
                                    console.error('hardwareMonitorInstance not found');
                                }
                            }
                        } catch (error) {
                            console.error('Error processing message:', error);
                        }
                    });
                ");
                
                LogHelper.Log("Dashboard JavaScript injection completed");
            }
            else if (!e.IsSuccess)
            {
                LogHelper.LogError("Dashboard navigation failed", new Exception($"Navigation failed for WebView2"));
            }
        }

        // Send sensor data to dashboard
        private void SendDataToDashboard(float? cpuTemp, float? gpuTemp, float? memoryTemp,
                                         float? cpuLoad, float? gpuLoad,
                                         Dictionary<string, float?> diskTemps)
        {
            if (!_isDashboardView || !_dashboardInitialized)
                return;

            try
            {
                // Ensure WebView2 is ready
                if (DashboardWebView.CoreWebView2 == null)
                {
                    LogHelper.Log("WebView2 CoreWebView2 is null, skipping data update");
                    return;
                }

                var data = new
                {
                    type = "sensorData",
                    data = new
                    {
                        cpuTemp = cpuTemp,
                        cpuLoad = cpuLoad,
                        gpuTemp = gpuTemp,
                        gpuLoad = gpuLoad,
                        memoryTemp = memoryTemp,
                        diskTemps = diskTemps?.ToDictionary(kvp => kvp.Key, kvp => kvp.Value),
                        timestamp = DateTime.Now.ToString("o")
                    }
                };

                var json = JsonSerializer.Serialize(data);
                LogHelper.Log($"Sending data to dashboard: {json}");
                DashboardWebView.CoreWebView2.PostWebMessageAsString(json);
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Dashboard data update", ex);
            }
        }
    }
}
