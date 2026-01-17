import serial
import time
import json
import os
import threading
from datetime import datetime
import tkinter as tk
from tkinter import ttk, messagebox
import winsound
import subprocess

# ============================================
# KONFIGURASI
# ============================================
SERIAL_PORT = 'COM3'           # Ganti dengan port CH341SER Anda
BAUD_RATE = 115200

# Multiple alarm file options
ALARM_FILES = [
    r"C:\HospitalAlarmApp\HospitalAlarmApp\alarm.wav",
    r"C:\Windows\Media\Alarm01.wav",
    r"C:\Windows\Media\Alarm02.wav",
    r"C:\Windows\Media\Alarm03.wav",
    r"C:\Windows\Media\Alarm04.wav",
    r"C:\Windows\Media\Alarm05.wav",
    r"C:\Windows\Media\Alarm06.wav",
    r"C:\Windows\Media\Alarm07.wav",
    r"C:\Windows\Media\Alarm08.wav",
    r"C:\Windows\Media\Alarm09.wav",
    r"C:\Windows\Media\Alarm10.wav",
    r"C:\Windows\Media\ringout.wav",
    r"C:\Windows\Media\notify.wav"
]

# Cari file alarm yang tersedia
def find_alarm_file():
    for file in ALARM_FILES:
        if os.path.exists(file):
            print(f"✅ Found alarm file: {file}")
            return file
    print("⚠ No alarm file found, will use system beep")
    return None

ALARM_FILE = find_alarm_file()

