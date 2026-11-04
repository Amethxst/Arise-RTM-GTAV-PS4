using System;
using System.Windows.Forms;

namespace Arise_RTM
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            bool preview = Array.Exists(args, arg => string.Equals(arg, "--preview", StringComparison.OrdinalIgnoreCase));
            Application.Run(new MainForm(preview));
        }
    }
}
