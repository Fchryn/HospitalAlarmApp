using System;
using System.Threading;
using System.Windows.Forms;

namespace HospitalEmergencySystem
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            // Check if Console.Beep is available
            try
            {
                Console.Beep(1000, 100);
                Thread.Sleep(100);
            }
            catch
            {
                MessageBox.Show("Warning: Console.Beep may not be available on this system.\n" +
                              "The system will use alternative sound methods.",
                              "Audio System Warning",
                              MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }

            Application.Run(new PatientDataForm());
        }
    }
}