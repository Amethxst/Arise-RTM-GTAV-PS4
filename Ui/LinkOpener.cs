using System;
using System.Diagnostics;

namespace Arise_RTM
{
    /// <summary>Opens a fixed web address or a local file with the default program.</summary>
    internal static class LinkOpener
    {
        public static void Open(string target, Action<string> log)
        {
            try
            {
                Process.Start(new ProcessStartInfo(target) { UseShellExecute = true });
            }
            catch (Exception ex)
            {
                log?.Invoke("Could not open " + target + ": " + ex.Message);
            }
        }
    }
}
