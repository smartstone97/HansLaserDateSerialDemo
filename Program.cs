using System;
using System.Diagnostics;
using System.Threading;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal static class Program
    {
        private static bool _restartRequested;

        public static void RequestRestart()
        {
            _restartRequested = true;
            Application.Exit();
        }

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

                LanguageManager.ApplySavedLanguage();
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new MainForm());
                if (_restartRequested)
                    RestartAfterMutexReleased(mutex);
                return 0;
            }
        }

        private static void RestartAfterMutexReleased(Mutex mutex)
        {
            mutex.ReleaseMutex();
            mutex.Dispose();
            Process.Start(Application.ExecutablePath);
        }
    }
}
