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

namespace HardwareMonitorApp
{
    public partial class MainWindow : Window
    {
        // Core services and components
        private SensorService _sensorService;
        private DispatcherTimer _updateTimer;
        private DateTime _lastUpdateTime = DateTime.Now;
        
        // Constants
        
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
                        ShowView("NumericView");
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
    }
}
