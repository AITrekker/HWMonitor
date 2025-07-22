use eframe::egui;
use sysinfo::{System, CpuExt, ComponentExt};

struct HwMonitorApp {
    system: System,
    cpu_temp: Option<f32>,
    cpu_load: Option<f32>,
    // TODO: Add fields for GPU temp/load, memory temp, disk temps
}

impl Default for HwMonitorApp {
    fn default() -> Self {
        let mut sys = System::new_all();
        sys.refresh_all(); // Initial refresh of all system information

        Self {
            system: sys,
            cpu_temp: None,
            cpu_load: None,
        }
    }
}

impl eframe::App for HwMonitorApp {
    fn update(&mut self, ctx: &egui::Context, _frame: &mut eframe::Frame) {
        self.system.refresh_cpu();
        self.system.refresh_memory(); // Good to refresh memory info
        self.system.refresh_components_list(); // Refresh the list of components
        self.system.refresh_disks_list(); // Refresh the list of disks

        // --- CPU Info ---
        let cpus = self.system.cpus();
        if !cpus.is_empty() {
            let total_load: f32 = cpus.iter().map(|cpu| cpu.cpu_usage()).sum();
            self.cpu_load = Some(total_load / cpus.len() as f32);
        }

        self.cpu_temp = self.system.components().iter()
            .find(|comp| comp.label().to_lowercase().contains("cpu") && comp.temperature() > 0.0)
            .map(|comp| comp.temperature());
        
        // --- UI Rendering ---
        egui::CentralPanel::default().show(ctx, |ui| {
            ui.heading("Hardware Monitor (Rust Egui)");
            ui.separator();

            ui.label(format!("CPU Temperature: {:.1}°C", self.cpu_temp.unwrap_or(0.0)));
            ui.label(format!("CPU Load: {:.1}%", self.cpu_load.unwrap_or(0.0)));
            
            ui.separator();
            ui.label("GPU Temperature: N/A");
            ui.label("GPU Load: N/A");
            ui.label("Memory Temperature: N/A");
            ui.separator();
            ui.label("Disk Temperatures:");
            
            let disks = self.system.disks();
            if disks.is_empty() {
                ui.label("  No disks found.");
            } else {
                for disk in disks {
                    ui.label(format!("  {}: {} (Type: {:?})", 
                        disk.name().to_string_lossy(), 
                        disk.mount_point().to_string_lossy(),
                        disk.kind()
                    ));
                }
            }
            
            ctx.request_repaint_after(std::time::Duration::from_millis(950));
        });
    }
}

fn main() -> Result<(), eframe::Error> {
    let options = eframe::NativeOptions {
        viewport: egui::ViewportBuilder::default().with_inner_size([380.0, 500.0]),
        ..Default::default()
    };
    eframe::run_native(
        "Hardware Monitor App",
        options,
        Box::new(|_cc| Box::<HwMonitorApp>::default()),
    )
} 