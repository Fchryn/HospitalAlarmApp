using MySql.Data.MySqlClient;
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Threading.Tasks;

namespace HospitalEmergencySystem
{
    public class MySQLSyncService
    {
        private string connectionString = "Server=service.reendoo.com;Database=reendo01_rhocs24;Uid=reendo01_rhocs24;Pwd=I!Q(cs-RUH8hYkH_;Port=3306;";
        private System.Windows.Forms.Timer syncTimer;

        public MySQLSyncService()
        {
            syncTimer = new System.Windows.Forms.Timer();
            syncTimer.Interval = 10000; 
            syncTimer.Tick += async (sender, e) => await SyncToMySQLAsync();
        }

        public void Start()
        {
            syncTimer.Start();
            Console.WriteLine("✅ MySQL Sync Service Started");
        }

        public void Stop()
        {
            syncTimer.Stop();
            Console.WriteLine("🛑 MySQL Sync Service Stopped");
        }

        public async Task SyncToMySQLAsync(Dictionary<string, PatientDevice> patientDevices = null)
        {
            try
            {
                if (patientDevices == null || patientDevices.Count == 0)
                    return;

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    await CreateTableIfNotExists(conn);

                    foreach (var device in patientDevices.Values)
                    {
                        await SyncDeviceToMySQL(conn, device);
                    }

                    Console.WriteLine($"✅ Synced {patientDevices.Count} devices to MySQL");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ MySQL Sync Error: {ex.Message}");
            }
        }

        private async Task CreateTableIfNotExists(MySqlConnection conn)
        {
            string createTableSQL = @"
                CREATE TABLE IF NOT EXISTS pts_emergency (
                    id INT AUTO_INCREMENT PRIMARY KEY,
                    DeviceID VARCHAR(100),
                    MACAddress VARCHAR(50),
                    IPAddress VARCHAR(50) UNIQUE,
                    PatientName VARCHAR(100),
                    RoomNumber VARCHAR(50),
                    BedNumber VARCHAR(50),
                    LastActivity DATETIME,
                    Status VARCHAR(50),
                    Emergency VARCHAR(50),
                    CreatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
                    UpdatedAt TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
                    INDEX idx_ip (IPAddress),
                    INDEX idx_status (Status),
                    INDEX idx_emergency (Emergency)
                ) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4;";

            using (MySqlCommand cmd = new MySqlCommand(createTableSQL, conn))
            {
                await cmd.ExecuteNonQueryAsync();
            }
        }

