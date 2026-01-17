class HospitalAlarmDashboard {
    constructor() {
        this.ws = null;
        this.state = {
            alarm: { active: false },
            device: { status: 'disconnected' },
            patients: []
        };
        this.refreshInterval = null;
        this.refreshTimer = 5;
        this.autoRefresh = true;
        
        this.initialize();
    }
    
    initialize() {
        console.log('🚨 Hospital Alarm Dashboard Initializing...');
        
        // Initialize WebSocket
        this.initWebSocket();
        
        // Initial data load
        this.loadInitialData();
        
        // Set up auto-refresh
        this.setupAutoRefresh();
        
        // Set up event listeners
        this.setupEventListeners();
        
        // Check alarm file
        this.checkAlarmFiles();
        
        console.log('Dashboard ready. Waiting for ESP8266 signals...');
    }
    
    initWebSocket() {
        const wsUrl = `ws://${window.location.hostname}:${window.location.port || 3000}`;
        this.ws = new WebSocket(wsUrl);
        
        this.ws.onopen = () => {
            console.log('✅ WebSocket connected');
            this.updateUIStatus('wsStatus', 'bg-success', 'Connected');
        };
        
        this.ws.onmessage = (event) => {
            try {
                const data = JSON.parse(event.data);
                this.handleWebSocketMessage(data);
            } catch (err) {
                console.error('Error parsing WebSocket message:', err);
            }
        };
        
        this.ws.onclose = () => {
            console.log('❌ WebSocket disconnected');
            this.updateUIStatus('wsStatus', 'bg-danger', 'Disconnected');
            
            // Try to reconnect after 3 seconds
            setTimeout(() => this.initWebSocket(), 3000);
        };
        
        this.ws.onerror = (error) => {
            console.error('WebSocket error:', error);
            this.updateUIStatus('wsStatus', 'bg-danger', 'Error');
        };
    }
    
    handleWebSocketMessage(data) {
        console.log('WebSocket message:', data.type);
        
        switch (data.type) {
            case 'initial_state':
                this.state = data.state;
                this.updateDashboard();
                break;
                
            case 'state_update':
                this.state = data.state;
                this.updateDashboard();
                break;
                
            case 'alarm_triggered':
                this.handleAlarmTriggered(data);
                break;
                
            case 'alarm_stopped':
                this.handleAlarmStopped(data);
                break;
                
            case 'alarm_sound_started':
                this.showAlarmPlaying(data);
                break;
                
            case 'data_cleared':
                this.showNotification('All data cleared', 'info');
                this.loadPatients();
                break;
        }
    }
    
    handleAlarmTriggered(data) {
        console.log('🚨 ALARM TRIGGERED:', data);
        
        // Update state
        this.state.alarm = data.alarm;
        
        // Show emergency overlay
        this.showEmergencyOverlay(data);
        
        // Update UI
        this.updateAlarmStatus(true);
        
        // Play browser notification sound
        this.playBrowserNotification();
        
        // Update patients
        this.loadPatients();
        
        // Flash browser title
        this.flashBrowserTitle('🚨 EMERGENCY!');
        
        // Show notification
        this.showNotification(`EMERGENCY! Triggered by ${data.alarm.triggeredBy}`, 'danger');
    }
    
    handleAlarmStopped(data) {
        console.log('Alarm stopped by:', data.source);
        
        this.state.alarm.active = false;
        this.updateAlarmStatus(false);
        this.hideEmergencyOverlay();
        this.stopFlashBrowserTitle();
        
        this.showNotification(`Alarm acknowledged by ${data.source}`, 'success');
    }
    
    showEmergencyOverlay(data) {
        const overlay = document.getElementById('emergencyOverlay');
        const source = document.getElementById('alarmSource');
        const time = document.getElementById('alarmTime');
        
        source.textContent = data.alarm.triggeredBy || 'Unknown';
        time.textContent = new Date(data.alarm.triggeredAt).toLocaleTimeString();
        
        overlay.style.display = 'flex';
        
        // Auto-hide after 30 seconds if not acknowledged
        setTimeout(() => {
            if (this.state.alarm.active && !this.state.alarm.acknowledged) {
                this.hideEmergencyOverlay();
            }
        }, 30000);
    }
    
    hideEmergencyOverlay() {
        document.getElementById('emergencyOverlay').style.display = 'none';
    }
    
    updateAlarmStatus(isActive) {
        const alarmBadge = document.getElementById('alarmBadge');
        const stopBtn = document.getElementById('stopBtn');
        const deviceIndicator = document.getElementById('deviceStatusIndicator');
        
        if (isActive) {
            alarmBadge.style.display = 'inline-block';
            stopBtn.disabled = false;
            deviceIndicator.className = 'status-indicator status-alarm';
            
            // Add alarm class to body
            document.body.classList.add('alarm-active');
        } else {
            alarmBadge.style.display = 'none';
            stopBtn.disabled = true;
            deviceIndicator.className = 'status-indicator ' + 
                (this.state.device.status === 'connected' ? 'status-connected' : 'status-disconnected');
            
            // Remove alarm class
            document.body.classList.remove('alarm-active');
        }
    }
    
    showAlarmPlaying(data) {
        console.log('Alarm sound started:', data.file);
        this.showNotification(`Playing alarm sound: ${data.file}`, 'warning');
    }
    
    playBrowserNotification() {
        // Try to play a simple beep in browser
        try {
            const audioContext = new (window.AudioContext || window.webkitAudioContext)();
            const oscillator = audioContext.createOscillator();
            const gainNode = audioContext.createGain();
            
            oscillator.connect(gainNode);
            gainNode.connect(audioContext.destination);
            
            oscillator.frequency.value = 800;
            oscillator.type = 'sine';
            
            gainNode.gain.setValueAtTime(0.3, audioContext.currentTime);
            gainNode.gain.exponentialRampToValueAtTime(0.01, audioContext.currentTime + 1);
            
            oscillator.start(audioContext.currentTime);
            oscillator.stop(audioContext.currentTime + 1);
        } catch (err) {
            console.log('Browser audio not supported:', err);
        }
    }
    
    flashBrowserTitle(message) {
        const originalTitle = document.title;
        let isOriginal = true;
        
        this.titleInterval = setInterval(() => {
            document.title = isOriginal ? message : originalTitle;
            isOriginal = !isOriginal;
        }, 1000);
    }
    
    stopFlashBrowserTitle() {
        if (this.titleInterval) {
            clearInterval(this.titleInterval);
            document.title = '🚨 Hospital Emergency Alarm System';
        }
    }
    
    loadInitialData() {
        // Load system status
        fetch('/api/status')
            .then(response => response.json())
            .then(data => {
                this.updateSystemStatus(data);
            })
            .catch(err => {
                console.error('Error loading system status:', err);
                this.updateUIStatus('connectionStatus', 'bg-danger', 'Server Error');
            });
        
        // Load patients
        this.loadPatients();
        
        // Load sound files
        this.loadSoundFiles();
    }
    
    updateSystemStatus(data) {
        // Device status
        const deviceIndicator = document.getElementById('deviceStatusIndicator');
        const deviceText = document.getElementById('deviceStatusText');
        const deviceInfo = document.getElementById('deviceInfo');
        
        if (data.device && data.device.status === 'connected') {
            deviceIndicator.className = 'status-indicator status-connected';
            deviceText.textContent = `Device: ${data.device.id || 'ESP8266'}`;
            deviceInfo.textContent = `IP: ${data.device.ip || '--'} | MAC: ${data.device.mac || '--'}`;
            this.updateUIStatus('connectionStatus', 'bg-success', 'Connected');
        } else {
            deviceIndicator.className = 'status-indicator status-disconnected';
            deviceText.textContent = 'Device: Disconnected';
            deviceInfo.textContent = 'IP: -- | MAC: --';
            this.updateUIStatus('connectionStatus', 'bg-warning', 'Waiting for ESP8266');
        }
        
        // Patient count
        document.getElementById('patientCount').textContent = data.patientsCount || 0;
        
        // Server info
        document.getElementById('serverUptime').textContent = 
            this.formatUptime(data.uptime || 0);
        document.getElementById('memoryUsage').textContent = 
            data.memory ? `${Math.round(data.memory.heapUsed / 1024 / 1024)}MB` : '--';
        
        // Alarm count
        document.getElementById('alarmCount').textContent = 
            data.alarm && data.alarm.triggeredAt ? '1' : '0';
    }
    
    loadPatients() {
        fetch('/api/patients')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    this.state.patients = data.patients || [];
                    this.updatePatientTable();
                    document.getElementById('patientCount').textContent = data.count || 0;
                    
                    // Update last update time
                    const lastUpdate = document.getElementById('lastUpdate');
                    const now = new Date();
                    lastUpdate.textContent = `Last: ${now.getHours().toString().padStart(2, '0')}:${now.getMinutes().toString().padStart(2, '0')}:${now.getSeconds().toString().padStart(2, '0')}`;
                }
            })
            .catch(err => {
                console.error('Error loading patients:', err);
            });
    }
    
    updatePatientTable() {
        const tbody = document.getElementById('patientTableBody');
        
        if (!this.state.patients || this.state.patients.length === 0) {
            tbody.innerHTML = `
                <tr>
                    <td colspan="7" class="text-center py-5">
                        <i class="bi bi-inbox display-6 text-muted"></i>
                        <h5 class="mt-3">No Patient Data</h5>
                        <p>Press ESP8266 button or simulate to add data</p>
                    </td>
                </tr>
            `;
            return;
        }
        
        let html = '';
        this.state.patients.forEach(patient => {
            const isEmergency = patient.status === 'EMERGENCY' || patient.status === 'EMERGENCY_TEST';
            const rowClass = isEmergency ? 'patient-row-emergency' : '';
            
            html += `
                <tr class="${rowClass}">
                    <td><span class="badge ${isEmergency ? 'bg-danger' : 'bg-primary'}">${patient.id}</span></td>
                    <td><strong>${patient.name}</strong></td>
                    <td>${patient.room}</td>
                    <td><span class="badge bg-secondary">${patient.bed}</span></td>
                    <td>
                        <span class="badge ${isEmergency ? 'bg-danger' : 'bg-success'}">
                            <i class="bi ${isEmergency ? 'bi-exclamation-triangle' : 'bi-check-circle'}"></i>
                            ${patient.status}
                        </span>
                    </td>
                    <td><small>${patient.time}</small></td>
                    <td>${patient.respondedBy || '--'}</td>
                </tr>
            `;
        });
        
        tbody.innerHTML = html;
        
        // Initialize/refresh DataTable
        if ($.fn.DataTable.isDataTable('#patientTable')) {
            $('#patientTable').DataTable().destroy();
        }
        $('#patientTable').DataTable({
            pageLength: 10,
            order: [[0, 'asc']],
            responsive: true
        });
    }
    
    loadSoundFiles() {
        fetch('/api/sounds')
            .then(response => response.json())
            .then(data => {
                if (data.success) {
                    this.updateSoundFilesList(data.sounds);
                }
            })
            .catch(err => {
                console.error('Error loading sound files:', err);
            });
    }
    
    updateSoundFilesList(sounds) {
        const container = document.getElementById('soundFilesList');
        
        if (!sounds || sounds.length === 0) {
            container.innerHTML = '<div class="text-warning">No sound files found</div>';
            this.updateUIStatus('soundFileStatus', 'bg-danger', 'Missing');
            return;
        }
        
        let html = '<div class="row g-2">';
        sounds.forEach(sound => {
            const exists = sound.size > 0;
            const statusClass = exists ? 'bg-success' : 'bg-warning';
            const statusText = exists ? 'OK' : 'Empty';
            
            html += `
                <div class="col-12">
                    <div class="d-flex justify-content-between align-items-center p-2 bg-dark rounded">
                        <div>
                            <i class="bi bi-file-earmark-music me-2"></i>
                            <strong>${sound.name}</strong>
                            <small class="text-muted d-block">${this.formatFileSize(sound.size)}</small>
                        </div>
                        <span class="badge ${statusClass}">${statusText}</span>
                    </div>
                </div>
            `;
        });
        html += '</div>';
        
        container.innerHTML = html;
        
        // Check if alarm.mav exists
        const alarmFile = sounds.find(s => s.name === 'alarm.mav');
        if (alarmFile && alarmFile.size > 0) {
            this.updateUIStatus('soundFileStatus', 'bg-success', 'Ready');
        } else {
            this.updateUIStatus('soundFileStatus', 'bg-warning', 'Missing');
        }
    }
    
    checkAlarmFiles() {
        // Simple check for alarm file
        const testAudio = new Audio();
        testAudio.src = '/sounds/alarm.mav';
        
        testAudio.addEventListener('canplay', () => {
            console.log('Alarm file can be played by browser');
        });
        
        testAudio.addEventListener('error', () => {
            console.warn('Browser cannot play alarm.mav directly');
        });
    }
    
    setupAutoRefresh() {
        if (this.autoRefresh) {
            this.refreshInterval = setInterval(() => {
                this.refreshData();
                this.updateRefreshTimer();
            }, 5000);
        }
    }
    
    updateRefreshTimer() {
        this.refreshTimer--;
        if (this.refreshTimer <= 0) {
            this.refreshTimer = 5;
        }
        document.getElementById('refreshTimer').textContent = this.refreshTimer;
    }
    
    setupEventListeners() {
        // Window beforeunload
        window.addEventListener('beforeunload', () => {
            if (this.ws) this.ws.close();
            if (this.refreshInterval) clearInterval(this.refreshInterval);
            if (this.titleInterval) clearInterval(this.titleInterval);
        });
    }
    
    // UI Actions
    acknowledgeAlarm() {
        if (this.ws && this.ws.readyState === WebSocket.OPEN) {
            this.ws.send(JSON.stringify({
                action: 'acknowledge_alarm'
            }));
        }
        this.hideEmergencyOverlay();
    }
    
    triggerTestAlarm() {
        fetch('/api/alarm/test')
            .then(response => response.json())
            .then(data => {
                this.showNotification('Test alarm triggered', 'info');
            })
            .catch(err => {
                console.error('Error testing alarm:', err);
                this.showNotification('Error testing alarm', 'danger');
            });
    }
    
    stopAlarm() {
        fetch('/api/alarm/stop', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' }
        })
        .then(response => response.json())
        .then(data => {
            this.showNotification('Alarm stopped', 'success');
        })
        .catch(err => {
            console.error('Error stopping alarm:', err);
            this.showNotification('Error stopping alarm', 'danger');
        });
    }
    
    refreshData() {
        this.loadInitialData();
        this.showNotification('Data refreshed', 'info');
    }
    
    simulateESP8266() {
        const testData = {
            deviceID: 'ESP8266-TEST',
            macAddress: 'AA:BB:CC:DD:EE:FF',
            ipAddress: '192.168.18.250',
            buttonPressed: true,
            emergency: true,
            patients: [
                {
                    deviceID: '1',
                    patientName: 'TEST PATIENT',
                    room: 'ICU-01',
                    bed: 'Bed 1',
                    respondedBy: 'System Test'
                }
            ]
        };
        
        fetch('/api/device/data', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify(testData)
        })
        .then(response => response.json())
        .then(data => {
            this.showNotification('ESP8266 simulation sent', 'success');
            console.log('Simulation response:', data);
        })
        .catch(err => {
            console.error('Error simulating ESP8266:', err);
            this.showNotification('Simulation failed', 'danger');
        });
    }
    
    clearAllData() {
        if (confirm('Are you sure you want to clear ALL data? This cannot be undone.')) {
            fetch('/api/clear', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' }
            })
            .then(response => response.json())
            .then(data => {
                this.showNotification('All data cleared', 'warning');
                this.loadPatients();
            })
            .catch(err => {
                console.error('Error clearing data:', err);
                this.showNotification('Error clearing data', 'danger');
            });
        }
    }
    
    // Utility functions
    updateUIStatus(elementId, badgeClass, text) {
        const element = document.getElementById(elementId);
        if (element) {
            element.className = `badge ${badgeClass}`;
            element.textContent = text;
        }
    }
    
    showNotification(message, type = 'info') {
        // Create notification element
        const notification = document.createElement('div');
        notification.className = `alert alert-${type} alert-dismissible fade show position-fixed`;
        notification.style.cssText = 'top: 20px; right: 20px; z-index: 10000; min-width: 300px;';
        notification.innerHTML = `
            ${message}
            <button type="button" class="btn-close" data-bs-dismiss="alert"></button>
        `;
        
        document.body.appendChild(notification);
        
        // Auto-remove after 5 seconds
        setTimeout(() => {
            if (notification.parentNode) {
                notification.remove();
            }
        }, 5000);
    }
    
    formatUptime(seconds) {
        const hours = Math.floor(seconds / 3600);
        const minutes = Math.floor((seconds % 3600) / 60);
        const secs = Math.floor(seconds % 60);
        return `${hours}h ${minutes}m ${secs}s`;
    }
    
    formatFileSize(bytes) {
        if (bytes === 0) return '0 Bytes';
        const k = 1024;
        const sizes = ['Bytes', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    }
    
    updateDashboard() {
        this.updateSystemStatus(this.state);
        this.updateAlarmStatus(this.state.alarm.active);
    }
}

// Initialize dashboard when page loads
document.addEventListener('DOMContentLoaded', () => {
    window.dashboard = new HospitalAlarmDashboard();
});

// Global functions for HTML onclick handlers
function acknowledgeAlarm() {
    if (window.dashboard) window.dashboard.acknowledgeAlarm();
}

function triggerTestAlarm() {
    if (window.dashboard) window.dashboard.triggerTestAlarm();
}

function stopAlarm() {
    if (window.dashboard) window.dashboard.stopAlarm();
}

function refreshData() {
    if (window.dashboard) window.dashboard.refreshData();
}

function simulateESP8266() {
    if (window.dashboard) window.dashboard.simulateESP8266();
}

function clearAllData() {
    if (window.dashboard) window.dashboard.clearAllData();
}