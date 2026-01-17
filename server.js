const express = require('express');
const http = require('http');
const socketIo = require('socket.io');
const cors = require('cors');
const fs = require('fs');
const path = require('path');
const { exec } = require('child_process');

const app = express();
const server = http.createServer(app);
const io = socketIo(server, {
  cors: {
    origin: "*",
    methods: ["GET", "POST"]
  }
});

const PORT = 8080;

// Middleware
app.use(cors());
app.use(express.json());
app.use(express.static('public'));

// Data storage
let devices = new Map(); // device_id -> device info
let activeAlarms = new Map(); // device_id -> alarm info
let alarmHistory = [];
let connectedClients = new Set();

// File paths
const ALARM_SOUND_PATH = "C:\\HospitalAlarmApp\\HospitalAlarmApp\\alarm.wav";
const LOG_DIR = path.join(__dirname, 'logs');

// Ensure log directory exists
if (!fs.existsSync(LOG_DIR)) {
  fs.mkdirSync(LOG_DIR, { recursive: true });
}

// ============================================
// API ENDPOINTS
// ============================================

// Device registration
app.post('/api/device/register', (req, res) => {
  const deviceData = req.body;
  console.log(`📱 Device registered: ${deviceData.device_id}`);
  
  devices.set(deviceData.device_id, {
    ...deviceData,
    last_seen: Date.now(),
    status: 'online'
  });
  
  res.json({ 
    status: 'success', 
    message: 'Device registered',
    registered_at: new Date().toISOString()
  });
});

// Heartbeat from devices
app.post('/api/device/heartbeat', (req, res) => {
  const { device_id, ip_address, status } = req.body;
  
  if (devices.has(device_id)) {
    const device = devices.get(device_id);
    device.last_seen = Date.now();
    device.status = 'online';
    device.ip_address = ip_address;
  }
  
  res.json({ status: 'received' });
});

// Emergency alarm trigger
app.post('/api/alarm/trigger', (req, res) => {
  const alarmData = req.body;
  const deviceId = alarmData.device_id;
  
  console.log(`🚨 ALARM TRIGGERED by ${deviceId}`);
  console.log('Alarm Data:', alarmData);
  
  // Store alarm
  alarmData.received_at = new Date().toISOString();
  alarmData.alarm_id = Date.now().toString();
  alarmData.acknowledged = false;
  alarmData.acknowledged_by = null;
  alarmData.acknowledged_at = null;
  
  activeAlarms.set(deviceId, alarmData);
  alarmHistory.push(alarmData);
  
  // Broadcast to all connected clients
  io.emit('alarm_triggered', alarmData);
  
  // Play alarm sound
  playAlarmSound();
  
  // Show desktop notification (Windows)
  showDesktopNotification(alarmData);
  
  // Log to file
  logAlarm(alarmData);
  
  res.json({ 
    status: 'success', 
    message: 'Alarm received',
    alarm_id: alarmData.alarm_id,
    acknowledged: false
  });
});

// Cancel emergency
app.post('/api/alarm/cancel', (req, res) => {
  const { device_id } = req.body;
  
  console.log(`✅ Alarm cancelled by ${device_id}`);
  
  if (activeAlarms.has(device_id)) {
    const alarm = activeAlarms.get(device_id);
    alarm.cancelled_at = new Date().toISOString();
    alarm.status = 'CANCELLED';
    
    // Move to history and remove from active
    activeAlarms.delete(device_id);
    
    // Broadcast cancellation
    io.emit('alarm_cancelled', { device_id });
    
    // Stop alarm sound if no active alarms
    if (activeAlarms.size === 0) {
      stopAlarmSound();
    }
  }
  
  res.json({ status: 'success', message: 'Alarm cancelled' });
});

// Check alarm status
app.get('/api/alarm/status/:device_id', (req, res) => {
  const deviceId = req.params.device_id;
  
  if (activeAlarms.has(deviceId)) {
    const alarm = activeAlarms.get(deviceId);
    res.json({
      active: true,
      acknowledged: alarm.acknowledged,
      triggered_at: alarm.received_at
    });
  } else {
    res.json({ active: false, acknowledged: false });
  }
});

