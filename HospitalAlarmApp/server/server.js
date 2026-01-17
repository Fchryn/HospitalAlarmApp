const express = require('express');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const { exec, spawn } = require('child_process');
const app = express();

const PORT = 3000;
const HOST = '192.168.18.242';
const ALARM_SOUND_PATH = 'C:\\HospitalAlarmApp\\HospitalAlarmApp\\alarm.wav';

// Middleware dengan logging
app.use(cors());
app.use(express.json());
app.use(express.urlencoded({ extended: true }));

// Log semua request untuk debugging
app.use((req, res, next) => {
  const timestamp = new Date().toISOString();
  console.log(`[${timestamp}] ${req.method} ${req.url}`);
  
  if (req.method === 'POST' && req.body) {
    console.log('Request Body:', JSON.stringify(req.body, null, 2));
  }
  
  if (req.headers['device-id']) {
    console.log('Device-ID:', req.headers['device-id']);
  }
  
  next();
});

// Check alarm sound file dengan lebih detail
function checkAlarmSound() {
  console.log('\n🔍 Checking alarm sound file...');
  console.log('Path:', ALARM_SOUND_PATH);
  
  if (!fs.existsSync(ALARM_SOUND_PATH)) {
    console.log('❌ ERROR: File not found!');
    console.log('Please check:');
    console.log('1. File exists at:', ALARM_SOUND_PATH);
    console.log('2. File extension is .wav (not .mav)');
    console.log('3. File is not corrupted');
    console.log('\n⚠️  Creating test alarm sound file...');
    
    // Create a simple test sound file
    const testSoundPath = 'C:\\HospitalAlarmApp\\HospitalAlarmApp\\test_alarm.wav';
    createTestSoundFile(testSoundPath);
    
    return false;
  }
  
  const stats = fs.statSync(ALARM_SOUND_PATH);
  console.log(`✅ File found! Size: ${stats.size} bytes`);
  return true;
}

// Create test sound file jika tidak ada
function createTestSoundFile(filePath) {
  const dir = path.dirname(filePath);
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
  }
  
  // Informasi file test
  const testInfo = `Test alarm sound file created at: ${new Date().toISOString()}\n`;
  fs.writeFileSync(filePath.replace('.wav', '.txt'), testInfo);
  
  console.log(`Test info created at: ${filePath.replace('.wav', '.txt')}`);
}

// Improved function to play alarm sound
function playAlarmSound(alarmType = 1) {
  console.log(`\n🔊 Playing alarm sound (Type: ${alarmType})`);
  
  if (!fs.existsSync(ALARM_SOUND_PATH)) {
    console.log('⚠️  Using system beep (alarm.wav not found)');
    
    // Multiple beeps for emergency
    if (alarmType === 1) {
      for (let i = 0; i < 3; i++) {
        process.stdout.write('\x07');
        setTimeout(() => process.stdout.write('\x07'), 300);
        setTimeout(() => process.stdout.write('\x07'), 600);
      }
    } else {
      process.stdout.write('\x07');
    }
    return;
  }
  
  // Try multiple methods to play sound
  const methods = [
    // Method 1: PowerShell (Windows)
    () => {
      return new Promise((resolve, reject) => {
        const psCommand = `powershell -c (New-Object Media.SoundPlayer "${ALARM_SOUND_PATH}").PlaySync()`;
        exec(psCommand, (error) => {
          if (error) {
            reject(error);
          } else {
            resolve();
          }
        });
      });
    },
    
    // Method 2: Using Windows Media Player via command line
    () => {
      return new Promise((resolve, reject) => {
        const wmpCommand = `cmd /c start /min wmplayer.exe "${ALARM_SOUND_PATH}" /play /close`;
        exec(wmpCommand, (error) => {
          if (error) {
            reject(error);
          } else {
            setTimeout(resolve, 2000); // Wait for sound to play
          }
        });
      });
    },
    
    // Method 3: Using VLC if installed
    () => {
      return new Promise((resolve, reject) => {
        const vlcPath = 'C:\\Program Files\\VideoLAN\\VLC\\vlc.exe';
        if (fs.existsSync(vlcPath)) {
          const vlcCommand = `"${vlcPath}" --intf dummy --play-and-exit "${ALARM_SOUND_PATH}"`;
          exec(vlcCommand, (error) => {
            if (error) {
              reject(error);
            } else {
              resolve();
            }
          });
        } else {
          reject(new Error('VLC not found'));
        }
      });
    }
  ];
  
  // Try each method until one works
  const tryPlaySound = async (methodIndex = 0) => {
    if (methodIndex >= methods.length) {
      console.log('❌ All sound playback methods failed');
      process.stdout.write('\x07'); // Fallback to system beep
      return;
    }
    
    try {
      console.log(`Trying method ${methodIndex + 1}...`);
      await methods[methodIndex]();
      console.log(`✅ Sound played successfully using method ${methodIndex + 1}`);
    } catch (error) {
      console.log(`Method ${methodIndex + 1} failed: ${error.message}`);
      await tryPlaySound(methodIndex + 1);
    }
  };
  
  tryPlaySound();
}

