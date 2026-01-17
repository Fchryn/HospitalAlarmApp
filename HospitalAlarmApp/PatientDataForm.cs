using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;

namespace HospitalEmergencySystem
{
    public partial class PatientDataForm : Form
    {
        // ============================================
        // KONFIGURASI SERVER
        // ============================================
        private TcpListener server;
        private int port = 8080;
        private bool isRunning = false;
        private Thread serverThread;
        private List<ClientHandler> connectedClients = new List<ClientHandler>();

        // ============================================
        // KONFIGURASI ALARM
        // ============================================
        private bool alarmActive = false;
        private Thread alarmThread;
        private int beepFrequency = 1000;
        private int beepDuration = 500;
        private int beepInterval = 500;

        // ============================================
        // DATA PASIEN
        // ============================================
        private Dictionary<string, PatientDevice> patientDevices = new Dictionary<string, PatientDevice>();
        private string databasePath = "patient_database.txt";

        // ============================================
        // DEVICE PRE-REGISTERED (3 Jam Tangan dengan IP dan MAC Default)
        // ============================================
        private List<PreRegisteredDevice> preRegisteredDevices = new List<PreRegisteredDevice>()
        {
            new PreRegisteredDevice { DeviceID = "", MACAddress = "BC:FF:4D:29:D2:95", IPAddress = "192.168.18.251" },
            new PreRegisteredDevice { DeviceID = "", MACAddress = "3C:E9:0E:E0:8A:CE", IPAddress = "192.168.18.8" },
            new PreRegisteredDevice { DeviceID = "", MACAddress = "84:F3:EB:75:60:8C", IPAddress = "192.168.18.36" }
        };

        // ============================================
        // COMPONENTS
        // ============================================
        private ListView listViewPatients;
        private Button btnStartServer;
        private Button btnStopServer;
        private Button btnTestAlarm;
        private Button btnStopAlarm;
        private Button btnBeepTest;
        private Button btnLoadDatabase;
        private Button btnSaveDatabase;
        private Button btnAddNewDevice;
        private Label lblStatus;
        private Label lblConnectedDevices;
        private Label lblAlarmStatus;
        private Label lblServerIP;
        private Panel panelEmergency;
        private TextBox txtEmergencyInfo;
        private System.Windows.Forms.Timer statusTimer;
        private NumericUpDown numBeepFrequency;
        private NumericUpDown numBeepDuration;

        // Dictionary untuk menyimpan warna berdasarkan IP dan MAC Address
        private Dictionary<string, Color> deviceColors = new Dictionary<string, Color>()
        {
            { "192.168.18.251", Color.FromArgb(255, 200, 200) }, // Merah muda untuk IP 251
            { "192.168.18.8", Color.FromArgb(200, 255, 200) },   // Hijau muda untuk IP 8
            { "192.168.18.36", Color.FromArgb(200, 200, 255) }   // Biru muda untuk IP 36
        };

        // Dictionary untuk mapping MAC Address ke IP Address
        private Dictionary<string, string> macToIpMapping = new Dictionary<string, string>()
        {
            { "BC:FF:4D:29:D2:95", "192.168.18.251" },
            { "3C:E9:0E:E0:8A:CE", "192.168.18.8" },
            { "84:F3:EB:75:60:8C", "192.168.18.36" }
        };

        // Variable untuk menyimpan urutan device berdasarkan waktu penambahan
        private List<string> deviceOrder = new List<string>();

        public PatientDataForm()
        {
            InitializeComponent();
            SetupComponents();
            LoadPatientDatabase();
            InitializePreRegisteredDevices();
        }

