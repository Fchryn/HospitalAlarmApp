-- ============================================
-- HOSPITAL EMERGENCY DATABASE EXPORT
-- Generated: 2026-01-19 15:56:52
-- Source: TXT Database File
-- ============================================

CREATE DATABASE IF NOT EXISTS hospital_emergency;
USE hospital_emergency;

CREATE TABLE IF NOT EXISTS devices (
    id INT AUTO_INCREMENT PRIMARY KEY,
    device_id VARCHAR(100),
    mac_address VARCHAR(20),
    ip_address VARCHAR(20),
    patient_name VARCHAR(100),
    room_number VARCHAR(50),
    bed_number VARCHAR(20),
    status VARCHAR(20),
    emergency_status VARCHAR(20),
    last_activity DATETIME,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP
);

CREATE TABLE IF NOT EXISTS emergency_logs (
    id INT AUTO_INCREMENT PRIMARY KEY,
    device_id VARCHAR(100),
    patient_name VARCHAR(100),
    room_number VARCHAR(50),
    bed_number VARCHAR(20),
    emergency_type VARCHAR(50),
    emergency_start DATETIME,
    emergency_end DATETIME,
    duration_seconds INT,
    handled_by VARCHAR(100),
    notes TEXT,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

TRUNCATE TABLE devices;

-- DEVICES DATA
INSERT INTO devices (device_id, mac_address, ip_address, patient_name, room_number, bed_number, status, emergency_status, last_activity) VALUES
    ('Jam Tangan 001', 'BC:FF:4D:29:D2:95', '192.168.18.251', 'Melpa', 'Room 001', 'Bed 001', 'DISCONNECTED', '', '2026-01-19 14:53:30'),
    ('', '3C:E9:0E:E0:8A:CE', '192.168.18.8', '', '', '', 'READY', '', '2026-01-19 14:53:30'),
    ('Jam Tangan 003', '84:F3:EB:75:60:8C', '192.168.18.36', 'Mona', 'Room 003', 'Bed 003', 'DISCONNECTED', '', '2026-01-19 15:54:55'),
    ('Jam Tangan 004', 'BV:HF:01:70:JK:H0', '192.168.18.103', 'Syifa', 'Room 004', 'Bed 004', 'READY', '', '2026-01-19 14:53:30'),
    ('Jam Tangan 005', '9Q:97:I3:HF:DC:AB', '192.168.18.104', '', '', '', 'READY', '', '2026-01-19 14:53:30'),
    ('Jam Tangan 006', '00:00:00:00:00:00', '192.168.18.105', '', '', '', 'READY', '', '2026-01-19 14:53:30'),
    ('Jam Tangan 007', 'BF:MZ:BX:AN:72:27', '192.168.18.127', '', '', '', 'READY', '', '2026-01-19 14:53:30'),
    ('Jam Tangan 008', '00:00:00:00:00:00', '192.168.18.107', '', '', '', 'READY', '', '2026-01-19 15:26:59');

-- EMERGENCY LOGS (Active Emergencies)
-- No active emergencies
