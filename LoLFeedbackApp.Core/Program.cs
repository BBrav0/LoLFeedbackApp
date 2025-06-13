using System;
using System.IO;
using System.Windows.Forms;

namespace LoLFeedbackApp.Core
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            try
            {
                LoadEnvironmentVariables();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Application failed to start: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private static void LoadEnvironmentVariables()
        {
            try
            {
                var envPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ".env");
                if (File.Exists(envPath))
                {
                    foreach (var line in File.ReadAllLines(envPath))
                    {
                        var parts = line.Split('=', 2);
                        if (parts.Length == 2)
                        {
                            Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                        }
                    }
                }
                else
                {
                    throw new FileNotFoundException(".env file not found. Please create a .env file with your RIOT_API_KEY.");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Error loading .env file: {ex.Message}", ex);
            }
        }
    }
}