// Acknowledge alarm
app.post('/api/alarm/acknowledge/:alarm_id', (req, res) => {
  const alarmId = req.params.alarm_id;
  const { acknowledged_by } = req.body;
  
  // Find alarm in active alarms
  for (let [deviceId, alarm] of activeAlarms) {
    if (alarm.alarm_id === alarmId) {
      alarm.acknowledged = true;
      alarm.acknowledged_by = acknowledged_by || 'Nurse Station';
      alarm.acknowledged_at = new Date().toISOString();
      
      console.log(`✅ Alarm ${alarmId} acknowledged by ${alarm.acknowledged_by}`);
      
      // Broadcast acknowledgment
      io.emit('alarm_acknowledged', alarm);
      
      // Stop alarm if all are acknowledged
      if (Array.from(activeAlarms.values()).every(a => a.acknowledged)) {
        stopAlarmSound();
      }
      
      return res.json({ 
        status: 'success', 
        message: 'Alarm acknowledged',
        alarm_id: alarmId
      });
    }
  }
  
  res.status(404).json({ status: 'error', message: 'Alarm not found' });
});

// Get all active alarms
app.get('/api/alarms/active', (req, res) => {
  const alarms = Array.from(activeAlarms.values());
  res.json({
    count: alarms.length,
    alarms: alarms
  });
});

// Get alarm history
app.get('/api/alarms/history', (req, res) => {
  res.json({
    count: alarmHistory.length,
    alarms: alarmHistory.slice(-50) // Last 50 alarms
  });
});

// Get connected devices
app.get('/api/devices', (req, res) => {
  const deviceList = Array.from(devices.values())
    .filter(device => Date.now() - device.last_seen < 60000) // Last minute
    .map(device => ({
      device_id: device.device_id,
      ip_address: device.ip_address,
      status: device.status,
      last_seen: new Date(device.last_seen).toLocaleTimeString()
    }));
  
  res.json({
    count: deviceList.length,
    devices: deviceList
  });
});

// ============================================
// WEB DASHBOARD
// ============================================

// Serve main dashboard
app.get('/', (req, res) => {
  res.sendFile(path.join(__dirname, 'public', 'dashboard.html'));
});

