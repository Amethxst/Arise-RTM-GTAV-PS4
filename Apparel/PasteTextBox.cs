using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>TextBox that keeps line breaks when pasting text that uses bare LF or CR.</summary>
    public class PasteTextBox : TextBox
    {
        private const int WM_PASTE = 0x0302;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_PASTE)
            {
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        string text = Clipboard.GetText()
                            .Replace("\r\n", "\n")
                            .Replace('\r', '\n')
                            .Replace("\n", Environment.NewLine);
                        SelectedText = text;
                        return;
                    }
                }
                catch (ExternalException)
                {
                }
            }

            base.WndProc(ref m);
        }
    }
}
