import serial
import time
import os
import sys
import winsound

def main():
    print("SUPER SIMPLE ALARM LISTENER")
    print("=" * 50)
    
    # Coba semua port COM
    for com in [f"COM{i}" for i in range(1, 11)]:
        try:
            print(f"\nTrying {com}...")
            ser = serial.Serial(com, 115200, timeout=1)
            time.sleep(2)
            
            print(f"✅ Connected to {com}")
            print("Listening for ESP8266... Press Ctrl+C to exit")
            print("-" * 50)
            
            # Clear buffer
            ser.reset_input_buffer()
            
            while True:
                try:
                    if ser.in_waiting > 0:
                        # Baca semua yang ada
                        raw = ser.read(ser.in_waiting)
                        text = raw.decode('utf-8', errors='ignore')
                        
                        # Tampilkan semua data
                        if text.strip():
                            print(f"Received: {repr(text)}")
                        
                        # Cek jika ada kata kunci ALARM
                        if any(keyword in text.upper() for keyword in ['ALARM', 'EMERGENCY', '!ALARM!', 'START']):
                            print("\n" + "="*50)
                            print("🚨 ALARM DETECTED! PLAYING SOUND...")
                            print("="*50)
                            
                            # Bunyikan alarm
                            for i in range(10):
                                winsound.Beep(1000, 7000)
                                time.sleep(0.1)
                            
                            print("Alarm stopped")
                            
                        # Cek jika STOP
                        elif any(keyword in text.upper() for keyword in ['STOP', 'CANCEL', '!STOP!']):
                            print("Alarm cancelled by ESP8266")
                            
                except KeyboardInterrupt:
                    print("\nExiting...")
                    ser.close()
                    sys.exit(0)
                except Exception as e:
                    print(f"Error: {e}")
                    continue
                    
                time.sleep(0.1)
                
        except serial.SerialException:
            continue
        except KeyboardInterrupt:
            print("\nExiting...")
            sys.exit(0)
    
    print("\n❌ No ESP8266 found on any COM port!")

if __name__ == "__main__":
    main()