        private void SetupComponents()
        {
            // Title Label
            Label titleLabel = new Label();
            titleLabel.Text = "🏥 HOSPITAL EMERGENCY MONITORING SYSTEM";
            titleLabel.Font = new Font("Segoe UI", 20, FontStyle.Bold);
            titleLabel.ForeColor = Color.DarkBlue;
            titleLabel.Size = new Size(1100, 45);
            titleLabel.Location = new Point(50, 20);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(titleLabel);

            Label subtitleLabel = new Label();
            subtitleLabel.Text = "ESP8266 Bracelet - Real-time Patient Monitoring with CRUD System";
            subtitleLabel.Font = new Font("Segoe UI", 11, FontStyle.Italic);
            subtitleLabel.ForeColor = Color.DarkSlateGray;
            subtitleLabel.Size = new Size(1100, 25);
            subtitleLabel.Location = new Point(50, 65);
            subtitleLabel.TextAlign = ContentAlignment.MiddleCenter;
            this.Controls.Add(subtitleLabel);

            // Server Info Panel
            Panel serverPanel = new Panel();
            serverPanel.BorderStyle = BorderStyle.FixedSingle;
            serverPanel.BackColor = Color.Lavender;
            serverPanel.Size = new Size(1100, 100);
            serverPanel.Location = new Point(50, 100);

            lblStatus = new Label();
            lblStatus.Text = "🔴 Server Status: Stopped";
            lblStatus.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblStatus.ForeColor = Color.Red;
            lblStatus.Size = new Size(300, 25);
            lblStatus.Location = new Point(20, 15);
            serverPanel.Controls.Add(lblStatus);

            lblServerIP = new Label();
            lblServerIP.Text = "Server IP: Checking...";
            lblServerIP.Font = new Font("Segoe UI", 10);
            lblServerIP.Size = new Size(300, 25);
            lblServerIP.Location = new Point(20, 45);
            serverPanel.Controls.Add(lblServerIP);

            lblConnectedDevices = new Label();
            lblConnectedDevices.Text = "Connected Devices: 0";
            lblConnectedDevices.Font = new Font("Segoe UI", 10);
            lblConnectedDevices.Size = new Size(200, 25);
            lblConnectedDevices.Location = new Point(350, 15);
            serverPanel.Controls.Add(lblConnectedDevices);

            lblAlarmStatus = new Label();
            lblAlarmStatus.Text = "Alarm: IDLE";
            lblAlarmStatus.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            lblAlarmStatus.ForeColor = Color.Green;
            lblAlarmStatus.Size = new Size(200, 25);
            lblAlarmStatus.Location = new Point(350, 45);
            serverPanel.Controls.Add(lblAlarmStatus);

            // Beep Settings
            Label lblFrequency = new Label();
            lblFrequency.Text = "Frequency (Hz):";
            lblFrequency.Size = new Size(100, 25);
            lblFrequency.Location = new Point(600, 15);
            serverPanel.Controls.Add(lblFrequency);

            numBeepFrequency = new NumericUpDown();
            numBeepFrequency.Minimum = 10;
            numBeepFrequency.Maximum = 2000;
            numBeepFrequency.Value = beepFrequency;
            numBeepFrequency.Width = 80;
            numBeepFrequency.Location = new Point(710, 15);
            numBeepFrequency.ValueChanged += NumBeepFrequency_ValueChanged;
            serverPanel.Controls.Add(numBeepFrequency);

            Label lblDuration = new Label();
            lblDuration.Text = "Duration (ms):";
            lblDuration.Size = new Size(100, 25);
            lblDuration.Location = new Point(600, 45);
            serverPanel.Controls.Add(lblDuration);

            numBeepDuration = new NumericUpDown();
            numBeepDuration.Minimum = 10;
            numBeepDuration.Maximum = 3000;
            numBeepDuration.Value = beepDuration;
            numBeepDuration.Width = 80;
            numBeepDuration.Location = new Point(710, 45);
            numBeepDuration.ValueChanged += NumBeepDuration_ValueChanged;
            serverPanel.Controls.Add(numBeepDuration);

            // Database Buttons
            btnLoadDatabase = new Button();
            btnLoadDatabase.Text = "📂 Load DB";
            btnLoadDatabase.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnLoadDatabase.BackColor = Color.LightSkyBlue;
            btnLoadDatabase.Size = new Size(80, 30);
            btnLoadDatabase.Location = new Point(800, 15);
            btnLoadDatabase.Click += BtnLoadDatabase_Click;
            serverPanel.Controls.Add(btnLoadDatabase);

            btnSaveDatabase = new Button();
            btnSaveDatabase.Text = "💾 Save DB";
            btnSaveDatabase.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnSaveDatabase.BackColor = Color.LightGreen;
            btnSaveDatabase.Size = new Size(80, 30);
            btnSaveDatabase.Location = new Point(890, 15);
            btnSaveDatabase.Click += BtnSaveDatabase_Click;
            serverPanel.Controls.Add(btnSaveDatabase);

            btnAddNewDevice = new Button();
            btnAddNewDevice.Text = "➕ Add Device";
            btnAddNewDevice.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            btnAddNewDevice.BackColor = Color.LightGoldenrodYellow;
            btnAddNewDevice.Size = new Size(100, 30);
            btnAddNewDevice.Location = new Point(980, 15);
            btnAddNewDevice.Click += BtnAddNewDevice_Click;
            serverPanel.Controls.Add(btnAddNewDevice);

            this.Controls.Add(serverPanel);

            // Control Buttons Panel
            Panel controlPanel = new Panel();
            controlPanel.Size = new Size(1100, 60);
            controlPanel.Location = new Point(50, 210);

            btnStartServer = new Button();
            btnStartServer.Text = "▶ Start Server";
            btnStartServer.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnStartServer.BackColor = Color.LightGreen;
            btnStartServer.ForeColor = Color.Black;
            btnStartServer.Size = new Size(120, 40);
            btnStartServer.Location = new Point(10, 10);
            btnStartServer.Click += BtnStartServer_Click;
            controlPanel.Controls.Add(btnStartServer);

            btnStopServer = new Button();
            btnStopServer.Text = "⏹ Stop Server";
            btnStopServer.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnStopServer.BackColor = Color.LightCoral;
            btnStopServer.ForeColor = Color.Black;
            btnStopServer.Size = new Size(120, 40);
            btnStopServer.Location = new Point(140, 10);
            btnStopServer.Click += BtnStopServer_Click;
            btnStopServer.Enabled = false;
            controlPanel.Controls.Add(btnStopServer);

            btnTestAlarm = new Button();
            btnTestAlarm.Text = "🔊 Test Alarm";
            btnTestAlarm.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnTestAlarm.BackColor = Color.LightYellow;
            btnTestAlarm.ForeColor = Color.Black;
            btnTestAlarm.Size = new Size(120, 40);
            btnTestAlarm.Location = new Point(270, 10);
            btnTestAlarm.Click += BtnTestAlarm_Click;
            controlPanel.Controls.Add(btnTestAlarm);

            btnBeepTest = new Button();
            btnBeepTest.Text = "🔈 Test Beep";
            btnBeepTest.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnBeepTest.BackColor = Color.LightSkyBlue;
            btnBeepTest.ForeColor = Color.Black;
            btnBeepTest.Size = new Size(120, 40);
            btnBeepTest.Location = new Point(400, 10);
            btnBeepTest.Click += BtnBeepTest_Click;
            controlPanel.Controls.Add(btnBeepTest);

            btnStopAlarm = new Button();
            btnStopAlarm.Text = "⏹ Stop Alarm";
            btnStopAlarm.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnStopAlarm.BackColor = Color.LightPink;
            btnStopAlarm.ForeColor = Color.Black;
            btnStopAlarm.Size = new Size(120, 40);
            btnStopAlarm.Location = new Point(530, 10);
            btnStopAlarm.Click += BtnStopAlarm_Click;
            controlPanel.Controls.Add(btnStopAlarm);

            this.Controls.Add(controlPanel);

            // Emergency Panel
            panelEmergency = new Panel();
            panelEmergency.BackColor = Color.Red;
            panelEmergency.BorderStyle = BorderStyle.FixedSingle;
            panelEmergency.Size = new Size(1100, 90);
            panelEmergency.Location = new Point(50, 280);
            panelEmergency.Visible = false;

            Label emergencyLabel = new Label();
            emergencyLabel.Text = "🚨 EMERGENCY ALARM ACTIVE 🚨";
            emergencyLabel.Font = new Font("Segoe UI", 18, FontStyle.Bold);
            emergencyLabel.ForeColor = Color.White;
            emergencyLabel.Size = new Size(500, 40);
            emergencyLabel.Location = new Point(20, 10);
            emergencyLabel.TextAlign = ContentAlignment.MiddleCenter;
            panelEmergency.Controls.Add(emergencyLabel);

            txtEmergencyInfo = new TextBox();
            txtEmergencyInfo.Multiline = true;
            txtEmergencyInfo.ReadOnly = true;
            txtEmergencyInfo.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            txtEmergencyInfo.BackColor = Color.Red;
            txtEmergencyInfo.ForeColor = Color.White;
            txtEmergencyInfo.BorderStyle = BorderStyle.None;
            txtEmergencyInfo.Size = new Size(550, 60);
            txtEmergencyInfo.Location = new Point(530, 10);
            panelEmergency.Controls.Add(txtEmergencyInfo);

            this.Controls.Add(panelEmergency);

            // Patient List View
            Label patientLabel = new Label();
            patientLabel.Text = "📋 CONNECTED PATIENTS & DEVICES (Click Edit/Delete buttons to manage)";
            patientLabel.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            patientLabel.Size = new Size(600, 30);
            patientLabel.Location = new Point(50, 380);
            this.Controls.Add(patientLabel);

            listViewPatients = new ListView();
            listViewPatients.View = View.Details;
            listViewPatients.FullRowSelect = true;
            listViewPatients.GridLines = true;
            listViewPatients.Size = new Size(1100, 250);
            listViewPatients.Location = new Point(50, 410);

            // Add columns
            listViewPatients.Columns.Add("No", 50);
            listViewPatients.Columns.Add("Device ID", 150);
            listViewPatients.Columns.Add("MAC Address", 150);
            listViewPatients.Columns.Add("IP Address", 120);
            listViewPatients.Columns.Add("Patient Name", 150);
            listViewPatients.Columns.Add("Room", 100);
            listViewPatients.Columns.Add("Bed", 80);
            listViewPatients.Columns.Add("Status", 100);
            listViewPatients.Columns.Add("Last Activity", 120);
            listViewPatients.Columns.Add("Emergency", 100);
            listViewPatients.Columns.Add("Actions", 150);

            this.Controls.Add(listViewPatients);

            // Status Timer
            statusTimer = new System.Windows.Forms.Timer();
            statusTimer.Interval = 1000;
            statusTimer.Tick += StatusTimer_Tick;
            statusTimer.Start();

            // Update server IP label
            UpdateServerIP();

            // Set window size
            this.Size = new Size(1200, 700);
            this.StartPosition = FormStartPosition.CenterScreen;
        }

        private void InitializePreRegisteredDevices()
        {
            foreach (var preDevice in preRegisteredDevices)
            {
                if (!patientDevices.ContainsKey(preDevice.IPAddress))
                {
                    patientDevices[preDevice.IPAddress] = new PatientDevice
                    {
                        DeviceId = "",              // DIKOSONGKAN
                        MacAddress = preDevice.MACAddress,
                        IpAddress = preDevice.IPAddress,
                        PatientName = "",           // DIKOSONGKAN
                        RoomNumber = "",            // DIKOSONGKAN
                        BedNumber = "",             // DIKOSONGKAN
                        Status = "READY",
                        LastActivity = DateTime.Now,
                        EmergencyStatus = ""
                    };

                    // Tambahkan ke urutan device
                    deviceOrder.Add(preDevice.IPAddress);
                }
            }
            UpdatePatientListView();
        }

        private void UpdateServerIP()
        {
            try
            {
                string localIP = GetLocalIPAddress();
                lblServerIP.Text = $"Server IP: {localIP}:{port}";
                lblServerIP.ForeColor = Color.DarkBlue;
            }
            catch (Exception ex)
            {
                lblServerIP.Text = $"Server IP: Error - {ex.Message}";
                lblServerIP.ForeColor = Color.Red;
            }
        }

        private void NumBeepFrequency_ValueChanged(object sender, EventArgs e)
        {
            beepFrequency = (int)numBeepFrequency.Value;
            LogMessage($"Beep frequency changed to {beepFrequency} Hz");
        }

        private void NumBeepDuration_ValueChanged(object sender, EventArgs e)
        {
            beepDuration = (int)numBeepDuration.Value;
            LogMessage($"Beep duration changed to {beepDuration} ms");
        }