        private async Task SyncDeviceToMySQL(MySqlConnection conn, PatientDevice device)
        {
            try
            {
                // Cek apakah device sudah ada
                string checkSQL = "SELECT COUNT(*) FROM pts_emergency WHERE IPAddress = @IPAddress";
                bool exists = false;

                using (MySqlCommand checkCmd = new MySqlCommand(checkSQL, conn))
                {
                    checkCmd.Parameters.AddWithValue("@IPAddress", device.IpAddress);
                    object result = await checkCmd.ExecuteScalarAsync();
                    if (result != null && result != DBNull.Value)
                    {
                        exists = Convert.ToInt32(result) > 0;
                    }
                }

                if (exists)
                {
                    // Update existing record
                    string updateSQL = @"
                        UPDATE pts_emergency SET 
                        DeviceID = @DeviceID,
                        MACAddress = @MACAddress,
                        PatientName = @PatientName,
                        RoomNumber = @RoomNumber,
                        BedNumber = @BedNumber,
                        LastActivity = @LastActivity,
                        Status = @Status,
                        Emergency = @Emergency,
                        UpdatedAt = CURRENT_TIMESTAMP
                        WHERE IPAddress = @IPAddress";

                    using (MySqlCommand cmd = new MySqlCommand(updateSQL, conn))
                    {
                        AddDeviceParameters(cmd, device);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
                else
                {
                    // Insert new record
                    string insertSQL = @"
                        INSERT INTO pts_emergency 
                        (DeviceID, MACAddress, IPAddress, PatientName, RoomNumber, BedNumber, LastActivity, Status, Emergency)
                        VALUES 
                        (@DeviceID, @MACAddress, @IPAddress, @PatientName, @RoomNumber, @BedNumber, @LastActivity, @Status, @Emergency)";

                    using (MySqlCommand cmd = new MySqlCommand(insertSQL, conn))
                    {
                        AddDeviceParameters(cmd, device);
                        await cmd.ExecuteNonQueryAsync();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error syncing device {device.IpAddress}: {ex.Message}");
            }
        }

        private void AddDeviceParameters(MySqlCommand cmd, PatientDevice device)
        {
            cmd.Parameters.AddWithValue("@DeviceID", (object)device.DeviceId ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@MACAddress", (object)device.MacAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@IPAddress", (object)device.IpAddress ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@PatientName", (object)device.PatientName ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@RoomNumber", (object)device.RoomNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@BedNumber", (object)device.BedNumber ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@LastActivity", device.LastActivity);
            cmd.Parameters.AddWithValue("@Status", (object)device.Status ?? "READY");
            cmd.Parameters.AddWithValue("@Emergency", (object)device.EmergencyStatus ?? DBNull.Value);
        }

        public async Task<List<PatientDevice>> LoadFromMySQLAsync()
        {
            var devices = new List<PatientDevice>();

            try
            {
                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    string selectSQL = "SELECT * FROM pts_emergency ORDER BY UpdatedAt DESC";

                    using (MySqlCommand cmd = new MySqlCommand(selectSQL, conn))
                    {
                        using (MySqlDataReader reader = cmd.ExecuteReader())
                        {
                            while (await reader.ReadAsync())
                            {
                                var device = new PatientDevice
                                {
                                    DeviceId = reader["DeviceID"] != DBNull.Value ? reader["DeviceID"].ToString() : "",
                                    MacAddress = reader["MACAddress"] != DBNull.Value ? reader["MACAddress"].ToString() : "",
                                    IpAddress = reader["IPAddress"] != DBNull.Value ? reader["IPAddress"].ToString() : "",
                                    PatientName = reader["PatientName"] != DBNull.Value ? reader["PatientName"].ToString() : "",
                                    RoomNumber = reader["RoomNumber"] != DBNull.Value ? reader["RoomNumber"].ToString() : "",
                                    BedNumber = reader["BedNumber"] != DBNull.Value ? reader["BedNumber"].ToString() : "",
                                    LastActivity = reader["LastActivity"] != DBNull.Value ? Convert.ToDateTime(reader["LastActivity"]) : DateTime.Now,
                                    Status = reader["Status"] != DBNull.Value ? reader["Status"].ToString() : "READY",
                                    EmergencyStatus = reader["Emergency"] != DBNull.Value ? reader["Emergency"].ToString() : ""
                                };

                                devices.Add(device);
                            }
                        }
                    }
                }

                Console.WriteLine($"✅ Loaded {devices.Count} devices from MySQL");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error loading from MySQL: {ex.Message}");
            }

            return devices;
        }

        // Fungsi untuk import otomatis dari patient_database.txt ke MySQL
        public async Task AutoImportFromTxtToMySQL(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Console.WriteLine($"❌ File not found: {filePath}");
                    return;
                }

                var lines = File.ReadAllLines(filePath);
                if (lines.Length < 2)
                {
                    Console.WriteLine("❌ No data to import");
                    return;
                }

                using (MySqlConnection conn = new MySqlConnection(connectionString))
                {
                    await conn.OpenAsync();

                    // Skip header line
                    for (int i = 1; i < lines.Length; i++)
                    {
                        var line = lines[i].Trim();
                        if (string.IsNullOrEmpty(line))
                            continue;

                        var parts = line.Split(',');
                        if (parts.Length >= 9)
                        {
                            await AutoImportDevice(conn, parts);
                        }
                    }
                }

                Console.WriteLine($"✅ Auto imported {lines.Length - 1} records from {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Auto Import Error: {ex.Message}");
            }
        }

        private async Task AutoImportDevice(MySqlConnection conn, string[] parts)
        {
            try
            {
                string deviceId = parts[0].Trim();
                string macAddress = parts[1].Trim();
                string ipAddress = parts[2].Trim();
                string patientName = parts[3].Trim();
                string roomNumber = parts[4].Trim();
                string bedNumber = parts[5].Trim();
                
                DateTime lastActivity;
                if (!DateTime.TryParse(parts[6].Trim(), out lastActivity))
                    lastActivity = DateTime.Now;
                    
                string status = parts[7].Trim();
                string emergency = parts[8].Trim();

                string sql = @"
                    INSERT INTO pts_emergency 
                    (DeviceID, MACAddress, IPAddress, PatientName, RoomNumber, BedNumber, LastActivity, Status, Emergency)
                    VALUES 
                    (@DeviceID, @MACAddress, @IPAddress, @PatientName, @RoomNumber, @BedNumber, @LastActivity, @Status, @Emergency)
                    ON DUPLICATE KEY UPDATE
                    DeviceID = VALUES(DeviceID),
                    PatientName = VALUES(PatientName),
                    RoomNumber = VALUES(RoomNumber),
                    BedNumber = VALUES(BedNumber),
                    LastActivity = VALUES(LastActivity),
                    Status = VALUES(Status),
                    Emergency = VALUES(Emergency),
                    UpdatedAt = CURRENT_TIMESTAMP";

                using (MySqlCommand cmd = new MySqlCommand(sql, conn))
                {
                    cmd.Parameters.AddWithValue("@DeviceID", (object)deviceId ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@MACAddress", (object)macAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@IPAddress", (object)ipAddress ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@PatientName", (object)patientName ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@RoomNumber", (object)roomNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@BedNumber", (object)bedNumber ?? DBNull.Value);
                    cmd.Parameters.AddWithValue("@LastActivity", lastActivity);
                    cmd.Parameters.AddWithValue("@Status", (object)status ?? "READY");
                    cmd.Parameters.AddWithValue("@Emergency", (object)emergency ?? DBNull.Value);

                    await cmd.ExecuteNonQueryAsync();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error auto importing device: {ex.Message}");
            }
        }
    }
}