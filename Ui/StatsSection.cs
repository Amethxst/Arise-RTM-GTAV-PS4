using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Fast run unlock and custom K/D (ratio, kills, deaths) for the character chosen in the toolbar.
    /// The buttons stay disabled until a character is chosen.
    /// </summary>
    internal sealed class StatsSection : Section
    {
        private const int TileHeight = 24;
        private const int BoxWidth = 64;

        private static readonly string[] KdRequired = { HandlerKeys.StatSetInt, HandlerKeys.StatSetFloat };
        private static readonly string[] KdOptional = { HandlerKeys.StatGetInt, HandlerKeys.StatSave };

        private readonly IToolHost host;
        private readonly List<TileButton> actionButtons = new List<TileButton>();

        public StatsSection(IToolHost host)
        {
            this.host = host;
            Text = "Stats";
            BuildUi();
            SetCharacterChosen(false);
        }

        /// <summary>Enables the buttons only while Character 1 or 2 is chosen in the toolbar.</summary>
        public void SetCharacterChosen(bool chosen)
        {
            foreach (TileButton button in actionButtons)
                button.Enabled = chosen;
        }

        private void BuildUi()
        {
            TileButton unlock = new TileButton { Text = "Unlock fast run", Primary = true, Centered = true };
            unlock.SetBounds(10, 30, 254, TileHeight);
            unlock.Click += async (s, e) => await host.RunOptionAsync(new FastRunOption());
            actionButtons.Add(unlock);
            Controls.Add(unlock);

            TextBox ratio = new TextBox { Text = "6.66", TextAlign = HorizontalAlignment.Right };
            AddRow(ratio, "K/D", "Set K/D", 72, async () =>
            {
                if (!TryParseDouble(ratio.Text, out double value))
                {
                    MessageBox.Show("Enter a number for the K/D ratio (e.g. 6.66).", "K/D", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                await RunKd($"K/D {value.ToString(CultureInfo.InvariantCulture)}", ctx => KdOption.ApplyRatio(ctx, value));
            });

            TextBox kills = new TextBox { TextAlign = HorizontalAlignment.Right };
            AddRow(kills, "Kills", "Set kills", 106, () =>
                ApplyNumber(kills, "Kills", value => RunKd($"Kills {value}", ctx => KdOption.ApplyKills(ctx, value))));

            TextBox deaths = new TextBox { TextAlign = HorizontalAlignment.Right };
            AddRow(deaths, "Deaths", "Set deaths", 140, () =>
                ApplyNumber(deaths, "Deaths", value => RunKd($"Deaths {value}", ctx => KdOption.ApplyDeaths(ctx, value))));

            Controls.Add(new Label
            {
                Text = "Choose Character 1 or 2 in the toolbar to enable the buttons. Change session to see the new values.",
                Location = new Point(10, 184), Size = new Size(254, 54), ForeColor = UiStyle.Muted
            });
        }

        private void AddRow(TextBox box, string label, string buttonText, int y, Func<Task> click)
        {
            const int buttonLeft = 56 + BoxWidth + 6;
            Controls.Add(new Label { Text = label, Location = new Point(10, y + 5), Size = new Size(44, 16), ForeColor = Theme.Muted });
            box.SetBounds(56, y, BoxWidth, TileHeight);
            box.BorderStyle = BorderStyle.FixedSingle;
            Controls.Add(box);

            TileButton button = new TileButton { Text = buttonText, Outline = true, Centered = true };
            button.SetBounds(buttonLeft, y, 264 - buttonLeft, TileHeight);
            button.Click += async (s, e) => await click();
            actionButtons.Add(button);
            Controls.Add(button);
        }

        private async Task ApplyNumber(TextBox box, string label, Func<long, Task> apply)
        {
            if (!long.TryParse(box.Text.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out long value))
            {
                MessageBox.Show($"Enter a whole number for {label.ToLowerInvariant()}.", label, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            await apply(value);
        }

        private Task RunKd(string title, Func<OptionContext, OptionResult> work)
        {
            return host.RunAsync(title, KdRequired, KdOptional, work);
        }

        private static bool TryParseDouble(string text, out double value)
        {
            return double.TryParse(text.Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
                   !double.IsNaN(value) && !double.IsInfinity(value);
        }
    }
}