// Serve admin dashboard
app.get('/admin', (req, res) => {
  res.send(`
    <!DOCTYPE html>
    <html lang="en">
    <head>
      <meta charset="UTF-8">
      <meta name="viewport" content="width=device-width, initial-scale=1.0">
      <title>Hospital Alarm System - Admin Dashboard</title>
      <link rel="stylesheet" href="https://cdnjs.cloudflare.com/ajax/libs/font-awesome/6.4.0/css/all.min.css">
      <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background: #0f172a; color: #f8fafc; }
        
        .container { max-width: 1400px; margin: 0 auto; padding: 20px; }
        
        .header { 
          background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
          padding: 30px; 
          border-radius: 15px;
          margin-bottom: 30px;
          box-shadow: 0 10px 25px rgba(0,0,0,0.3);
          border: 1px solid #475569;
        }
        
        .header h1 { 
          color: #60a5fa; 
          font-size: 2.8rem; 
          margin-bottom: 10px;
          display: flex;
          align-items: center;
          gap: 15px;
        }
        
        .header p { color: #cbd5e1; font-size: 1.1rem; }
        
        .stats-grid { 
          display: grid; 
          grid-template-columns: repeat(auto-fit, minmax(250px, 1fr)); 
          gap: 20px; 
          margin-bottom: 30px;
        }
        
        .stat-card {
          background: #1e293b;
          padding: 25px;
          border-radius: 12px;
          box-shadow: 0 5px 15px rgba(0,0,0,0.2);
          border-left: 5px solid;
          transition: transform 0.3s;
        }
        
        .stat-card:hover { transform: translateY(-5px); }
        
        .stat-card.emergency { border-color: #ef4444; }
        .stat-card.devices { border-color: #3b82f6; }
        .stat-card.online { border-color: #10b981; }
        .stat-card.history { border-color: #8b5cf6; }
        
        .stat-card h3 { color: #94a3b8; font-size: 1rem; margin-bottom: 10px; }
        .stat-card .value { font-size: 2.5rem; font-weight: bold; }
        .stat-card.emergency .value { color: #ef4444; }
        .stat-card.devices .value { color: #60a5fa; }
        .stat-card.online .value { color: #34d399; }
        .stat-card.history .value { color: #a78bfa; }
        
        .alarms-section { margin-bottom: 40px; }
        
        .section-title { 
          font-size: 1.8rem; 
          margin-bottom: 20px; 
          color: #e2e8f0;
          display: flex;
          align-items: center;
          gap: 10px;
        }
        
        .alarm-card {
          background: #1e293b;
          border-radius: 10px;
          padding: 20px;
          margin-bottom: 15px;
          border-left: 6px solid #ef4444;
          box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }
        
        .alarm-card.acknowledged { border-left-color: #10b981; }
        
        .alarm-header { 
          display: flex; 
          justify-content: space-between; 
          align-items: center;
          margin-bottom: 15px;
        }
        
        .alarm-title { font-size: 1.4rem; font-weight: bold; color: #f8fafc; }
        .alarm-time { color: #94a3b8; font-size: 0.9rem; }
        
        .alarm-details { 
          display: grid; 
          grid-template-columns: repeat(auto-fit, minmax(200px, 1fr)); 
          gap: 15px;
          margin-bottom: 15px;
        }
        
        .detail-item label { color: #64748b; font-size: 0.9rem; }
        .detail-item div { color: #e2e8f0; font-size: 1.1rem; }
        
        button {
          background: #3b82f6;
          color: white;
          border: none;
          padding: 12px 24px;
          border-radius: 8px;
          font-size: 1rem;
          font-weight: 600;
          cursor: pointer;
          transition: all 0.3s;
          display: flex;
          align-items: center;
          gap: 8px;
        }
        
        button:hover { background: #2563eb; transform: scale(1.05); }
        
        button.acknowledge-btn { background: #10b981; }
        button.acknowledge-btn:hover { background: #059669; }
        
        button:disabled { background: #475569; cursor: not-allowed; }
        
        .empty-state {
          text-align: center;
          padding: 40px;
          color: #64748b;
          font-size: 1.2rem;
        }
        
        .history-table {
          width: 100%;
          background: #1e293b;
          border-radius: 10px;
          overflow: hidden;
          box-shadow: 0 4px 12px rgba(0,0,0,0.15);
        }
        
        table { width: 100%; border-collapse: collapse; }
        
        th { background: #334155; color: #cbd5e1; padding: 15px; text-align: left; }
        td { padding: 15px; border-bottom: 1px solid #334155; color: #e2e8f0; }
        
        tr:hover { background: #2d3748; }
        
        .status-badge {
          display: inline-block;
          padding: 5px 12px;
          border-radius: 20px;
          font-size: 0.8rem;
          font-weight: bold;
        }
        
        .status-active { background: #fef2f2; color: #dc2626; }
        .status-acknowledged { background: #f0fdf4; color: #16a34a; }
        .status-cancelled { background: #f8fafc; color: #475569; }
        
        .sound-controls {
          background: #1e293b;
          padding: 20px;
          border-radius: 10px;
          margin-top: 20px;
          display: flex;
          gap: 15px;
          flex-wrap: wrap;
        }
        
        footer {
          text-align: center;
          margin-top: 50px;
          padding: 20px;
          color: #64748b;
          border-top: 1px solid #334155;
        }
        
        .connection-status {
          display: inline-flex;
          align-items: center;
          gap: 8px;
          padding: 8px 16px;
          background: #1e293b;
          border-radius: 20px;
          margin-top: 10px;
        }
        
        .status-dot {
          width: 10px;
          height: 10px;
          border-radius: 50%;
          background: #10b981;
          animation: pulse 2s infinite;
        }
        
        @keyframes pulse {
          0% { opacity: 1; }
          50% { opacity: 0.5; }
          100% { opacity: 1; }
        }
        
        @media (max-width: 768px) {
          .container { padding: 10px; }
          .header h1 { font-size: 2rem; }
          .stats-grid { grid-template-columns: 1fr; }
        }
      </style>
    </head>
    <body>
      <div class="container">
        <div class="header">
          <h1><i class="fas fa-hospital"></i> HOSPITAL EMERGENCY ALARM SYSTEM</h1>
          <p>Real-time monitoring of patient emergency alerts | Admin Dashboard</p>
          <div class="connection-status">
            <span class="status-dot"></span>
            <span id="connectionStatus">Connecting to server...</span>
            <span id="clientCount">0 clients connected</span>
          </div>
        </div>
        
        <div class="stats-grid">
          <div class="stat-card emergency">
            <h3><i class="fas fa-bell"></i> ACTIVE ALARMS</h3>
            <div class="value" id="activeAlarmsCount">0</div>
          </div>
          <div class="stat-card devices">
            <h3><i class="fas fa-watch"></i> CONNECTED DEVICES</h3>
            <div class="value" id="connectedDevicesCount">0</div>
          </div>
          <div class="stat-card online">
            <h3><i class="fas fa-wifi"></i> ONLINE NOW</h3>
            <div class="value" id="onlineDevicesCount">0</div>
          </div>
          <div class="stat-card history">
            <h3><i class="fas fa-history"></i> TOTAL ALARMS TODAY</h3>
            <div class="value" id="totalAlarmsCount">0</div>
          </div>
        </div>
        
        <div class="alarms-section">
          <h2 class="section-title"><i class="fas fa-exclamation-triangle"></i> ACTIVE EMERGENCY ALARMS</h2>
          <div id="activeAlarmsContainer">
            <div class="empty-state">
              <i class="fas fa-check-circle" style="font-size: 3rem; margin-bottom: 15px;"></i>
              <p>No active emergency alarms</p>
              <p style="font-size: 0.9rem; margin-top: 10px;">System is ready</p>
            </div>
          </div>
        </div>
        
        <div class="sound-controls">
          <button onclick="playTestSound()">
            <i class="fas fa-volume-up"></i> Test Alarm Sound
          </button>
          <button onclick="stopAlarmSound()">
            <i class="fas fa-volume-mute"></i> Stop Alarm Sound
          </button>
          <button onclick="clearAllAlarms()">
            <i class="fas fa-trash"></i> Clear All Alarms
          </button>
          <button onclick="refreshData()">
            <i class="fas fa-sync-alt"></i> Refresh Data
          </button>
        </div>
        
        <div style="margin-top: 40px;">
          <h2 class="section-title"><i class="fas fa-history"></i> RECENT ALARM HISTORY</h2>
          <div class="history-table">
            <table>
              <thead>
                <tr>
                  <th>Time</th>
                  <th>Device ID</th>
                  <th>Room</th>
                  <th>Status</th>
                  <th>Acknowledged By</th>
                </tr>
              </thead>
              <tbody id="historyTable">
                <tr>
                  <td colspan="5" style="text-align: center; padding: 30px;">Loading history...</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>
        
        <footer>
          <p>Hospital Alarm System v1.0 | Server: 192.168.18.242:8080</p>
          <p>© 2024 Hospital Emergency Response Team</p>
        </footer>
      </div>
      
      <script src="/socket.io/socket.io.js"></script>
      <script>
        const socket = io();
        let alarmSound = null;
        
        // Connection status
        socket.on('connect', () => {
          document.getElementById('connectionStatus').textContent = 'Connected to server';
          updateClientCount();
        });
        
        socket.on('disconnect', () => {
          document.getElementById('connectionStatus').textContent = 'Disconnected from server';
        });
        
        socket.on('clients_updated', (count) => {
          document.getElementById('clientCount').textContent = "${count} clients connected";
        });
        
        // Alarm events
        socket.on('alarm_triggered', (alarmData) => {
          console.log('New alarm received:', alarmData);
          updateActiveAlarms();
          updateStats();
          showBrowserNotification(alarmData);
        });
        
        socket.on('alarm_acknowledged', (alarmData) => {
          console.log('Alarm acknowledged:', alarmData);
          updateActiveAlarms();
          updateStats();
        });
        
        socket.on('alarm_cancelled', (data) => {
          console.log('Alarm cancelled:', data);
          updateActiveAlarms();
          updateStats();
        });
        
        // Initial data load
        window.addEventListener('load', () => {
          updateActiveAlarms();
          updateHistory();
          updateStats();
          updateDevices();
          
          // Request notification permission
          if ('Notification' in window && Notification.permission === 'default') {
            Notification.requestPermission();
          }
        });
        
        // Functions
        async function updateActiveAlarms() {
          try {
            const response = await fetch('/api/alarms/active');
            const data = await response.json();
            
            const container = document.getElementById('activeAlarmsContainer');
            
            if (data.count === 0) {
              container.innerHTML = \`
                <div class="empty-state">
                  <i class="fas fa-check-circle" style="font-size: 3rem; margin-bottom: 15px;"></i>
                  <p>No active emergency alarms</p>
                  <p style="font-size: 0.9rem; margin-top: 10px;">System is ready</p>
                </div>
              \`;
            } else {
              container.innerHTML = data.alarms.map(alarm => \`
                <div class="alarm-card \${alarm.acknowledged ? 'acknowledged' : ''}" id="alarm-\${alarm.alarm_id}">
                  <div class="alarm-header">
                    <div class="alarm-title">
                      <i class="fas fa-bell"></i> EMERGENCY - \${alarm.device_id}
                    </div>
                    <div class="alarm-time">
                      \${new Date(alarm.received_at).toLocaleTimeString()}
                    </div>
                  </div>
                  
                  <div class="alarm-details">
                    <div class="detail-item">
                      <label><i class="fas fa-user-injured"></i> Patient Room</label>
                      <div>\${alarm.room_number || 'N/A'}</div>
                    </div>
                    <div class="detail-item">
                      <label><i class="fas fa-wifi"></i> IP Address</label>
                      <div>\${alarm.ip_address}</div>
                    </div>
                    <div class="detail-item">
                      <label><i class="fas fa-signal"></i> WiFi Strength</label>
                      <div>\${alarm.wifi_rssi || 'N/A'} dBm</div>
                    </div>
                    <div class="detail-item">
                      <label><i class="fas fa-battery"></i> Battery</label>
                      <div>\${alarm.battery_level || 'N/A'}%</div>
                    </div>
                  </div>
                  
                  <div style="margin-top: 15px;">
                    \${alarm.acknowledged ? 
                      \`<button class="acknowledge-btn" disabled>
                        <i class="fas fa-check-circle"></i> ACKNOWLEDGED
                      </button>\` : 
                      \`<button class="acknowledge-btn" onclick="acknowledgeAlarm('\${alarm.alarm_id}')">
                        <i class="fas fa-check"></i> ACKNOWLEDGE ALARM
                      </button>\`
                    }
                  </div>
                </div>
              \`).join('');
            }
          } catch (error) {
            console.error('Error fetching active alarms:', error);
          }
        }
        
        async function updateHistory() {
          try {
            const response = await fetch('/api/alarms/history');
            const data = await response.json();
            
            const tbody = document.getElementById('historyTable');
            
            if (data.count === 0) {
              tbody.innerHTML = \`
                <tr>
                  <td colspan="5" style="text-align: center; padding: 30px;">No alarm history</td>
                </tr>
              \`;
            } else {
              tbody.innerHTML = data.alarms.slice().reverse().map(alarm => \`
                <tr>
                  <td>\${new Date(alarm.received_at).toLocaleTimeString()}</td>
                  <td>\${alarm.device_id}</td>
                  <td>\${alarm.room_number || 'N/A'}</td>
                  <td>
                    <span class="status-badge \${getStatusClass(alarm)}">
                      \${getStatusText(alarm)}
                    </span>
                  </td>
                  <td>\${alarm.acknowledged_by || '-'}</td>
                </tr>
              \`).join('');
            }
          } catch (error) {
            console.error('Error fetching history:', error);
          }
        }
        
        async function updateStats() {
          try {
            const [alarmsRes, devicesRes] = await Promise.all([
              fetch('/api/alarms/active'),
              fetch('/api/devices')
            ]);
            
            const alarmsData = await alarmsRes.json();
            const devicesData = await devicesRes.json();
            
            document.getElementById('activeAlarmsCount').textContent = alarmsData.count;
            document.getElementById('connectedDevicesCount').textContent = devicesData.count;
            document.getElementById('onlineDevicesCount').textContent = devicesData.devices.filter(d => d.status === 'online').length;
            document.getElementById('totalAlarmsCount').textContent = await getTodayAlarmCount();
          } catch (error) {
            console.error('Error updating stats:', error);
          }
        }
        
        async function updateDevices() {
          try {
            const response = await fetch('/api/devices');
            const data = await response.json();
            // Update UI as needed
          } catch (error) {
            console.error('Error fetching devices:', error);
          }
        }
        
        async function getTodayAlarmCount() {
          try {
            const response = await fetch('/api/alarms/history');
            const data = await response.json();
            const today = new Date().toDateString();
            
            return data.alarms.filter(alarm => {
              const alarmDate = new Date(alarm.received_at).toDateString();
              return alarmDate === today;
            }).length;
          } catch (error) {
            return 'N/A';
          }
        }
        
        function getStatusClass(alarm) {
          if (alarm.status === 'CANCELLED') return 'status-cancelled';
          if (alarm.acknowledged) return 'status-acknowledged';
          return 'status-active';
        }
        
        function getStatusText(alarm) {
          if (alarm.status === 'CANCELLED') return 'Cancelled';
          if (alarm.acknowledged) return 'Acknowledged';
          return 'Active';
        }
        
        async function acknowledgeAlarm(alarmId) {
          try {
            const response = await fetch(\`/api/alarm/acknowledge/\${alarmId}\`, {
              method: 'POST',
              headers: { 'Content-Type': 'application/json' },
              body: JSON.stringify({ acknowledged_by: 'Admin Dashboard' })
            });
            
            if (response.ok) {
              updateActiveAlarms();
              updateStats();
            }
          } catch (error) {
            console.error('Error acknowledging alarm:', error);
          }
        }
        
        function showBrowserNotification(alarmData) {
          if (Notification.permission === 'granted') {
            new Notification('🚨 EMERGENCY ALERT', {
              body: \`Device: \${alarmData.device_id}\\nRoom: \${alarmData.room_number}\\nTime: \${new Date(alarmData.received_at).toLocaleTimeString()}\`,
              icon: '/hospital-icon.png',
              requireInteraction: true
            });
          }
        }
        
        function playTestSound() {
          fetch('/api/test-sound')
            .then(response => response.json())
            .then(data => {
              if (data.status === 'success') {
                alert('Test sound played successfully');
              }
            });
        }
        
        function stopAlarmSound() {
          fetch('/api/stop-sound')
            .then(response => response.json())
            .then(data => {
              if (data.status === 'success') {
                alert('Alarm sound stopped');
              }
            });
        }
        
        async function clearAllAlarms() {
          if (confirm('Are you sure you want to clear all active alarms?')) {
            // Implement clearing logic
            alert('This feature would clear all alarms (implementation needed)');
          }
        }
        
        function refreshData() {
          updateActiveAlarms();
          updateHistory();
          updateStats();
          updateDevices();
        }
        
        // Auto-refresh every 10 seconds
        setInterval(refreshData, 10000);
        
        // Update client count every 5 seconds
        function updateClientCount() {
          socket.emit('get_clients_count');
        }
        
        setInterval(updateClientCount, 5000);
      </script>
    </body>
    </html>
  `);
});