        // ============================================
        // DATABASE FUNCTIONS
        // ============================================
        private void LoadPatientDatabase()
        {
            try
            {
                if (File.Exists(databasePath))
                {
                    string[] lines = File.ReadAllLines(databasePath);
                    int loadedCount = 0;

                    // Skip header line
                    for (int i = 1; i < lines.Length; i++)
                    {
                        if (string.IsNullOrWhiteSpace(lines[i])) continue;

                        string[] parts = lines[i].Split(',');
                        if (parts.Length >= 9)
                        {
                            string ipAddress = parts[2].Trim();
                            string deviceId = parts[0].Trim();
                            string macAddress = parts[1].Trim();
                            string patientName = parts[3].Trim();
                            string roomNumber = parts[4].Trim();
                            string bedNumber = parts[5].Trim();
                            string status = parts[7].Trim();
                            string emergencyStatus = parts[8].Trim();

                            if (!patientDevices.ContainsKey(ipAddress))
                            {
                                patientDevices[ipAddress] = new PatientDevice
                                {
                                    DeviceId = deviceId,
                                    MacAddress = macAddress,
                                    IpAddress = ipAddress,
                                    PatientName = patientName,
                                    RoomNumber = roomNumber,
                                    BedNumber = bedNumber,
                                    Status = status,
                                    LastActivity = DateTime.Now,
                                    EmergencyStatus = emergencyStatus
                                };

                                // Tambahkan ke urutan device
                                deviceOrder.Add(ipAddress);
                            }
                            else
                            {
                                // Update existing device
                                patientDevices[ipAddress].DeviceId = deviceId;
                                patientDevices[ipAddress].PatientName = patientName;
                                patientDevices[ipAddress].RoomNumber = roomNumber;
                                patientDevices[ipAddress].BedNumber = bedNumber;
                                patientDevices[ipAddress].Status = status;
                                patientDevices[ipAddress].EmergencyStatus = emergencyStatus;
                            }

                            loadedCount++;
                        }
                    }

                    UpdatePatientListView();
                    LogMessage($"✅ Loaded {loadedCount} patient records from database");
                }
                else
                {
                    LogMessage("📝 Database file not found. Creating new database...");
                    SavePatientDatabase();
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error loading database: {ex.Message}");
            }
        }

        private void SavePatientDatabase()
        {
            try
            {
                using (StreamWriter writer = new StreamWriter(databasePath, false))
                {
                    writer.WriteLine("DeviceID,MACAddress,IPAddress,PatientName,Room Number,Bed Number,Last Activity,Status,Emergency");

                    // Simpan berdasarkan urutan di deviceOrder
                    foreach (var ipAddress in deviceOrder)
                    {
                        if (patientDevices.ContainsKey(ipAddress))
                        {
                            var device = patientDevices[ipAddress];
                            writer.WriteLine($"{device.DeviceId}," +
                                           $"{device.MacAddress}," +
                                           $"{device.IpAddress}," +
                                           $"{device.PatientName}," +
                                           $"{device.RoomNumber}," +
                                           $"{device.BedNumber}," +
                                           $"{device.LastActivity:yyyy-MM-dd HH:mm:ss}," +
                                           $"{device.Status}," +
                                           $"{device.EmergencyStatus}");
                        }
                    }
                }

                LogMessage($"💾 Saved {patientDevices.Count} patient records to database");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error saving database: {ex.Message}");
            }
        }

        private void BtnLoadDatabase_Click(object sender, EventArgs e)
        {
            LoadPatientDatabase();
        }

        private void BtnSaveDatabase_Click(object sender, EventArgs e)
        {
            SavePatientDatabase();
            MessageBox.Show("Database saved successfully!", "Save Complete",
                          MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void BtnAddNewDevice_Click(object sender, EventArgs e)
        {
            // Form untuk menambah device baru
            Form addForm = new Form();
            addForm.Text = "➕ Add New Device";
            addForm.Size = new Size(400, 250);
            addForm.StartPosition = FormStartPosition.CenterParent;
            addForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            addForm.MaximizeBox = false;
            addForm.MinimizeBox = false;

            // Hitung nomor urut berikutnya
            int nextDeviceNumber = patientDevices.Count + 1;

            Label lblDeviceId = new Label();
            lblDeviceId.Text = "Device ID:";
            lblDeviceId.Location = new Point(20, 30);
            lblDeviceId.Size = new Size(100, 25);
            addForm.Controls.Add(lblDeviceId);

            TextBox txtDeviceId = new TextBox();
            txtDeviceId.Text = $"BRACELET_{nextDeviceNumber:00}";
            txtDeviceId.Location = new Point(130, 30);
            txtDeviceId.Size = new Size(200, 25);
            addForm.Controls.Add(txtDeviceId);

            Label lblMac = new Label();
            lblMac.Text = "MAC Address:";
            lblMac.Location = new Point(20, 70);
            lblMac.Size = new Size(100, 25);
            addForm.Controls.Add(lblMac);

            TextBox txtMac = new TextBox();
            txtMac.Text = "00:00:00:00:00:00";
            txtMac.Location = new Point(130, 70);
            txtMac.Size = new Size(200, 25);
            addForm.Controls.Add(txtMac);

            Label lblIp = new Label();
            lblIp.Text = "IP Address:";
            lblIp.Location = new Point(20, 110);
            lblIp.Size = new Size(100, 25);
            addForm.Controls.Add(lblIp);

            TextBox txtIp = new TextBox();
            txtIp.Text = $"192.168.18.{100 + patientDevices.Count}";
            txtIp.Location = new Point(130, 110);
            txtIp.Size = new Size(200, 25);
            addForm.Controls.Add(txtIp);

            Button btnAdd = new Button();
            btnAdd.Text = "➕ Add";
            btnAdd.Location = new Point(100, 160);
            btnAdd.Size = new Size(100, 30);
            btnAdd.BackColor = Color.LightGreen;
            btnAdd.Click += (s, e2) =>
            {
                if (!patientDevices.ContainsKey(txtIp.Text))
                {
                    patientDevices[txtIp.Text] = new PatientDevice
                    {
                        DeviceId = txtDeviceId.Text,
                        MacAddress = txtMac.Text,
                        IpAddress = txtIp.Text,
                        PatientName = "",
                        RoomNumber = "",
                        BedNumber = "",
                        Status = "READY",
                        LastActivity = DateTime.Now,
                        EmergencyStatus = ""
                    };

                    // Tambahkan IP ke akhir daftar urutan
                    deviceOrder.Add(txtIp.Text);

                    UpdatePatientListView();
                    SavePatientDatabase();
                    LogMessage($"✅ Added new device: {txtDeviceId.Text} at position {patientDevices.Count}");
                    addForm.Close();
                }
                else
                {
                    MessageBox.Show("Device with this IP already exists!", "Error",
                                  MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            };
            addForm.Controls.Add(btnAdd);

            Button btnCancel = new Button();
            btnCancel.Text = "❌ Cancel";
            btnCancel.Location = new Point(210, 160);
            btnCancel.Size = new Size(100, 30);
            btnCancel.BackColor = Color.LightCoral;
            btnCancel.Click += (s, e2) => addForm.Close();
            addForm.Controls.Add(btnCancel);

            addForm.ShowDialog();
        }

        // ============================================
        // CRUD OPERATIONS - EDIT & DELETE BUTTONS 
        // DENGAN FORM EDIT LENGKAP (SAVE, EXIT, CANCEL)
        // ============================================
        private void UpdatePatientListView()
        {
            listViewPatients.BeginUpdate();
            listViewPatients.Items.Clear();

            int counter = 1;

            // Tampilkan device berdasarkan urutan di deviceOrder
            foreach (var ipAddress in deviceOrder)
            {
                if (patientDevices.ContainsKey(ipAddress))
                {
                    var device = patientDevices[ipAddress];

                    ListViewItem item = new ListViewItem(counter.ToString());
                    item.SubItems.Add(device.DeviceId);
                    item.SubItems.Add(device.MacAddress);
                    item.SubItems.Add(device.IpAddress);
                    item.SubItems.Add(device.PatientName);
                    item.SubItems.Add(device.RoomNumber);
                    item.SubItems.Add(device.BedNumber);
                    item.SubItems.Add(device.Status);
                    item.SubItems.Add(device.LastActivity.ToString("HH:mm:ss"));
                    item.SubItems.Add(device.EmergencyStatus);

                    // Add Edit and Delete buttons as subitem
                    item.SubItems.Add("[Edit] [Delete]");

                    // WARNA SESUAI DENGAN IP ADDRESS DAN STATUS
                    Color baseColor = GetDeviceColor(device.IpAddress, device.MacAddress);

                    if (device.EmergencyStatus == "ACTIVE")
                    {
                        // WARNA MERAH GELAP untuk emergency aktif
                        item.BackColor = Color.Red;
                        item.ForeColor = Color.White;
                        item.Font = new Font(listViewPatients.Font, FontStyle.Bold);
                    }
                    else if (device.Status == "CONNECTED")
                    {
                        // WARNA HIJAU dengan gradasi sesuai IP
                        item.BackColor = LightenColor(baseColor, 0.3f); // Lebih terang
                        item.ForeColor = Color.Black;
                    }
                    else if (device.Status == "EMERGENCY")
                    {
                        // WARNA ORANGE untuk status emergency (tapi bukan active)
                        item.BackColor = Color.Orange;
                        item.ForeColor = Color.Black;
                        item.Font = new Font(listViewPatients.Font, FontStyle.Bold);
                    }
                    else if (device.Status == "READY")
                    {
                        // WARNA KUNING dengan gradasi sesuai IP
                        item.BackColor = LightenColor(baseColor, 0.5f); // Lebih terang lagi
                        item.ForeColor = Color.Black;
                    }
                    else if (device.Status == "DISCONNECTED")
                    {
                        // WARNA ABU-ABU untuk disconnected
                        item.BackColor = Color.LightGray;
                        item.ForeColor = Color.DarkGray;
                    }
                    else
                    {
                        item.BackColor = Color.White;
                        item.ForeColor = Color.Black;
                    }

                    // Simpan IP dan MAC sebagai tag untuk identifikasi
                    item.Tag = new DeviceTag
                    {
                        Device = device,
                        IpAddress = device.IpAddress,
                        MacAddress = device.MacAddress,
                        DisplayOrder = counter
                    };

                    listViewPatients.Items.Add(item);
                    counter++;
                }
            }

            listViewPatients.EndUpdate();
            listViewPatients.Refresh();

            // Add event handler for mouse click on listview
            listViewPatients.MouseClick += ListViewPatients_MouseClick;
        }

        // Fungsi untuk mendapatkan warna berdasarkan IP Address
        private Color GetDeviceColor(string ipAddress, string macAddress)
        {
            // Cek berdasarkan IP Address terlebih dahulu
            if (deviceColors.ContainsKey(ipAddress))
            {
                return deviceColors[ipAddress];
            }

            // Jika IP tidak ditemukan, cek berdasarkan MAC Address
            if (macToIpMapping.ContainsKey(macAddress))
            {
                string mappedIp = macToIpMapping[macAddress];
                if (deviceColors.ContainsKey(mappedIp))
                {
                    return deviceColors[mappedIp];
                }
            }

            // Default color jika tidak ditemukan
            return Color.LightGray;
        }

        // Fungsi untuk membuat warna lebih terang
        private Color LightenColor(Color color, float factor)
        {
            float red = (color.R * (1 - factor) + 255 * factor);
            float green = (color.G * (1 - factor) + 255 * factor);
            float blue = (color.B * (1 - factor) + 255 * factor);

            return Color.FromArgb(
                Math.Min(255, (int)red),
                Math.Min(255, (int)green),
                Math.Min(255, (int)blue)
            );
        }

        // Class untuk menyimpan tag device
        private class DeviceTag
        {
            public PatientDevice Device { get; set; }
            public string IpAddress { get; set; }
            public string MacAddress { get; set; }
            public int DisplayOrder { get; set; }
        }

        private void ListViewPatients_MouseClick(object sender, MouseEventArgs e)
        {
            var hitTest = listViewPatients.HitTest(e.Location);
            if (hitTest.Item != null && hitTest.SubItem != null)
            {
                // Check if click is on the Actions column (index 10)
                if (hitTest.Item.SubItems.Count > 10 && hitTest.Item.SubItems[10] == hitTest.SubItem)
                {
                    var deviceTag = hitTest.Item.Tag as DeviceTag;
                    if (deviceTag == null) return;

                    var device = deviceTag.Device;

                    // Calculate button positions
                    int editButtonStart = hitTest.SubItem.Bounds.Left;
                    int deleteButtonStart = hitTest.SubItem.Bounds.Left + 50;

                    if (e.X >= editButtonStart && e.X < editButtonStart + 50)
                    {
                        // Edit button clicked
                        EditPatientData(device);
                    }
                    else if (e.X >= deleteButtonStart && e.X < deleteButtonStart + 60)
                    {
                        // Delete button clicked
                        DeletePatientData(device);
                    }
                }
            }
        }

        private void EditPatientData(PatientDevice device)
        {
            if (device == null) return;

            // Create edit form with SAVE, EXIT, and CANCEL buttons
            Form editForm = new Form();
            editForm.Text = "✏️ Edit Patient Data";
            editForm.Size = new Size(500, 400);
            editForm.StartPosition = FormStartPosition.CenterParent;
            editForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            editForm.MaximizeBox = false;
            editForm.MinimizeBox = false;

            // Title
            Label titleLabel = new Label();
            titleLabel.Text = "EDIT PATIENT DATA";
            titleLabel.Font = new Font("Segoe UI", 14, FontStyle.Bold);
            titleLabel.ForeColor = Color.DarkBlue;
            titleLabel.Size = new Size(450, 30);
            titleLabel.Location = new Point(10, 10);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            editForm.Controls.Add(titleLabel);

            // Device ID (Editable)
            Label lblDeviceId = new Label();
            lblDeviceId.Text = "Device ID:";
            lblDeviceId.Font = new Font("Segoe UI", 10);
            lblDeviceId.Location = new Point(20, 60);
            lblDeviceId.Size = new Size(120, 25);
            editForm.Controls.Add(lblDeviceId);

            TextBox txtDeviceId = new TextBox();
            txtDeviceId.Text = device.DeviceId;
            txtDeviceId.Font = new Font("Segoe UI", 10);
            txtDeviceId.Location = new Point(150, 60);
            txtDeviceId.Size = new Size(300, 25);
            editForm.Controls.Add(txtDeviceId);

            // MAC Address (Read Only)
            Label lblMac = new Label();
            lblMac.Text = "MAC Address:";
            lblMac.Font = new Font("Segoe UI", 10);
            lblMac.Location = new Point(20, 100);
            lblMac.Size = new Size(120, 25);
            editForm.Controls.Add(lblMac);

            TextBox txtMac = new TextBox();
            txtMac.Text = device.MacAddress;
            txtMac.Font = new Font("Segoe UI", 10);
            txtMac.Location = new Point(150, 100);
            txtMac.Size = new Size(300, 25);
            txtMac.ReadOnly = true;
            txtMac.BackColor = Color.LightGray;
            editForm.Controls.Add(txtMac);

            // IP Address (Read Only)
            Label lblIp = new Label();
            lblIp.Text = "IP Address:";
            lblIp.Font = new Font("Segoe UI", 10);
            lblIp.Location = new Point(20, 140);
            lblIp.Size = new Size(120, 25);
            editForm.Controls.Add(lblIp);

            TextBox txtIp = new TextBox();
            txtIp.Text = device.IpAddress;
            txtIp.Font = new Font("Segoe UI", 10);
            txtIp.Location = new Point(150, 140);
            txtIp.Size = new Size(300, 25);
            txtIp.ReadOnly = true;
            txtIp.BackColor = Color.LightGray;
            editForm.Controls.Add(txtIp);

            // Patient Name (Editable)
            Label lblPatient = new Label();
            lblPatient.Text = "Patient Name:";
            lblPatient.Font = new Font("Segoe UI", 10);
            lblPatient.Location = new Point(20, 180);
            lblPatient.Size = new Size(120, 25);
            editForm.Controls.Add(lblPatient);

            TextBox txtPatient = new TextBox();
            txtPatient.Text = device.PatientName;
            txtPatient.Font = new Font("Segoe UI", 10);
            txtPatient.Location = new Point(150, 180);
            txtPatient.Size = new Size(300, 25);
            editForm.Controls.Add(txtPatient);

            // Room Number (Editable)
            Label lblRoom = new Label();
            lblRoom.Text = "Room Number:";
            lblRoom.Font = new Font("Segoe UI", 10);
            lblRoom.Location = new Point(20, 220);
            lblRoom.Size = new Size(120, 25);
            editForm.Controls.Add(lblRoom);

            TextBox txtRoom = new TextBox();
            txtRoom.Text = device.RoomNumber;
            txtRoom.Font = new Font("Segoe UI", 10);
            txtRoom.Location = new Point(150, 220);
            txtRoom.Size = new Size(300, 25);
            editForm.Controls.Add(txtRoom);

            // Bed Number (Editable)
            Label lblBed = new Label();
            lblBed.Text = "Bed Number:";
            lblBed.Font = new Font("Segoe UI", 10);
            lblBed.Location = new Point(20, 260);
            lblBed.Size = new Size(120, 25);
            editForm.Controls.Add(lblBed);

            TextBox txtBed = new TextBox();
            txtBed.Text = device.BedNumber;
            txtBed.Font = new Font("Segoe UI", 10);
            txtBed.Location = new Point(150, 260);
            txtBed.Size = new Size(300, 25);
            editForm.Controls.Add(txtBed);

            // Separator Line
            Label separator = new Label();
            separator.BorderStyle = BorderStyle.Fixed3D;
            separator.Size = new Size(460, 2);
            separator.Location = new Point(10, 300);
            editForm.Controls.Add(separator);

            // BUTTON PANEL
            Panel buttonPanel = new Panel();
            buttonPanel.Size = new Size(460, 50);
            buttonPanel.Location = new Point(10, 310);

            // SAVE Button
            Button btnSave = new Button();
            btnSave.Text = "💾 SAVE";
            btnSave.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnSave.Location = new Point(50, 10);
            btnSave.Size = new Size(100, 35);
            btnSave.BackColor = Color.LightGreen;
            btnSave.ForeColor = Color.Black;
            btnSave.Click += (s, e2) =>
            {
                device.DeviceId = txtDeviceId.Text;
                device.PatientName = txtPatient.Text;
                device.RoomNumber = txtRoom.Text;
                device.BedNumber = txtBed.Text;

                UpdatePatientListView();
                SavePatientDatabase();

                // Send update to device if connected
                SendPatientDataToDevice(device);

                LogMessage($"✅ Updated patient data for {device.DeviceId}");

                MessageBox.Show("Patient data saved successfully!", "Save Complete",
                              MessageBoxButtons.OK, MessageBoxIcon.Information);

                editForm.Close(); // TUTUP FORM SETELAH SAVE
            };
            buttonPanel.Controls.Add(btnSave);

            // EXIT Button - LANGSUNG TUTUP FORM
            Button btnExit = new Button();
            btnExit.Text = "🚪 EXIT";
            btnExit.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnExit.Location = new Point(160, 10);
            btnExit.Size = new Size(100, 35);
            btnExit.BackColor = Color.LightCoral;
            btnExit.ForeColor = Color.Black;
            btnExit.Click += (s, e2) =>
            {
                editForm.Close(); // LANGSUNG TUTUP TANPA KONFIRMASI
            };
            buttonPanel.Controls.Add(btnExit);

            // CANCEL Button - DENGAN KONFIRMASI
            Button btnCancel = new Button();
            btnCancel.Text = "❌ CANCEL";
            btnCancel.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            btnCancel.Location = new Point(270, 10);
            btnCancel.Size = new Size(100, 35);
            btnCancel.BackColor = Color.LightGray;
            btnCancel.ForeColor = Color.Black;
            btnCancel.Click += (s, e2) =>
            {
                if (MessageBox.Show("Are you sure you want to cancel without saving?",
                    "Confirm Cancel", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    editForm.Close();
                }
            };
            buttonPanel.Controls.Add(btnCancel);

            editForm.Controls.Add(buttonPanel);

            editForm.ShowDialog();
        }

        private void SendPatientDataToDevice(PatientDevice device)
        {
            // Send updated patient data to the device
            string message = $"{{\"status\":\"DATA_UPDATE\"," +
                           $"\"device_id\":\"{device.DeviceId}\"," +
                           $"\"patient_name\":\"{device.PatientName}\"," +
                           $"\"room_number\":\"{device.RoomNumber}\"," +
                           $"\"bed_number\":\"{device.BedNumber}\"," +
                           $"\"timestamp\":\"{DateTime.Now:HH:mm:ss}\"}}";
            SendToDevice(device.IpAddress, message);
        }

        private void DeletePatientData(PatientDevice device)
        {
            if (device == null) return;

            DialogResult result = MessageBox.Show(
                $"Are you sure you want to clear patient data for this device?\n\n" +
                $"Device: {device.DeviceId}\n" +
                $"IP: {device.IpAddress}\n" +
                $"MAC: {device.MacAddress}\n" +
                $"Patient: {device.PatientName}\n" +
                $"Room: {device.RoomNumber}\n" +
                $"Bed: {device.BedNumber}",
                "Confirm Clear Data",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Yes)
            {
                // Clear patient data but keep device info
                device.DeviceId = "";
                device.PatientName = "";
                device.RoomNumber = "";
                device.BedNumber = "";
                device.Status = "READY";
                device.EmergencyStatus = "";

                // Hapus dari urutan deviceOrder jika ingin menghapus sepenuhnya
                // deviceOrder.Remove(device.IpAddress);
                // patientDevices.Remove(device.IpAddress);

                UpdatePatientListView();
                SavePatientDatabase();

                // Send clear command to device
                SendToDevice(device.IpAddress, $"{{\"status\":\"DATA_CLEAR\",\"message\":\"Patient data cleared\",\"timestamp\":\"{DateTime.Now:HH:mm:ss}\"}}");

                LogMessage($"🗑️ Cleared patient data for device at IP: {device.IpAddress}");
            }
        }

        // ============================================
        // SERVER FUNCTIONS
        // ============================================
        private void BtnStartServer_Click(object sender, EventArgs e)
        {
            StartServer();
        }

        private void BtnStopServer_Click(object sender, EventArgs e)
        {
            StopServer();
        }

        private void StartServer()
        {
            try
            {
                server = new TcpListener(IPAddress.Any, port);
                server.Start();
                isRunning = true;

                serverThread = new Thread(new ThreadStart(ListenForClients));
                serverThread.IsBackground = true;
                serverThread.Start();

                lblStatus.Text = $"🟢 Server Running on {GetLocalIPAddress()}:{port}";
                lblStatus.ForeColor = Color.Green;
                btnStartServer.Enabled = false;
                btnStopServer.Enabled = true;

                LogMessage($"✅ SERVER STARTED on {GetLocalIPAddress()}:{port}");
                LogMessage($"📡 Listening for ESP8266 emergency bracelets...");
                LogMessage($"🔊 Alarm Type: System.Beep ({beepFrequency}Hz, {beepDuration}ms)");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ ERROR starting server: {ex.Message}");
                MessageBox.Show($"Error starting server:\n{ex.Message}", "Server Error",
                              MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StopServer()
        {
            isRunning = false;

            // Stop all client connections
            lock (connectedClients)
            {
                foreach (var client in connectedClients.ToList())
                {
                    client.Stop();
                }
                connectedClients.Clear();
            }

            if (server != null)
            {
                server.Stop();
            }

            lblStatus.Text = "🔴 Server Status: Stopped";
            lblStatus.ForeColor = Color.Red;
            btnStartServer.Enabled = true;
            btnStopServer.Enabled = false;

            LogMessage("🛑 SERVER STOPPED");
        }

        private void ListenForClients()
        {
            while (isRunning)
            {
                try
                {
                    TcpClient client = server.AcceptTcpClient();
                    string clientIP = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

                    ClientHandler handler = new ClientHandler(client, this);

                    lock (connectedClients)
                    {
                        connectedClients.Add(handler);
                    }

                    Thread clientThread = new Thread(new ThreadStart(handler.HandleClient));
                    clientThread.IsBackground = true;
                    clientThread.Start();

                    this.Invoke((MethodInvoker)delegate
                    {
                        LogMessage($"🔗 New connection from {clientIP}");
                        UpdateDeviceConnectionStatus(clientIP, "CONNECTED");
                    });
                }
                catch (SocketException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    this.Invoke((MethodInvoker)delegate
                    {
                        LogMessage($"❌ Error accepting client: {ex.Message}");
                    });
                }
            }
        }

        // ============================================
        // ALARM FUNCTIONS DENGAN PERBAIKAN WARNA
        // ============================================
        private void BtnTestAlarm_Click(object sender, EventArgs e)
        {
            PlayAlarm("Manual Test", "TEST_DEVICE", "Test Patient", "Test Room");
        }

        private void BtnBeepTest_Click(object sender, EventArgs e)
        {
            try
            {
                Console.Beep(beepFrequency, beepDuration);
                LogMessage($"🔊 Single beep test: {beepFrequency}Hz, {beepDuration}ms");
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Beep test failed: {ex.Message}");
            }
        }

        private void BtnStopAlarm_Click(object sender, EventArgs e)
        {
            StopAlarm();
        }

        public void PlayAlarm(string source, string deviceId, string patientName, string roomNumber)
        {
            if (alarmActive)
            {
                LogMessage("⚠ Alarm already active, ignoring duplicate");
                return;
            }

            this.Invoke((MethodInvoker)delegate
            {
                alarmActive = true;
                panelEmergency.Visible = true;
                lblAlarmStatus.Text = "Alarm: ACTIVE 🚨";
                lblAlarmStatus.ForeColor = Color.Red;

                txtEmergencyInfo.Text = $"Device: {deviceId}\r\nPatient: {patientName}\r\nRoom: {roomNumber}\r\nSource: {source}\r\nTime: {DateTime.Now:HH:mm:ss}\r\nBeep: {beepFrequency}Hz, {beepDuration}ms";

                LogMessage($"🚨 ALARM ACTIVATED by {source}");
                LogMessage($"   Device: {deviceId}, Patient: {patientName}, Room: {roomNumber}");

                // Tampilkan notifikasi popup
                ShowNotificationPopup(deviceId, patientName, roomNumber);

                // Update warna merah pada ListView untuk device yang sesuai
                UpdateEmergencyDeviceColor(deviceId, true);
            });

            alarmThread = new Thread(() => PlayBeepAlarm());
            alarmThread.IsBackground = true;
            alarmThread.Start();
        }

        private void UpdateEmergencyDeviceColor(string deviceId, bool isEmergency)
        {
            foreach (ListViewItem item in listViewPatients.Items)
            {
                var deviceTag = item.Tag as DeviceTag;
                if (deviceTag != null && deviceTag.Device != null)
                {
                    // Cek apakah device ini yang dimaksud
                    bool isTargetDevice = deviceTag.Device.DeviceId == deviceId ||
                                         deviceTag.Device.IpAddress.EndsWith(deviceId.Replace("BRACELET_", ""));

                    if (isTargetDevice)
                    {
                        if (isEmergency)
                        {
                            // WARNA MERAH untuk emergency
                            item.BackColor = Color.Red;
                            item.ForeColor = Color.White;
                            item.Font = new Font(listViewPatients.Font, FontStyle.Bold);
                        }
                        else
                        {
                            // Kembalikan warna berdasarkan IP
                            Color baseColor = GetDeviceColor(deviceTag.IpAddress, deviceTag.MacAddress);

                            if (deviceTag.Device.Status == "CONNECTED")
                            {
                                item.BackColor = LightenColor(baseColor, 0.3f);
                                item.ForeColor = Color.Black;
                            }
                            else if (deviceTag.Device.Status == "READY")
                            {
                                item.BackColor = LightenColor(baseColor, 0.5f);
                                item.ForeColor = Color.Black;
                            }
                            else
                            {
                                item.BackColor = Color.White;
                                item.ForeColor = Color.Black;
                            }
                        }
                        break;
                    }
                }
            }
        }

        private void ShowNotificationPopup(string deviceId, string patientName, string roomNumber)
        {
            // Buat form notifikasi popup
            Form notificationForm = new Form();
            notificationForm.Text = "🚨 EMERGENCY ALERT!";
            notificationForm.Size = new Size(400, 250);
            notificationForm.StartPosition = FormStartPosition.CenterScreen;
            notificationForm.BackColor = Color.Red;
            notificationForm.TopMost = true;
            notificationForm.FormBorderStyle = FormBorderStyle.FixedDialog;
            notificationForm.ControlBox = false;

            Label titleLabel = new Label();
            titleLabel.Text = "EMERGENCY CALL!";
            titleLabel.Font = new Font("Arial", 16, FontStyle.Bold);
            titleLabel.ForeColor = Color.White;
            titleLabel.Size = new Size(380, 40);
            titleLabel.Location = new Point(10, 10);
            titleLabel.TextAlign = ContentAlignment.MiddleCenter;
            notificationForm.Controls.Add(titleLabel);

            Label infoLabel = new Label();
            infoLabel.Text = $"Patient: {patientName}\nRoom: {roomNumber}\nDevice: {deviceId}";
            infoLabel.Font = new Font("Arial", 12);
            infoLabel.ForeColor = Color.White;
            infoLabel.Size = new Size(380, 80);
            infoLabel.Location = new Point(10, 60);
            infoLabel.TextAlign = ContentAlignment.MiddleCenter;
            notificationForm.Controls.Add(infoLabel);

            Button closeButton = new Button();
            closeButton.Text = "ACKNOWLEDGE";
            closeButton.Font = new Font("Arial", 10, FontStyle.Bold);
            closeButton.Size = new Size(150, 40);
            closeButton.Location = new Point(125, 150);
            closeButton.Click += (s, e) => notificationForm.Close();
            notificationForm.Controls.Add(closeButton);

            // Tampilkan popup
            Thread popupThread = new Thread(() => notificationForm.ShowDialog());
            popupThread.SetApartmentState(ApartmentState.STA);
            popupThread.Start();
        }

        private void PlayBeepAlarm()
        {
            try
            {
                LogMessage($"🔊 Starting beep alarm: {beepFrequency}Hz, {beepDuration}ms interval");

                while (alarmActive)
                {
                    try
                    {
                        Console.Beep(beepFrequency, beepDuration);

                        if (!alarmActive) break;
                        Thread.Sleep(beepInterval);

                        if (!alarmActive) break;
                        Console.Beep(beepFrequency + 200, beepDuration);
                        Thread.Sleep(beepInterval);
                    }
                    catch (Exception beepEx)
                    {
                        if (beepEx.Message.Contains("Beep"))
                        {
                            LogMessage("⚠ Console.Beep failed, using audio fallback");
                            System.Media.SystemSounds.Exclamation.Play();
                            Thread.Sleep(1000);
                        }
                        else
                        {
                            LogMessage($"⚠ Beep error: {beepEx.Message}");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Alarm thread error: {ex.Message}");
            }
        }

        private void StopAlarm()
        {
            alarmActive = false;

            this.Invoke((MethodInvoker)delegate
            {
                panelEmergency.Visible = false;
                lblAlarmStatus.Text = "Alarm: IDLE";
                lblAlarmStatus.ForeColor = Color.Green;

                LogMessage("🔇 ALARM STOPPED");

                // Reset semua emergency status dan warna
                foreach (var device in patientDevices.Values)
                {
                    if (device.EmergencyStatus == "ACTIVE")
                    {
                        device.EmergencyStatus = "";
                        if (device.Status == "EMERGENCY")
                        {
                            device.Status = "CONNECTED";
                        }
                    }
                }

                UpdatePatientListView(); // Update warna semua item
            });

            if (alarmThread != null && alarmThread.IsAlive)
            {
                Thread.Sleep(100);
                alarmThread = null;
            }
        }

        // ============================================
        // PATIENT DEVICE MANAGEMENT DENGAN PERBAIKAN WARNA
        // ============================================
        public void RegisterDevice(string deviceId, string macAddress, string ipAddress, string patientName, string roomNumber, string bedNumber)
        {
            this.Invoke((MethodInvoker)delegate
            {
                bool isNewDevice = !patientDevices.ContainsKey(ipAddress);

                if (isNewDevice)
                {
                    patientDevices[ipAddress] = new PatientDevice
                    {
                        DeviceId = deviceId,
                        MacAddress = macAddress,
                        IpAddress = ipAddress,
                        PatientName = patientName,
                        RoomNumber = roomNumber,
                        BedNumber = bedNumber,
                        Status = "CONNECTED",
                        LastActivity = DateTime.Now,
                        EmergencyStatus = ""
                    };

                    // Jika IP belum ada di deviceOrder, tambahkan ke akhir
                    if (!deviceOrder.Contains(ipAddress))
                    {
                        deviceOrder.Add(ipAddress);
                    }

                    LogMessage($"✅ NEW DEVICE REGISTERED: {deviceId}");
                    LogMessage($"   MAC: {macAddress}, IP: {ipAddress}");
                    LogMessage($"   Added at position: {deviceOrder.Count}");

                    // Tambahkan warna untuk IP baru jika belum ada
                    if (!deviceColors.ContainsKey(ipAddress))
                    {
                        // Generate warna unik berdasarkan IP
                        int hash = ipAddress.GetHashCode();
                        Color newColor = Color.FromArgb(
                            150 + Math.Abs(hash % 105),
                            150 + Math.Abs((hash / 100) % 105),
                            150 + Math.Abs((hash / 10000) % 105)
                        );
                        deviceColors[ipAddress] = newColor;
                    }

                    // Update mapping MAC to IP
                    if (!macToIpMapping.ContainsKey(macAddress))
                    {
                        macToIpMapping[macAddress] = ipAddress;
                    }
                }
                else
                {
                    patientDevices[ipAddress].Status = "CONNECTED";
                    patientDevices[ipAddress].LastActivity = DateTime.Now;
                    patientDevices[ipAddress].IpAddress = ipAddress;
                    patientDevices[ipAddress].MacAddress = macAddress;

                    // Update data jika ada dari device
                    if (!string.IsNullOrEmpty(deviceId))
                        patientDevices[ipAddress].DeviceId = deviceId;
                    if (!string.IsNullOrEmpty(patientName))
                        patientDevices[ipAddress].PatientName = patientName;
                    if (!string.IsNullOrEmpty(roomNumber))
                        patientDevices[ipAddress].RoomNumber = roomNumber;
                    if (!string.IsNullOrEmpty(bedNumber))
                        patientDevices[ipAddress].BedNumber = bedNumber;

                    LogMessage($"📱 Device reconnected: {deviceId}");
                }

                UpdatePatientListView();
                SavePatientDatabase();
                UpdateConnectedDevicesCount();
            });
        }

        public void HandleEmergency(string deviceId, string patientName, string roomNumber, string bedNumber)
        {
            LogMessage($"🚨 EMERGENCY SIGNAL RECEIVED!");
            LogMessage($"   Device: {deviceId}");

            // Update device data
            this.Invoke((MethodInvoker)delegate
            {
                // Cari device berdasarkan DeviceId, IP, atau MAC
                PatientDevice device = null;

                foreach (var dev in patientDevices.Values)
                {
                    if (dev.DeviceId == deviceId ||
                        dev.IpAddress == deviceId ||
                        dev.MacAddress == deviceId ||
                        dev.IpAddress.EndsWith(deviceId.Replace("BRACELET_", "")))
                    {
                        device = dev;
                        break;
                    }
                }

                if (device != null)
                {
                    device.LastActivity = DateTime.Now;
                    device.EmergencyStatus = "ACTIVE";
                    device.Status = "EMERGENCY";

                    // Update patient info jika ada
                    if (!string.IsNullOrEmpty(patientName))
                        device.PatientName = patientName;
                    if (!string.IsNullOrEmpty(roomNumber))
                        device.RoomNumber = roomNumber;
                    if (!string.IsNullOrEmpty(bedNumber))
                        device.BedNumber = bedNumber;

                    UpdatePatientListView();
                    SavePatientDatabase();

                    // Update warna merah untuk device ini
                    UpdateEmergencyDeviceColor(deviceId, true);
                }
                else
                {
                    LogMessage($"⚠ Device not found for emergency: {deviceId}");
                }
            });

            // Play alarm dengan info device
            PlayAlarm($"ESP8266 Bracelet", deviceId, patientName, roomNumber);

            // Send acknowledgment
            SendToDevice(deviceId, $"{{\"status\":\"ACK\",\"action\":\"ALARM_STARTED\",\"timestamp\":\"{DateTime.Now:HH:mm:ss}\"}}");
        }

        public void HandleEmergencyStop(string deviceId)
        {
            LogMessage($"✅ Emergency STOP received from device: {deviceId}");

            this.Invoke((MethodInvoker)delegate
            {
                var device = patientDevices.Values.FirstOrDefault(d => d.DeviceId == deviceId);
                if (device != null)
                {
                    device.EmergencyStatus = "";
                    device.Status = "CONNECTED";
                    device.LastActivity = DateTime.Now;

                    // Update warna kembali normal
                    UpdateEmergencyDeviceColor(deviceId, false);

                    UpdatePatientListView();
                    SavePatientDatabase();
                }
            });

            StopAlarm();
            SendToDevice(deviceId, $"{{\"status\":\"ACK\",\"action\":\"ALARM_STOPPED\",\"timestamp\":\"{DateTime.Now:HH:mm:ss}\"}}");
        }

        private void UpdateDeviceConnectionStatus(string ipAddress, string status)
        {
            var device = patientDevices.Values.FirstOrDefault(d => d.IpAddress == ipAddress);
            if (device != null)
            {
                device.Status = status;
                device.LastActivity = DateTime.Now;
                UpdatePatientListView();
            }
        }

        public void UpdateDeviceInfo(string deviceId, string patientName, string roomNumber, string bedNumber, string status, string emergencyStatus)
        {
            this.Invoke((MethodInvoker)delegate
            {
                var device = patientDevices.Values.FirstOrDefault(d => d.DeviceId == deviceId);
                if (device != null)
                {
                    device.PatientName = patientName;
                    device.RoomNumber = roomNumber;
                    device.BedNumber = bedNumber;
                    device.Status = status;
                    device.EmergencyStatus = emergencyStatus;
                    device.LastActivity = DateTime.Now;

                    UpdatePatientListView();
                    SavePatientDatabase();
                }
            });
        }

        private void SendToDevice(string identifier, string message)
        {
            lock (connectedClients)
            {
                // Cari client berdasarkan IP atau DeviceId
                ClientHandler client = null;

                // Cari berdasarkan IP
                if (identifier.Contains(".")) // Jika identifier adalah IP address
                {
                    client = connectedClients.FirstOrDefault(c => c.IpAddress == identifier);
                }
                // Cari berdasarkan DeviceId
                else
                {
                    client = connectedClients.FirstOrDefault(c => c.DeviceId == identifier);

                    // Jika tidak ditemukan dengan DeviceId, cari device di patientDevices dan cari IP-nya
                    if (client == null)
                    {
                        var device = patientDevices.Values.FirstOrDefault(d => d.DeviceId == identifier);
                        if (device != null)
                        {
                            client = connectedClients.FirstOrDefault(c => c.IpAddress == device.IpAddress);
                        }
                    }
                }

                if (client != null && client.IsConnected)
                {
                    client.SendMessage(message);
                    LogMessage($"📤 Sent to {identifier}: {message}");
                }
                else
                {
                    LogMessage($"⚠ Cannot send to {identifier}: Device not connected");
                }
            }
        }

        public void RemoveClient(ClientHandler client)
        {
            lock (connectedClients)
            {
                connectedClients.Remove(client);
            }

            this.Invoke((MethodInvoker)delegate
            {
                var device = patientDevices.Values.FirstOrDefault(d => d.IpAddress == client.IpAddress);
                if (device != null && device.Status != "EMERGENCY")
                {
                    device.Status = "DISCONNECTED";
                    UpdatePatientListView();
                    SavePatientDatabase();
                    UpdateConnectedDevicesCount();

                    LogMessage($"📴 Device disconnected: {device.DeviceId} at IP {client.IpAddress}");
                }
            });
        }

        // ============================================
        // UI UPDATE FUNCTIONS
        // ============================================
        private void UpdateConnectedDevicesCount()
        {
            int connectedCount = patientDevices.Values.Count(d => d.Status == "CONNECTED" || d.Status == "EMERGENCY");
            lblConnectedDevices.Text = $"Connected Devices: {connectedCount}";

            if (connectedCount > 0)
            {
                lblConnectedDevices.ForeColor = Color.Green;
            }
            else
            {
                lblConnectedDevices.ForeColor = Color.Red;
            }
        }

        private void StatusTimer_Tick(object sender, EventArgs e)
        {
            UpdateConnectedDevicesCount();

            // Update last activity setiap 5 detik untuk device yang connected
            foreach (var device in patientDevices.Values.Where(d => d.Status == "CONNECTED" || d.Status == "EMERGENCY"))
            {
                if ((DateTime.Now - device.LastActivity).TotalSeconds > 60)
                {
                    device.Status = "DISCONNECTED";
                    UpdatePatientListView();
                }
            }
        }

        // ============================================
        // UTILITY FUNCTIONS
        // ============================================
        private void LogMessage(string message)
        {
            if (this.InvokeRequired)
            {
                this.Invoke((MethodInvoker)delegate { LogMessage(message); });
                return;
            }

            string timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
            Console.WriteLine($"[{timestamp}] {message}");
        }

        private string GetLocalIPAddress()
        {
            try
            {
                var host = Dns.GetHostEntry(Dns.GetHostName());
                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        if (ip.ToString().StartsWith("192.168.18."))
                        {
                            return ip.ToString();
                        }
                    }
                }

                foreach (var ip in host.AddressList)
                {
                    if (ip.AddressFamily == AddressFamily.InterNetwork)
                    {
                        return ip.ToString();
                    }
                }
            }
            catch (Exception ex)
            {
                LogMessage($"❌ Error getting IP address: {ex.Message}");
            }

            return "127.0.0.1";
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopServer();
            StopAlarm();

            if (alarmThread != null && alarmThread.IsAlive)
            {
                try
                {
                    alarmThread.Abort();
                }
                catch { }
            }

            SavePatientDatabase();

            base.OnFormClosing(e);
        }
    }

    // ============================================
    // SUPPORTING CLASSES
    // ============================================
    public class PreRegisteredDevice
    {
        public string DeviceID { get; set; }
        public string MACAddress { get; set; }
        public string IPAddress { get; set; }
    }

    public class PatientDevice
    {
        public string DeviceId { get; set; }
        public string MacAddress { get; set; }
        public string IpAddress { get; set; }
        public string PatientName { get; set; }
        public string RoomNumber { get; set; }
        public string BedNumber { get; set; }
        public string Status { get; set; }
        public DateTime LastActivity { get; set; }
        public string EmergencyStatus { get; set; }
    }

    public class ClientHandler
    {
        private TcpClient client;
        private NetworkStream stream;
        private StreamReader reader;
        private StreamWriter writer;
        private PatientDataForm form;
        private bool isConnected;

        public string DeviceId { get; private set; }
        public string IpAddress { get; private set; }
        public bool IsConnected => isConnected;

        public ClientHandler(TcpClient client, PatientDataForm form)
        {
            this.client = client;
            this.form = form;
            this.IpAddress = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
            this.stream = client.GetStream();
            this.reader = new StreamReader(stream, Encoding.UTF8);
            this.writer = new StreamWriter(stream, Encoding.UTF8);
            this.writer.AutoFlush = true;
            this.isConnected = true;
        }

        public void HandleClient()
        {
            try
            {
                while (isConnected && client.Connected)
                {
                    if (stream.DataAvailable)
                    {
                        string data = reader.ReadLine();
                        if (data != null)
                        {
                            ProcessMessage(data);
                        }
                    }

                    Thread.Sleep(10);
                }
            }
            catch (IOException)
            {
                Console.WriteLine($"💔 Client {IpAddress} disconnected");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Client error from {IpAddress}: {ex.Message}");
            }
            finally
            {
                Stop();
            }
        }

        private void ProcessMessage(string message)
        {
            try
            {
                message = message.Trim();
                Console.WriteLine($"📥 Raw data from {IpAddress}: {message}");

                // Handle simple text commands
                if (message == "!EMERGENCY!" || message.ToUpper().Contains("EMERGENCY"))
                {
                    Console.WriteLine($"🚨 Simple emergency command from {IpAddress}");
                    DeviceId = "BRACELET_" + IpAddress.Replace(".", "");
                    form.HandleEmergency(DeviceId, "Unknown Patient", "Unknown Room", "");
                    return;
                }
                else if (message == "!STOP!" || message.ToUpper().Contains("STOP"))
                {
                    Console.WriteLine($"✅ Simple stop command from {IpAddress}");
                    form.HandleEmergencyStop(DeviceId ?? ("BRACELET_" + IpAddress.Replace(".", "")));
                    return;
                }

                // Try to parse as JSON
                if (message.StartsWith("{") && message.EndsWith("}"))
                {
                    try
                    {
                        JObject json = JObject.Parse(message);
                        string command = json["command"]?.ToString();

                        if (string.IsNullOrEmpty(command))
                        {
                            SendMessage("{\"error\":\"No command specified\"}");
                            return;
                        }

                        switch (command)
                        {
                            case "REGISTER":
                                DeviceId = json["device_id"]?.ToString();
                                string macAddress = json["mac_address"]?.ToString();
                                string ipAddress = json["ip_address"]?.ToString() ?? IpAddress;
                                string patientName = json["patient"]?.ToString() ?? "";
                                string roomNumber = json["room"]?.ToString() ?? "";
                                string bedNumber = json["bed"]?.ToString() ?? "";
                                string status = json["status"]?.ToString() ?? "CONNECTED";

                                if (string.IsNullOrEmpty(DeviceId))
                                {
                                    DeviceId = "BRACELET_" + IpAddress.Replace(".", "");
                                }

                                form.RegisterDevice(DeviceId, macAddress ?? "Unknown",
                                                  ipAddress, patientName, roomNumber, bedNumber);

                                SendMessage($"{{\"status\":\"REGISTERED\",\"message\":\"Device registered successfully\",\"server_time\":\"{DateTime.Now:HH:mm:ss}\"}}");
                                break;

                            case "EMERGENCY_START":
                                DeviceId = json["device_id"]?.ToString() ?? DeviceId;
                                string emergencyPatient = json["patient"]?.ToString() ?? "";
                                string emergencyRoom = json["room"]?.ToString() ?? "";
                                string emergencyBed = json["bed"]?.ToString() ?? "";

                                if (string.IsNullOrEmpty(DeviceId))
                                {
                                    DeviceId = "BRACELET_" + IpAddress.Replace(".", "");
                                }

                                form.HandleEmergency(DeviceId, emergencyPatient, emergencyRoom, emergencyBed);
                                break;

                            case "EMERGENCY_STOP":
                                DeviceId = json["device_id"]?.ToString() ?? DeviceId;

                                if (string.IsNullOrEmpty(DeviceId))
                                {
                                    DeviceId = "BRACELET_" + IpAddress.Replace(".", "");
                                }

                                form.HandleEmergencyStop(DeviceId);
                                break;

                            case "DEVICE_INFO":
                                DeviceId = json["device_id"]?.ToString() ?? DeviceId;
                                string infoPatient = json["patient"]?.ToString() ?? "";
                                string infoRoom = json["room"]?.ToString() ?? "";
                                string infoBed = json["bed"]?.ToString() ?? "";
                                string infoStatus = json["status"]?.ToString() ?? "CONNECTED";
                                string emergencyStatus = json["emergency"]?.ToString() ?? "";

                                if (string.IsNullOrEmpty(DeviceId))
                                {
                                    DeviceId = "BRACELET_" + IpAddress.Replace(".", "");
                                }

                                form.UpdateDeviceInfo(DeviceId, infoPatient, infoRoom, infoBed, infoStatus, emergencyStatus);
                                SendMessage($"{{\"status\":\"ACK\",\"message\":\"Device info updated\"}}");
                                break;

                            default:
                                SendMessage("{\"error\":\"Unknown command\",\"received_command\":\"" + command + "\"}");
                                break;
                        }
                    }
                    catch (JsonException jsonEx)
                    {
                        Console.WriteLine($"❌ JSON parse error from {IpAddress}: {jsonEx.Message}");
                        SendMessage("{\"error\":\"Invalid JSON format\"}");
                    }
                }
                else
                {
                    // Not JSON, just log it
                    Console.WriteLine($"📝 Text message from {IpAddress}: {message}");
                    SendMessage("{\"status\":\"OK\",\"message\":\"Message received\"}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error processing message from {IpAddress}: {ex.Message}");
            }
        }

        public void SendMessage(string message)
        {
            try
            {
                writer.WriteLine(message);
                writer.Flush();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error sending message to {DeviceId}: {ex.Message}");
                Stop();
            }
        }

        public void Stop()
        {
            isConnected = false;

            try
            {
                reader?.Close();
                writer?.Close();
                stream?.Close();
                client?.Close();
            }
            catch { }

            form.RemoveClient(this);
        }
    }
}