// API endpoint to trigger alarm - SIMPLIFIED VERSION
app.post('/api/trigger-alarm', (req, res) => {
  console.log('\n🚨 ===== ALARM TRIGGERED ===== 🚨');
  
  const timestamp = new Date().toLocaleString('id-ID', {
    timeZone: 'Asia/Jakarta',
    hour12: false
  });
  
  console.log(`📅 Timestamp: ${timestamp}`);
  console.log('📦 Received Data:', req.body);
  console.log('📱 Device ID:', req.headers['device-id'] || 'Unknown');
  
  try {
    // Validate data
    if (!req.body) {
      console.log('❌ No data received');
      return res.status(400).json({ 
        success: false, 
        message: 'No data received' 
      });
    }
    
    // Log alarm details
    const data = req.body;
    console.log('\n📊 Alarm Details:');
    console.log(`📍 Location: ${data.location || 'Unknown'}`);
    console.log(`🚨 Type: ${data.alarmTypeText || 'Unknown'}`);
    console.log(`📶 IP: ${data.ipAddress || 'Unknown'}`);
    console.log(`🔢 MAC: ${data.macAddress || 'Unknown'}`);
    
    // Play alarm sound IMMEDIATELY
    console.log('\n🎵 Playing alarm sound...');
    playAlarmSound(data.alarmType || 1);
    
    // Send immediate response
    res.json({ 
      success: true, 
      message: 'ALARM ACTIVATED SUCCESSFULLY',
      timestamp: timestamp,
      alarmId: Date.now(),
      receivedData: data
    });
    
    console.log('✅ Response sent to ESP8266');
    
    // Log to file
    const logEntry = `[${timestamp}] ALARM - Device: ${data.deviceID || 'Unknown'} - Location: ${data.location || 'Unknown'} - Type: ${data.alarmTypeText || 'Unknown'}\n`;
    fs.appendFileSync('alarm_log.txt', logEntry);
    
  } catch (error) {
    console.error('❌ Error processing alarm:', error);
    res.status(500).json({ 
      success: false, 
      message: 'Internal server error',
      error: error.message 
    });
  }
});

// Simple test endpoint
app.get('/api/test', (req, res) => {
  res.json({ 
    status: 'OK', 
    server: 'Hospital Alarm System',
    timestamp: new Date().toISOString(),
    ip: HOST,
    port: PORT
  });
});

// Test alarm from browser
app.post('/api/test-alarm', (req, res) => {
  console.log('\n🧪 TEST ALARM from Browser');
  
  const testData = {
    deviceID: 'BROWSER-TEST',
    location: 'TEST-ROOM',
    alarmType: 1,
    alarmTypeText: 'TEST_EMERGENCY',
    ipAddress: '192.168.18.242',
    macAddress: '00:00:00:00:00:00'
  };
  
  console.log('Test Data:', testData);
  
  // Play sound
  playAlarmSound(1);
  
  res.json({ 
    success: true, 
    message: 'Test alarm activated from browser',
    timestamp: new Date().toISOString(),
    data: testData
  });
});

