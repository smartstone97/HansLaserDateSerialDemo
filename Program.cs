using System;
using System.Threading;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal static class Program
    {
        [STAThread]
        private static int Main()
        {
            bool firstInstance;
            using (Mutex mutex = new Mutex(true, "HansLaserDateSerialDemo.SingleInstance", out firstInstance))
            {
                if (!firstInstance)
                {
                    MessageBox.Show(Resources.already_running_message, Resources.app_demo_title, MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return 2;
                }

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                return 0;
            }
        }
    }
}
