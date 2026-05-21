using System;
using System.Windows.Forms;

namespace StarTradForLauncher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && String.Equals(args[0], "--launcher-json", StringComparison.OrdinalIgnoreCase))
            {
                return LauncherCommand.Run(args);
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
            return 0;
        }
    }
}