// Simple dashboard for testing
app.get('/', (req, res) => {
  const html = `
  <!DOCTYPE html>
  <html>
  <head>
      <title>Hospital Alarm Test</title>
      <style>
          body { font-family: Arial; padding: 20px; }
          button { padding: 15px 30px; font-size: 18px; margin: 10px; }
          .success { color: green; }
          .error { color: red; }
          .status { padding: 10px; margin: 10px 0; }
      </style>
  </head>
  <body>
      <h1>🏥 Hospital Alarm System - Test Page</h1>
      
      <div id="status" class="status"></div>
      
      <button onclick="testServer()">Test Server Connection</button>
      <button onclick="testAlarm()" style="background: red; color: white;">🔊 Test Alarm Sound</button>
      <button onclick="checkSoundFile()">Check Alarm Sound File</button>
      
      <h3>Last Test Result:</h3>
      <pre id="result"></pre>
      
      <script>
          async function testServer() {
              try {
                  const response = await fetch('/api/test');
                  const data = await response.json();
                  showResult('✅ Server is running', data);
              } catch (error) {
                  showResult('❌ Server error', error, true);
              }
          }
          
          async function testAlarm() {
              try {
                  const response = await fetch('/api/test-alarm', {
                      method: 'POST',
                      headers: { 'Content-Type': 'application/json' }
                  });
                  const data = await response.json();
                  showResult('✅ Test alarm activated', data);
              } catch (error) {
                  showResult('❌ Test alarm failed', error, true);
              }
          }
          
          async function checkSoundFile() {
              try {
                  const response = await fetch('/api/check-sound');
                  const data = await response.json();
                  showResult('Sound file check', data);
              } catch (error) {
                  showResult('Check failed', error, true);
              }
          }
          
          function showResult(message, data, isError = false) {
              const status = document.getElementById('status');
              const result = document.getElementById('result');
              
              status.textContent = message;
              status.className = 'status ' + (isError ? 'error' : 'success');
              result.textContent = JSON.stringify(data, null, 2);
          }
          
          // Test server on load
          window.onload = testServer;
      </script>
  </body>
  </html>
  `;
  
  res.send(html);
});

// Check sound file endpoint
app.get('/api/check-sound', (req, res) => {
  const fileExists = fs.existsSync(ALARM_SOUND_PATH);
  
  let fileInfo = {};
  if (fileExists) {
    const stats = fs.statSync(ALARM_SOUND_PATH);
    fileInfo = {
      exists: true,
      path: ALARM_SOUND_PATH,
      size: stats.size,
      created: stats.birthtime,
      modified: stats.mtime
    };
  }
  
  res.json({
    fileExists: fileExists,
    fileInfo: fileInfo,
    serverTime: new Date().toISOString()
  });
});

// Start server
app.listen(PORT, HOST, () => {
  console.log(`
╔══════════════════════════════════════════════════════╗
║    🏥 HOSPITAL ALARM SYSTEM - DEBUG MODE            ║
╠══════════════════════════════════════════════════════╣
║ Server: http://${HOST}:${PORT}                    ║
║ API:    http://${HOST}:${PORT}/api/trigger-alarm ║
║ Test:   http://${HOST}:${PORT}/                   ║
║ Sound:  ${ALARM_SOUND_PATH}      ║
╚══════════════════════════════════════════════════════╝
  `);
  
  console.log('\n🔍 Debug Information:');
  console.log('1. Checking alarm sound file...');
  const soundExists = checkAlarmSound();
  
  console.log('\n2. Network Information:');
  console.log(`   - Host: ${HOST}`);
  console.log(`   - Port: ${PORT}`);
  console.log(`   - Local URL: http://localhost:${PORT}`);
  console.log(`   - Network URL: http://${HOST}:${PORT}`);
  
  console.log('\n3. Testing Routes:');
  console.log(`   ✅ GET  /           - Test page`);
  console.log(`   ✅ GET  /api/test   - Server test`);
  console.log(`   ✅ POST /api/test-alarm - Browser test`);
  console.log(`   ✅ POST /api/trigger-alarm - ESP8266 endpoint`);
  
  console.log('\n🚨 READY TO RECEIVE ALARMS!');
  console.log('\nTroubleshooting steps if alarm not working:');
  console.log('1. Open browser to: http://192.168.18.242:3000');
  console.log('2. Click "Test Alarm Sound" button');
  console.log('3. If browser test works but ESP doesn\'t:');
  console.log('   - Check ESP Serial Monitor');
  console.log('   - Check WiFi connection');
  console.log('   - Check server IP address in ESP code');
  console.log('   - Check firewall on port 3000');
});