// Additional API endpoints
app.post('/api/test-sound', (req, res) => {
  playAlarmSound();
  res.json({ status: 'success', message: 'Test sound played' });
});

app.post('/api/stop-sound', (req, res) => {
  stopAlarmSound();
  res.json({ status: 'success', message: 'Sound stopped' });
});

// ============================================
// SOCKET.IO HANDLERS
// ============================================

io.on('connection', (socket) => {
  connectedClients.add(socket.id);
  console.log(`Client connected: ${socket.id} (Total: ${connectedClients.size})`);
  
  // Send initial data
  socket.emit('initial_data', {
    activeAlarms: Array.from(activeAlarms.values()),
    devices: Array.from(devices.values()).filter(d => Date.now() - d.last_seen < 60000)
  });
  
  // Handle client count request
  socket.on('get_clients_count', () => {
    io.emit('clients_updated', connectedClients.size);
  });
  
  socket.on('disconnect', () => {
    connectedClients.delete(socket.id);
    console.log(`Client disconnected: ${socket.id} (Total: ${connectedClients.size})`);
    io.emit('clients_updated', connectedClients.size);
  });
});

// ============================================
// HELPER FUNCTIONS
// ============================================

function playAlarmSound() {
  if (process.platform === 'win32' && fs.existsSync(ALARM_SOUND_PATH)) {
    try {
      // Use Windows Media Player to play sound
      const command = `powershell -c (New-Object Media.SoundPlayer "${ALARM_SOUND_PATH.replace(/\\/g, '\\\\')}").PlaySync()`;
      exec(command, { timeout: 10000 }, (error) => {
        if (error && error.code !== 1) { // Ignore timeout errors
          console.error('Error playing sound:', error.message);
          // Fallback: system beep
          process.stdout.write('\x07');
        }
      });
    } catch (error) {
      console.error('Sound playback error:', error);
      process.stdout.write('\x07');
    }
  } else {
    // System beep for other platforms or if file doesn't exist
    process.stdout.write('\x07');
  }
}

