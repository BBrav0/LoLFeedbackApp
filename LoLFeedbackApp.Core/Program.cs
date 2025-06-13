using System;
using System.Windows.Forms;
using System.Drawing;

namespace LoLFeedbackApp.Core
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }

    public class MainForm : Form
    {
        public MainForm()
        {
            this.Text = "LoL Feedback App";
            this.Size = new System.Drawing.Size(800, 600);
        }
    }
}
