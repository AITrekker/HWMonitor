/*******************************************************************************
 * SimplifiedSensorService.cs
 * 
 * Description:
 *     A streamlined version of the hardware monitoring service for HwMonitor.
 *******************************************************************************/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using LibreHardwareMonitor.Hardware;

namespace HardwareMonitorApp
{
    public class UpdateVisitor : IVisitor
    {
        private readonly bool _thoroughUpdate;
        
        public UpdateVisitor(bool thoroughUpdate = false) => _thoroughUpdate = thoroughUpdate;
        
        public void VisitComputer(IComputer computer) => computer.Traverse(this);
        
        public void VisitHardware(IHardware hardware) 
        {
            try 
            { 
                hardware.Update();
                if (_thoroughUpdate && (hardware.HardwareType == HardwareType.Motherboard || 
                                       hardware.HardwareType == HardwareType.Cpu ||
                                       hardware.HardwareType.ToString().Contains("Gpu")))
                {
                    hardware.Update();
                }
            }
            catch { /* Ignore errors */ }
            
            foreach (var subHardware in hardware.SubHardware) 
                subHardware.Accept(this);
        }
        
        public void VisitSensor(ISensor sensor) { }
        public void VisitParameter(IParameter parameter) { }
    }
    
    public class SensorService : IDisposable
    {
        private readonly Computer _computer;
        private readonly object _lock = new object();
        private bool _isUpdating = false;
        private DateTime _lastUpdateTime = DateTime.MinValue;
        private bool _disposed = false;
        private Dictionary<HardwareType, IHardware> _hardwareCache = new Dictionary<HardwareType, IHardware>();
        
        public SensorService()
        {
            _computer = new Computer 
            { 
                IsCpuEnabled = true, 
                IsGpuEnabled = true, 
                IsStorageEnabled = true, 
                IsMemoryEnabled = true,
                IsMotherboardEnabled = true 
            };
            
            try
            {
                _computer.Open();
                
                // Do a basic update now
                Update(true);
                
                // Schedule a comprehensive scan after system is fully initialized
                Task.Delay(2000).ContinueWith(_ => 
                {
                    for (int i = 0; i < 3; i++) // Try multiple scans
                    {
                        Update(true);
                        
                        Task.Delay(1000).Wait();
                    }
                });
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Initialization error", ex);
            }
        }
        
        public bool Update(bool forceThorough = false)
        {
            lock (_lock)
            {
                if (_isUpdating || (!forceThorough && DateTime.Now - _lastUpdateTime < TimeSpan.FromMilliseconds(750)))
                    return false;
                _isUpdating = true;
            }
            
            try
            {
                bool thorough = forceThorough || (DateTime.Now - _lastUpdateTime > TimeSpan.FromSeconds(1));
                _computer.Accept(new UpdateVisitor(thorough));
                
                // Only clear cache if we haven't completed initial sensor scan
                if (thorough) 
                    _hardwareCache.Clear();
                    
                _lastUpdateTime = DateTime.Now;
                return true;
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Update failed", ex);
                return false;
            }
            finally
            {
                lock (_lock) { _isUpdating = false; }
            }
        }
        
        private IHardware GetHardware(HardwareType type)
        {
            if (_hardwareCache.TryGetValue(type, out var hardware))
                return hardware;
                
            hardware = _computer.Hardware.FirstOrDefault(h => h.HardwareType == type);
            if (hardware != null) _hardwareCache[type] = hardware;
            return hardware;
        }
        
        private float? GetSensorValue(HardwareType type, SensorType sensorType, string nameFilter = null)
        {
            try
            {
                var hardware = GetHardware(type);
                if (hardware == null) return null;
                
                var query = hardware.Sensors.Where(s => s.SensorType == sensorType);
                if (!string.IsNullOrEmpty(nameFilter))
                    query = query.Where(s => s.Name.Contains(nameFilter));
                    
                return query.FirstOrDefault()?.Value;
            }
            catch { return null; }
        }
        
        // Basic metrics
        public float? GetCpuTemperature() 
        {
            Update();
            return GetSensorValue(HardwareType.Cpu, SensorType.Temperature);
        }
            
        public float? GetCpuLoad() => GetSensorValue(HardwareType.Cpu, SensorType.Load, "Total");
        
        public float? GetGpuTemperature() => 
            GetSensorValue(HardwareType.GpuNvidia, SensorType.Temperature) ?? 
            GetSensorValue(HardwareType.GpuAmd, SensorType.Temperature);
            
        public float? GetGpuLoad() => 
            GetSensorValue(HardwareType.GpuNvidia, SensorType.Load, "Core") ?? 
            GetSensorValue(HardwareType.GpuAmd, SensorType.Load);
        
        public float? GetGpuFanSpeed()
        {
            Update(true);
            return GetSensorValue(HardwareType.GpuNvidia, SensorType.Fan) ?? 
                   GetSensorValue(HardwareType.GpuAmd, SensorType.Fan);
        }
        
        public float? GetMemoryTemperature()
        {
            try
            {
                Update();
                
                // Try memory hardware
                var memory = _computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.Memory);
                if (memory != null)
                {
                    var temp = memory.Sensors.FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value;
                    if (temp.HasValue) return temp;
                }
                
                // Try motherboard memory sensors
                var motherboard = GetHardware(HardwareType.Motherboard);
                if (motherboard != null)
                {
                    foreach (var hardware in new[] { motherboard }.Concat(motherboard.SubHardware))
                    {
                        var temp = hardware.Sensors
                            .FirstOrDefault(s => s.SensorType == SensorType.Temperature && 
                                     (s.Name.Contains("Memory") || s.Name.Contains("RAM")))?.Value;
                        if (temp.HasValue) return temp;
                    }
                }
                
                // Fallback: approximate from CPU
                var cpuTemp = GetCpuTemperature();
                return cpuTemp.HasValue ? cpuTemp.Value - 5 : null;
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Memory temp error", ex);
                return null;
            }
        }
        
        public Dictionary<string, float?> GetDiskTemperatures()
        {
            var result = new Dictionary<string, float?>();
            var counter = new Dictionary<string, int>();
            
            try
            {
                Update();
                foreach (var storage in _computer.Hardware.Where(h => h.HardwareType == HardwareType.Storage))
                {
                    string name = storage.Name;
                    if (counter.ContainsKey(name))
                    {
                        counter[name]++;
                        name = $"{name} #{counter[name]}";
                    }
                    else counter[name] = 1;
                    
                    result[name] = storage.Sensors
                        .FirstOrDefault(s => s.SensorType == SensorType.Temperature)?.Value;
                }
            }
            catch (Exception ex)
            {
                LogHelper.LogError("Disk temps error", ex);
            }
            
            return result;
        }

        private float? GetSafeValue(ISensor sensor)
        {
            if (sensor == null) return null;
            
            try
            {
                return sensor.Value;
            }
            catch
            {
                return null;
            }
        }
        
        public void Dispose()
        {
            if (!_disposed)
            {
                _computer?.Close();
                _disposed = true;
            }
            GC.SuppressFinalize(this);
        }
    }
}