function stopAlarmSound() {
  // On Windows, we can kill the powershell process
  if (process.platform === 'win32') {
    exec('taskkill /f /im powershell.exe', (error) => {
      if (error && !error.message.includes('not found')) {
        console.error('Error stopping sound:', error.message);
      }
    });
  }
}

function showDesktopNotification(alarmData) {
  if (process.platform === 'win32') {
    const notificationScript = `
      Add-Type -AssemblyName System.Windows.Forms
      $notify = New-Object System.Windows.Forms.NotifyIcon
      $notify.Icon = [System.Drawing.SystemIcons]::Information
      $notify.BalloonTipTitle = '🚨 EMERGENCY ALARM!'
      $notify.BalloonTipText = 'Device: ${alarmData.device_id}\\nRoom: ${alarmData.room_number}\\nTime: ${new Date().toLocaleTimeString()}'
      $notify.Visible = $true
      $notify.ShowBalloonTip(10000)
    `;
    
    exec(`powershell -command "${notificationScript.replace(/\n/g, ';')}"`);
  }
}

function logAlarm(alarmData) {
  const logFile = path.join(LOG_DIR, `alarms_${new Date().toISOString().split('T')[0]}.log`);
  const logEntry = `${new Date().toISOString()} | EMERGENCY | Device: ${alarmData.device_id} | IP: ${alarmData.ip_address} | Room: ${alarmData.room_number} | RSSI: ${alarmData.wifi_rssi || 'N/A'} dBm\n`;
  
  fs.appendFile(logFile, logEntry, (err) => {
    if (err) console.error('Error writing to log file:', err);
  });
}

// Cleanup old devices periodically
setInterval(() => {
  const now = Date.now();
  for (let [deviceId, device] of devices) {
    if (now - device.last_seen > 120000) { // 2 minutes
      devices.delete(deviceId);
      console.log(`Device ${deviceId} marked as offline`);
    }
  }
}, 60000); // Check every minute

// Start server
server.listen(PORT, '192.168.18.242', () => {
  console.log('==============================================');
  console.log('🏥 HOSPITAL EMERGENCY ALARM SERVER');
  console.log('==============================================');
  console.log(`Server running at:`);
  console.log(`  Local:    http://localhost:${PORT}`);
  console.log(`  Network:  http://192.168.18.242:${PORT}`);
  console.log(`  Admin:    http://192.168.18.242:${PORT}/admin`);
  console.log('');
  console.log('Listening for bracelet alarms...');
  console.log('Press Ctrl+C to stop the server');
  console.log('==============================================');
});

// Handle graceful shutdown
process.on('SIGINT', () => {
  console.log('\nShutting down server...');
  server.close(() => {
    console.log('Server stopped');
    process.exit(0);
  });
});