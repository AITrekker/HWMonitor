/*******************************************************************************
 * App.xaml.cs
 * 
 * Description:
 *     Application bootstrap class that initializes the HwMonitor application and
 *     sets up global exception handling and logging.
 * 
 * Key Features:
 *     - Global exception handling
 *     - Application startup management
 * 
 * Dependencies:
 *     - LogHelper for application logging
 *     - MainWindow for the main user interface
 ******************************************************************************/

using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;

namespace HardwareMonitorApp
{
    public partial class App : Application
    {
        // Constructor - configure exception handlers
        public App()
        {
            // Add global exception handling
            AppDomain.CurrentDomain.UnhandledException += (s, e) => 
                HandleException("Unhandled Exception", e.ExceptionObject as Exception);
                
            DispatcherUnhandledException += (s, e) => {
                HandleException("UI Exception", e.Exception);
                e.Handled = true;  // Prevent application crash
            };
            
            LogHelper.Log("Application starting");
        }
        
        // Handle exceptions in a centralized way
        private void HandleException(string context, Exception ex)
        {
            try
            {
                // Log the exception
                if (ex != null)
                {
                    LogHelper.LogError($"{context}: {ex.Message}", ex);
                }
                else
                {
                    LogHelper.Log($"{context}: Unknown error (not an Exception object)");
                }
                
                // Show user-friendly message
                string message = ex != null ? 
                    $"An error occurred: {ex.Message}" : 
                    "An unknown error occurred";
                    
                MessageBox.Show(
                    $"{message}\r\n\r\nCheck the log file for details.",
                    "Hardware Monitor Error", 
                    MessageBoxButton.OK, 
                    MessageBoxImage.Error);
            }
            catch
            {
                // Last-resort exception handling - nothing more we can do
            }
        }

        // Application startup logic
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            LogHelper.Log("Application starting up");
            
            try
            {
                // Create and show the main window
                MainWindow mainWindow = new MainWindow();
                mainWindow.Show();
                LogHelper.Log("Main window displayed");
            }
            catch (Exception ex)
            {
                HandleException("Startup Error", ex);
                Shutdown(1); // Exit with error code
            }
        }
    }
}