# ============================================
# KELAS UTAMA PC ALARM CONTROLLER (FIXED)
# ============================================
class PCAlarmController:
    def __init__(self):
        self.serial_conn = None
        self.running = False
        self.current_alarm = None
        self.alarm_active = False
        self.sound_process = None
        
        # GUI Setup
        self.setup_gui()
        
    def setup_gui(self):
        """Setup GUI untuk monitoring"""
        self.root = tk.Tk()
        self.root.title("Hospital Alarm PC Controller - FIXED")
        self.root.geometry("900x700")
        
        # Style
        style = ttk.Style()
        style.theme_use('clam')
        
        # Header
        header_frame = ttk.Frame(self.root, padding="20")
        header_frame.grid(row=0, column=0, sticky=(tk.W, tk.E))
        
        ttk.Label(header_frame, text="🏥 HOSPITAL EMERGENCY ALARM SYSTEM", 
                 font=("Arial", 20, "bold")).grid(row=0, column=0)
        ttk.Label(header_frame, text="FIXED VERSION - Handles all ESP8266 formats",
                 font=("Arial", 12)).grid(row=1, column=0)
        
        # Status Frame
        status_frame = ttk.LabelFrame(self.root, text="System Status", padding="10")
        status_frame.grid(row=1, column=0, padx=20, pady=10, sticky=(tk.W, tk.E))
        
        self.status_label = ttk.Label(status_frame, text="⚪ Disconnected", font=("Arial", 12))
        self.status_label.grid(row=0, column=0, sticky=tk.W)
        
        self.connection_label = ttk.Label(status_frame, text="Port: Not connected")
        self.connection_label.grid(row=0, column=1, sticky=tk.W, padx=50)
        
        # Alarm Status Frame
        alarm_status_frame = ttk.LabelFrame(self.root, text="Active Emergency", padding="15")
        alarm_status_frame.grid(row=2, column=0, padx=20, pady=10, sticky=(tk.W, tk.E))
        
        self.alarm_icon = ttk.Label(alarm_status_frame, text="🔴", font=("Arial", 40))
        self.alarm_icon.grid(row=0, column=0, rowspan=2, padx=20)
        
        self.alarm_text = ttk.Label(alarm_status_frame, text="NO ACTIVE ALARM", 
                                   font=("Arial", 18, "bold"), foreground="green")
        self.alarm_text.grid(row=0, column=1, sticky=tk.W)
        
        self.alarm_details = ttk.Label(alarm_status_frame, text="System Ready", font=("Arial", 12))
        self.alarm_details.grid(row=1, column=1, sticky=tk.W)
        
        # Device Info Frame
        device_frame = ttk.LabelFrame(self.root, text="Connected Device Info", padding="10")
        device_frame.grid(row=3, column=0, padx=20, pady=10, sticky=(tk.W, tk.E))
        
        self.device_id_label = ttk.Label(device_frame, text="Device ID: Not connected")
        self.device_id_label.grid(row=0, column=0, sticky=tk.W)
        
        self.patient_label = ttk.Label(device_frame, text="Patient: Unknown")
        self.patient_label.grid(row=0, column=1, sticky=tk.W, padx=50)
        
        self.room_label = ttk.Label(device_frame, text="Room: Unknown")
        self.room_label.grid(row=0, column=2, sticky=tk.W)
        
        # Control Buttons
        control_frame = ttk.Frame(self.root)
        control_frame.grid(row=4, column=0, pady=20)
        
        ttk.Button(control_frame, text="🔊 Test Alarm", 
                  command=self.test_alarm, width=15).grid(row=0, column=0, padx=5)
        ttk.Button(control_frame, text="⏹️ Stop Alarm", 
                  command=self.stop_alarm, width=15).grid(row=0, column=1, padx=5)
        ttk.Button(control_frame, text="🔄 Reconnect", 
                  command=self.connect_serial, width=15).grid(row=0, column=2, padx=5)
        ttk.Button(control_frame, text="📊 View Log", 
                  command=self.show_log, width=15).grid(row=0, column=3, padx=5)
        ttk.Button(control_frame, text="🛑 Emergency Stop", 
                  command=self.emergency_stop, width=15).grid(row=0, column=4, padx=5)
        
        # Log Frame
        log_frame = ttk.LabelFrame(self.root, text="Serial Monitor & Log", padding="10")
        log_frame.grid(row=5, column=0, padx=20, pady=10, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # Log Text dengan Scrollbar
        log_container = ttk.Frame(log_frame)
        log_container.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        self.log_text = tk.Text(log_container, height=15, width=100, font=("Consolas", 10))
        self.log_text.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        scrollbar = ttk.Scrollbar(log_container, orient=tk.VERTICAL, command=self.log_text.yview)
        scrollbar.grid(row=0, column=1, sticky=(tk.N, tk.S))
        self.log_text['yscrollcommand'] = scrollbar.set
        
        # Button untuk clear log
        ttk.Button(log_frame, text="Clear Log", command=self.clear_log).grid(row=1, column=0, pady=5)
        
        # Configure grid weights
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(5, weight=1)
        log_container.columnconfigure(0, weight=1)
        log_container.rowconfigure(0, weight=1)
        
        # Bind close event
        self.root.protocol("WM_DELETE_WINDOW", self.on_closing)
        
        # Set icon
        self.set_window_icon()
    
    def set_window_icon(self):
        """Set window icon (if available)"""
        try:
            self.root.iconbitmap(r"C:\HospitalAlarmApp\icon.ico")
        except:
            pass
    
    def log_message(self, message, color="black"):
        """Menambahkan pesan ke log dengan warna"""
        timestamp = datetime.now().strftime("%H:%M:%S.%f")[:-3]
        
        # Insert dengan tag untuk warna
        self.log_text.insert(tk.END, f"[{timestamp}] {message}\n")
        
        # Apply color tag
        if color != "black":
            self.log_text.tag_add(color, f"end-2l", "end-1l")
            self.log_text.tag_config(color, foreground=color)
        
        self.log_text.see(tk.END)  # Auto-scroll
        
        # Juga print ke console
        print(f"[{timestamp}] {message}")
    
    def clear_log(self):
        """Membersihkan log"""
        self.log_text.delete(1.0, tk.END)
        self.log_message("Log cleared", "blue")
    
    def connect_serial(self):
        """Membuka koneksi serial ke ESP8266"""
        ports = self.find_serial_ports()
        
        if not ports:
            self.log_message("❌ No serial ports found!", "red")
            messagebox.showerror("Error", "No serial ports detected!\nPlease connect ESP8266 via USB.")
            return
        
        # Coba semua port yang tersedia
        for port in ports:
            try:
                self.log_message(f"🔍 Trying to connect to {port}...", "blue")
                
                if self.serial_conn and self.serial_conn.is_open:
                    self.serial_conn.close()
                
                self.serial_conn = serial.Serial(
                    port=port,
                    baudrate=BAUD_RATE,
                    timeout=1,
                    write_timeout=1
                )
                
                time.sleep(2)  # Tunggu koneksi stabil
                
                # Clear buffer
                self.serial_conn.reset_input_buffer()
                self.serial_conn.reset_output_buffer()
                
                # Test connection
                self.serial_conn.write(b"PC_PING\n")
                time.sleep(0.5)
                
                self.running = True
                self.status_label.config(text=f"🟢 Connected to {port}")
                self.connection_label.config(text=f"Port: {port} | Baud: {BAUD_RATE}")
                
                self.log_message(f"✅ Successfully connected to {port}", "green")
                self.log_message(f"📡 Listening for ESP8266 commands...", "blue")
                
                # Start serial listener thread
                serial_thread = threading.Thread(target=self.serial_listener, daemon=True)
                serial_thread.start()
                
                # Send acknowledgment
                self.send_to_esp("PC_CONTROLLER_READY")
                
                return
                
            except Exception as e:
                self.log_message(f"Failed to connect to {port}: {str(e)[:50]}...", "orange")
                continue
        
        self.status_label.config(text="🔴 Connection Failed")
        self.log_message("❌ Could not connect to any serial port!", "red")
    
    def find_serial_ports(self):
        """Mencari port serial yang tersedia"""
        import sys
        ports = []
        
        if sys.platform.startswith('win'):
            # Windows
            for i in range(1, 21):  # Check COM1 to COM20
                port = f"COM{i}"
                try:
                    s = serial.Serial(port)
                    s.close()
                    ports.append(port)
                except:
                    pass
        else:
            # Linux/Mac
            import glob
            ports = glob.glob('/dev/ttyUSB*') + glob.glob('/dev/ttyACM*')
        
        return ports
    
    def serial_listener(self):
        """Thread untuk membaca data dari serial"""
        buffer = ""
        in_handshake = False
        handshake_data = {}
        
        while self.running and self.serial_conn and self.serial_conn.is_open:
            try:
                if self.serial_conn.in_waiting > 0:
                    # Baca semua data yang tersedia
                    raw_data = self.serial_conn.read(self.serial_conn.in_waiting)
                    
                    try:
                        data = raw_data.decode('utf-8', errors='replace')
                        buffer += data
                        
                        # Proses per baris
                        while '\n' in buffer:
                            line, buffer = buffer.split('\n', 1)
                            line = line.strip()
                            
                            if line:
                                self.process_serial_line(line, handshake_data)
                                
                                # Handle handshake parsing
                                if line == "=== HANDSHAKE ===":
                                    in_handshake = True
                                    handshake_data = {}
                                elif line == "=== END_HANDSHAKE ===":
                                    in_handshake = False
                                    self.handle_complete_handshake(handshake_data)
                                elif in_handshake and ':' in line:
                                    key, value = line.split(':', 1)
                                    handshake_data[key.strip()] = value.strip()
                                    
                    except UnicodeDecodeError:
                        self.log_message("⚠ Received non-UTF8 data", "orange")
                        
            except Exception as e:
                self.log_message(f"Serial error: {e}", "red")
                time.sleep(1)
            
            time.sleep(0.01)
    
    def process_serial_line(self, line, handshake_data):
        """Memproses satu baris data dari ESP8266"""
        # Log semua data yang diterima
        self.log_message(f"📥 {line}", "purple")
        
        # ============================================
        # DETEKSI PERINTAH DARI ESP8266
        # ============================================
        
        # 1. Emergency Alarm (Format sederhana)
        if line == "!ALARM_START!" or "EMERGENCY" in line.upper():
            self.handle_emergency_start("Button Press", handshake_data)
            return
        
        # 2. Emergency Alarm (JSON format)
        if line.startswith('{') and line.endswith('}'):
            try:
                data = json.loads(line)
                if data.get("command") == "PLAY_ALARM":
                    self.handle_emergency_start("JSON Command", data)
                    return
            except json.JSONDecodeError:
                pass
        
        # 3. Cancel Alarm
        if line == "!ALARM_STOP!" or "CANCEL" in line.upper() or "STOP" in line.upper():
            self.handle_emergency_stop()
            return
        
        # 4. Custom Command
        if line.startswith("CMD:") or line.startswith("PLAY_SOUND"):
            self.handle_custom_command(line)
            return
        
        # 5. Status Update
        if line.startswith("STATUS:"):
            self.update_status_from_device(line)
            return
        
        # 6. Info messages
        if any(keyword in line for keyword in ["✅", "🚨", "🔘", "📤", "🤝"]):
            # Hanya log, tidak perlu action
            return
        
        # 7. Jika tidak dikenali, coba parsing sebagai emergency trigger
        emergency_keywords = ["BUTTON PRESSED", "EMERGENCY BUTTON", "ALARM", "TRIGGER"]
        if any(keyword in line.upper() for keyword in emergency_keywords):
            self.handle_emergency_start("Auto-detected", {"message": line})
    
    def handle_complete_handshake(self, handshake_data):
        """Menangani data handshake yang lengkap"""
        self.log_message("🤝 Handshake completed!", "green")
        
        # Update device info di GUI
        device_id = handshake_data.get("DEVICE_ID", "UNKNOWN")
        patient = handshake_data.get("PATIENT", "Unknown")
        room = handshake_data.get("ROOM", "Unknown")
        
        self.device_id_label.config(text=f"Device ID: {device_id}")
        self.patient_label.config(text=f"Patient: {patient}")
        self.room_label.config(text=f"Room: {room}")
        
        self.log_message(f"Device: {device_id}, Patient: {patient}, Room: {room}", "blue")
        
        # Kirim acknowledgment
        self.send_to_esp("HANDSHAKE_ACK")
    
    def handle_emergency_start(self, source, data):
        """Menangani emergency alarm dari berbagai sumber"""
        if self.alarm_active:
            self.log_message("⚠ Alarm already active, ignoring duplicate", "orange")
            return
        
        self.alarm_active = True
        
        # Update GUI
        self.alarm_icon.config(text="🚨", foreground="red")
        self.alarm_text.config(text="EMERGENCY ALARM ACTIVE!", foreground="red")
        
        # Create alarm details
        timestamp = datetime.now().strftime("%H:%M:%S")
        
        if isinstance(data, dict):
            patient = data.get("patient", data.get("PATIENT", "Unknown Patient"))
            room = data.get("room", data.get("ROOM", "Unknown Room"))
            device = data.get("device_id", data.get("DEVICE_ID", "Unknown"))
        else:
            patient = self.patient_label.cget("text").replace("Patient: ", "")
            room = self.room_label.cget("text").replace("Room: ", "")
            device = self.device_id_label.cget("text").replace("Device ID: ", "")
        
        alarm_details = f"Time: {timestamp} | Source: {source}\n"
        alarm_details += f"Patient: {patient} | Room: {room}\n"
        alarm_details += f"Device: {device}"
        
        self.alarm_details.config(text=alarm_details)
        
        # Log
        self.log_message(f"🚨 EMERGENCY ALARM ACTIVATED! Source: {source}", "red")
        self.log_message(f"   Patient: {patient}, Room: {room}", "red")
        
        # Play alarm sound
        self.play_alarm_advanced()
        
        # Kirim acknowledgment ke ESP8266
        self.send_to_esp("ALARM_ACKNOWLEDGED")
        
        # Show notification
        self.show_emergency_notification(patient, room)
        
        # Flash window
        self.flash_window()
    
    def handle_emergency_stop(self):
        """Menangani pembatalan emergency"""
        if not self.alarm_active:
            self.log_message("⚠ No active alarm to stop", "orange")
            return
        
        self.alarm_active = False
        
        # Update GUI
        self.alarm_icon.config(text="✅", foreground="green")
        self.alarm_text.config(text="NO ACTIVE ALARM", foreground="green")
        self.alarm_details.config(text="System Ready")
        
        # Log
        self.log_message("✅ EMERGENCY ALARM STOPPED", "green")
        
        # Stop alarm sound
        self.stop_alarm_sound()
        
        # Kirim acknowledgment
        self.send_to_esp("ALARM_STOPPED_ACK")
    
    def handle_custom_command(self, command):
        """Menangani custom command"""
        self.log_message(f"🔧 Custom command: {command}", "blue")
        
        # Parse command
        if "PLAY_SOUND" in command or "ALARM" in command.upper():
            self.handle_emergency_start("Custom Command", {})
    
    def update_status_from_device(self, status_line):
        """Update status dari device"""
        self.log_message(f"📊 Device status: {status_line}", "blue")
    
    def play_alarm_advanced(self):
        """Memutar alarm dengan metode yang lebih robust"""
        self.log_message("🔊 Playing alarm sound...", "blue")
        
        # Method 1: Try winsound first (Windows)
        if ALARM_FILE and os.path.exists(ALARM_FILE):
            try:
                self.log_message(f"Using winsound: {ALARM_FILE}", "blue")
                winsound.PlaySound(ALARM_FILE, winsound.SND_FILENAME | winsound.SND_ASYNC | winsound.SND_LOOP)
                self.log_message("✅ Alarm sound started (looping)", "green")
                return
            except Exception as e:
                self.log_message(f"Winsound failed: {e}", "orange")
        
        # Method 2: Try system default beep
        try:
            self.log_message("Trying system beep...", "blue")
            for _ in range(5):
                winsound.Beep(1000, 500)  # 1000Hz, 500ms
                time.sleep(0.5)
            self.log_message("✅ System beep activated", "green")
        except:
            # Method 3: Print bell character (might work on some terminals)
            self.log_message("Trying bell character...", "blue")
            print('\a' * 10)  # System bell
            self.log_message("✅ Bell character sent", "green")
    
    def stop_alarm_sound(self):
        """Menghentikan suara alarm"""
        self.log_message("🔇 Stopping alarm sound...", "blue")
        
        try:
            winsound.PlaySound(None, winsound.SND_PURGE)
            self.log_message("✅ Alarm sound stopped", "green")
        except:
            self.log_message("⚠ Could not stop sound properly", "orange")
    
    def test_alarm(self):
        """Test alarm dari GUI"""
        self.log_message("🔧 Manual test alarm triggered", "blue")
        
        test_data = {
            "patient": "TEST PATIENT",
            "room": "TEST ROOM",
            "device_id": "TEST_DEVICE"
        }
        
        self.handle_emergency_start("Manual Test", test_data)
        
        # Kirim test command ke ESP8266
        self.send_to_esp("TEST_ALARM_TRIGGERED")
    
    def emergency_stop(self):
        """Emergency stop dari GUI"""
        self.log_message("🛑 EMERGENCY STOP from GUI", "red")
        self.handle_emergency_stop()
    
    def send_to_esp(self, message):
        """Mengirim pesan ke ESP8266"""
        if self.serial_conn and self.serial_conn.is_open:
            try:
                full_message = f"{message}\n"
                self.serial_conn.write(full_message.encode('utf-8'))
                self.log_message(f"📤 To ESP: {message}", "darkgreen")
            except Exception as e:
                self.log_message(f"❌ Error sending to ESP: {e}", "red")
        else:
            self.log_message("⚠ Cannot send: Serial not connected", "orange")
    
    def show_emergency_notification(self, patient, room):
        """Menampilkan notifikasi emergency"""
        try:
            message = f"EMERGENCY ALERT!\n\nPatient: {patient}\nRoom: {room}\n\nTime: {datetime.now().strftime('%H:%M:%S')}"
            
            # Tkinter messagebox
            self.root.after(0, lambda: 
                messagebox.showwarning("🚨 HOSPITAL EMERGENCY", message, 
                                      icon=messagebox.WARNING))
            
            # Bring window to front
            self.root.lift()
            self.root.attributes('-topmost', True)
            self.root.after(1000, lambda: self.root.attributes('-topmost', False))
            
        except Exception as e:
            self.log_message(f"Notification error: {e}", "orange")
    
    def flash_window(self):
        """Flash window untuk perhatian"""
        try:
            original_color = self.root.cget("bg")
            for _ in range(6):
                self.root.config(bg="red")
                self.root.update()
                time.sleep(0.3)
                self.root.config(bg=original_color)
                self.root.update()
                time.sleep(0.3)
        except:
            pass
    
    def show_log(self):
        """Menampilkan log window"""
        log_window = tk.Toplevel(self.root)
        log_window.title("Alarm History")
        log_window.geometry("800x400")
        
        text_widget = tk.Text(log_window, wrap=tk.WORD, font=("Consolas", 10))
        scrollbar = ttk.Scrollbar(log_window, orient=tk.VERTICAL, command=text_widget.yview)
        
        text_widget.pack(side=tk.LEFT, expand=True, fill=tk.BOTH, padx=5, pady=5)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        
        text_widget.config(yscrollcommand=scrollbar.set)
        
        # Get all log content
        all_log = self.log_text.get(1.0, tk.END)
        text_widget.insert(tk.END, all_log)
        text_widget.config(state=tk.DISABLED)
        
        ttk.Button(log_window, text="Close", command=log_window.destroy).pack(pady=10)
    
    def on_closing(self):
        """Handle window closing"""
        self.log_message("🛑 Shutting down...", "red")
        self.running = False
        time.sleep(0.5)
        
        if self.serial_conn and self.serial_conn.is_open:
            try:
                self.serial_conn.write(b"PC_SHUTDOWN\n")
                time.sleep(0.2)
                self.serial_conn.close()
                self.log_message("Serial port closed", "blue")
            except:
                pass
        
        self.stop_alarm_sound()
        self.root.destroy()
    
    def run(self):
        """Menjalankan aplikasi"""
        # Auto-connect setelah 1 detik
        self.root.after(1000, self.connect_serial)
        
        # Auto-clear old log entries setiap 5 menit
        self.root.after(300000, self.auto_clear_old_log)
        
        # Run GUI
        self.root.mainloop()
    
    def auto_clear_old_log(self):
        """Otomatis clear log lama"""
        current_lines = self.log_text.get(1.0, tk.END).count('\n')
        if current_lines > 500:
            self.log_text.delete(1.0, "end-400l")
            self.log_message("Auto-cleared old log entries", "blue")
        
        # Schedule next auto-clear
        self.root.after(300000, self.auto_clear_old_log)

# ============================================
# FUNGSI UTAMA
# ============================================
def main():
    print("=" * 70)
    print("🏥 HOSPITAL EMERGENCY ALARM - PC CONTROLLER (FIXED VERSION)")
    print("=" * 70)
    print("Fixes:")
    print("1. Handles ALL ESP8266 data formats")
    print("2. Auto-detects emergency commands")
    print("3. Better error handling")
    print("4. Multiple alarm sound fallbacks")
    print("=" * 70)
    
    if ALARM_FILE:
        print(f"✅ Using alarm file: {ALARM_FILE}")
    else:
        print("⚠ No alarm file found, will use system beeps")
    
    print("\nInstructions:")
    print("1. Connect ESP8266 via USB to CH341SER")
    print("2. Press the button on ESP8266")
    print("3. Alarm should play automatically")
    print("=" * 70)
    
    # Jalankan controller
    controller = PCAlarmController()
    controller.run()

if __name__ == "__main__":
    main()