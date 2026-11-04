using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Debug tab for advanced users: write or read a stat by name, set a packed value by index, and run a list of writes
    /// from a text file on your PC. Nothing is bundled: the tool only writes what you type or load.
    /// </summary>
    public sealed class DebugPanel : Panel
    {
        private const int ColumnWidth = 470;
        private const int RightColumnX = 500;
        private const int WideWidth = 956;

        private static readonly string[] PrefixChoices = { "Character stat", "Account stat", "Exact name" };
        private static readonly Regex AlreadyPrefixed = new Regex(@"^(MP\d_|MPPLY_)", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        private const string Intro =
            "Advanced tools. Write or read one stat, set a packed value, or run a list of writes. Everything goes straight to the game, so check what you type.";

        private const string CharacterHint =
            "Your two characters are called MP0_ (Character 1) and MP1_ (Character 2) in the game. " +
            "This is the same choice as \"Stats character\" in the toolbar.";

        private const string PrefixHint =
            "Character stat: the tool adds MP0_ or MP1_ for you. Account stat: it adds MPPLY_. Exact name: it writes the name as you typed it.";

        private const string TypeHint = "Pick the type that matches the stat: int = whole number, bool = true / false, float = decimal number.";

        private const string PackedHint =
            "A packed value is a flag (or a small number) stored by its number, the index, instead of by a name. It belongs to the chosen character.";

        private const string ListFormat =
            "int    MP_CHAR_ABILITY_1_UNLCK   -1       stat, 32-bit integer\r\n" +
            "bool   MP_SOME_STAT              true     true / false (or 1 / 0)\r\n" +
            "float  MPPLY_SOME_STAT           6.66     decimal number\r\n" +
            "pbool  1234                      true     packed flag by index\r\n" +
            "pint   1234                      5        packed integer by index";

        private const string ListNotes =
            "One write per line, # starts a comment. MP_ at the start of a name means the character chosen above; any other name is used as typed. " +
            "See stats_example.txt. The toolbar options (Save after write, Verify read-back, Ask before applying) apply to lists too, " +
            "and Cancel stops a long list: what is already written stays written.";

        private const string ListWarning =
            "The tool writes exactly what the file says, with no check on the values. A wrong list can damage your character or your account: " +
            "use lists from sources you trust, and change session afterwards.";

        private readonly IToolHost host;
        private readonly List<TileButton> actionButtons = new List<TileButton>();
        private readonly ChoiceBar characterBar;
        private readonly ChoiceBar setPrefix, getPrefix, packedValue;
        private readonly TextBox setName, setValue, getName, result;
        private readonly Label setFullName, getFullName;
        private readonly NumberBox packedIndex;
        private bool syncing;

        public DebugPanel(IToolHost host)
        {
            this.host = host;
            Text = "Debug";

            Controls.Add(new Label { Text = Intro, Location = new Point(14, 8), Size = new Size(WideWidth, 16), ForeColor = UiStyle.Muted });

            // ---- character
            Section character = new Section { Text = "Character", Location = new Point(14, 30), Size = new Size(WideWidth, 60) };
            characterBar = new ChoiceBar(new[] { "Character 1  (MP0_)", "Character 2  (MP1_)" }, 190, 26, -1) { Location = new Point(14, 26) };
            characterBar.SelectedChanged += (s, e) =>
            {
                if (!syncing && characterBar.SelectedIndex >= 0)
                    host.ChooseCharacter(characterBar.SelectedIndex);
            };
            character.Controls.Add(characterBar);
            character.Controls.Add(new Label { Text = CharacterHint, Location = new Point(404, 26), Size = new Size(540, 32), ForeColor = UiStyle.Muted });
            Controls.Add(character);

            // ---- set stat
            Section set = new Section { Text = "Set a stat", Location = new Point(14, 98), Size = new Size(ColumnWidth, 208) };
            setPrefix = new ChoiceBar(PrefixChoices, 148, 24, 0) { Location = new Point(14, 26) };
            set.Controls.Add(setPrefix);
            setName = TextField(set, "Name", 14, 58, 440, "e.g. CHAR_ABILITY_1_UNLCK");
            setValue = TextField(set, "Value", 14, 86, 440, "e.g. -1, true or 6.66");
            setFullName = FullNameLabel(set, 14, 112);
            AddButtons(set, 14, 134, ("Set int", () => SetStat("int")), ("Set bool", () => SetStat("bool")), ("Set float", () => SetStat("float")));
            set.Controls.Add(new Label { Text = TypeHint, Location = new Point(14, 166), Size = new Size(440, 32), ForeColor = UiStyle.Muted });
            Controls.Add(set);

            // ---- get stat
            Section get = new Section { Text = "Read a stat", Location = new Point(RightColumnX, 98), Size = new Size(ColumnWidth, 208) };
            getPrefix = new ChoiceBar(PrefixChoices, 148, 24, 0) { Location = new Point(14, 26) };
            get.Controls.Add(getPrefix);
            getName = TextField(get, "Name", 14, 58, 440, "e.g. CHAR_ABILITY_1_UNLCK");
            getFullName = FullNameLabel(get, 14, 82);
            AddButtons(get, 14, 106, ("Get int", () => GetStat("int")), ("Get bool", () => GetStat("bool")), ("Get float", () => GetStat("float")));
            get.Controls.Add(new Label { Text = "Result", Location = new Point(14, 146), Size = new Size(44, 16), ForeColor = Theme.Muted });
            result = new TextBox { Location = new Point(60, 142), Size = new Size(394, 23), ReadOnly = true };
            get.Controls.Add(result);
            get.Controls.Add(new Label { Text = PrefixHint, Location = new Point(14, 172), Size = new Size(440, 32), ForeColor = UiStyle.Muted });
            Controls.Add(get);

            // ---- packed
            Section packed = new Section { Text = "Packed value", Location = new Point(14, 314), Size = new Size(WideWidth, 88) };
            packed.Controls.Add(new Label { Text = PackedHint, Location = new Point(14, 26), Size = new Size(920, 16), ForeColor = UiStyle.Muted });
            packed.Controls.Add(new Label { Text = "Index", AutoSize = true, Location = new Point(14, 54) });
            packedIndex = new NumberBox { Minimum = 0, Maximum = 200000, Location = new Point(60, 50), Width = 90 };
            packed.Controls.Add(packedIndex);
            packedValue = new ChoiceBar(new[] { "true", "false" }, 70, 24, 0) { Location = new Point(166, 48) };
            packed.Controls.Add(packedValue);
            AddButtons(packed, 326, 48, ("Set", () => SetPacked()), ("Get", () => GetPacked()));
            Controls.Add(packed);

            // ---- list
            Section list = new Section { Text = "Run a list from a file", Location = new Point(14, 410), Size = new Size(WideWidth, 248) };
            int y = 28;
            y = AddFlowLabel(list, "Load a list from your own PC (a .txt file). The format, one write per line:", UiStyle.Muted, null, y, 14) - 2;
            y = AddFlowLabel(list, ListFormat, Theme.Text, Theme.Mono, y, 14);
            y = AddFlowLabel(list, ListNotes, UiStyle.Muted, null, y, 14);

            TileButton check = new TileButton { Text = "Check a list (writes nothing)", Outline = true, Centered = true };
            check.SetBounds(14, y, 260, 26);
            check.Click += (s, e) => CheckList();
            list.Controls.Add(check);

            TileButton run = new TileButton { Text = "Run a list...", Primary = true, Centered = true };
            run.SetBounds(284, y, 200, 26);
            run.Click += async (s, e) => await RunList();
            actionButtons.Add(run);
            list.Controls.Add(run);
            y += 32;

            AddFlowLabel(list, ListWarning, Theme.Yellow, null, y, 14);
            Controls.Add(list);

            setPrefix.SelectedChanged += (s, e) => RefreshFullNames();
            getPrefix.SelectedChanged += (s, e) => RefreshFullNames();
            setName.TextChanged += (s, e) => RefreshFullNames();
            getName.TextChanged += (s, e) => RefreshFullNames();
            SetCharacter(host.Slot);
        }

        /// <summary>Follows the character chosen in the toolbar: shows it here and enables the buttons that write or read.</summary>
        public void SetCharacter(int slot)
        {
            syncing = true;
            try
            {
                characterBar.SelectedIndex = slot;
            }
            finally
            {
                syncing = false;
            }

            foreach (TileButton button in actionButtons)
                button.Enabled = slot >= 0;

            RefreshFullNames();
        }

        // ---- layout helpers -------------------------------------------------------------

        private static TextBox TextField(Control parent, string label, int x, int y, int width, string cue)
        {
            parent.Controls.Add(new Label { Text = label, AutoSize = true, Location = new Point(x, y + 4) });
            TextBox box = new TextBox { Location = new Point(x + 50, y), Width = width - 50 };
            Theme.Cue(box, cue);
            parent.Controls.Add(box);
            return box;
        }

        private static Label FullNameLabel(Control parent, int x, int y)
        {
            Label label = new Label { Location = new Point(x, y), Size = new Size(440, 18), ForeColor = Theme.Yellow };
            parent.Controls.Add(label);
            return label;
        }

        private void AddButtons(Control parent, int x, int y, params (string Text, Func<Task> Action)[] buttons)
        {
            foreach ((string text, Func<Task> action) in buttons)
            {
                TileButton button = new TileButton { Text = text, Outline = true, Centered = true };
                button.SetBounds(x, y, 100, 26);
                button.Click += async (s, e) => await action();
                actionButtons.Add(button);
                parent.Controls.Add(button);
                x += 108;
            }
        }

        /// <summary>Adds a label as tall as its text needs and returns the y below it.</summary>
        private static int AddFlowLabel(Control parent, string text, Color color, Font font, int y, int x)
        {
            int width = parent.Width - 2 * x;
            Label label = new Label { Text = text, Location = new Point(x, y), ForeColor = color, UseMnemonic = false, Width = width };
            label.Font = font ?? Theme.Body;
            label.Height = label.GetPreferredSize(new Size(width, 0)).Height + 2;
            parent.Controls.Add(label);
            return y + label.Height + 8;
        }

        // ---- names ----------------------------------------------------------------------

        private string FullName(ChoiceBar prefix, string typed)
        {
            string name = typed.Trim();
            if (name.Length == 0)
                return string.Empty;

            if (prefix.SelectedIndex == 1)
                return AlreadyPrefixed.IsMatch(name) ? name : "MPPLY_" + name;

            if (prefix.SelectedIndex == 0 && !AlreadyPrefixed.IsMatch(name))
                return host.Slot >= 0 ? $"MP{host.Slot}_{name}" : null;

            return name;
        }

        private void RefreshFullNames()
        {
            UpdateFullName(setFullName, setPrefix, setName);
            UpdateFullName(getFullName, getPrefix, getName);
        }

        private void UpdateFullName(Label label, ChoiceBar prefix, TextBox box)
        {
            string full = FullName(prefix, box.Text);
            label.Text = full == null ? "Choose Character 1 or 2 above to see the full name."
                : full.Length == 0 ? string.Empty
                : "Will use: " + full;
        }

        // ---- single stat ----------------------------------------------------------------

        private Task SetStat(string type)
        {
            if (setName.Text.Trim().Length == 0)
            {
                MessageBox.Show("Type the stat name first, for example CHAR_ABILITY_1_UNLCK.", "Set a stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Task.CompletedTask;
            }

            string name = FullName(setPrefix, setName.Text);
            string raw = setValue.Text.Trim().Replace(',', '.');
            Func<OptionContext, OptionResult> work;
            string required;

            switch (type)
            {
                case "int":
                    if (!long.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out long intValue) || intValue < int.MinValue || intValue > uint.MaxValue)
                        return Invalid("a whole number (32 bit), for example -1 or 100");
                    int value32 = unchecked((int)intValue);
                    required = HandlerKeys.StatSetInt;
                    work = ctx => Single(ctx, r => ctx.WriteInt(r, name, value32));
                    break;
                case "bool":
                    bool flag;
                    if (raw.Equals("true", StringComparison.OrdinalIgnoreCase) || raw == "1") flag = true;
                    else if (raw.Equals("false", StringComparison.OrdinalIgnoreCase) || raw == "0") flag = false;
                    else return Invalid("true or false (or 1 / 0)");
                    required = HandlerKeys.StatSetBool;
                    work = ctx => Single(ctx, r => ctx.WriteBool(r, name, flag));
                    break;
                default:
                    if (!float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out float number) || float.IsNaN(number) || float.IsInfinity(number))
                        return Invalid("a decimal number, for example 6.66");
                    required = HandlerKeys.StatSetFloat;
                    work = ctx => Single(ctx, r => ctx.WriteFloat(r, name, number));
                    break;
            }

            string details = FlowStats.IsFlow(name) ? "[!] " + FlowStats.Explanation : null;
            return host.RunAsync($"Set {type} {name}", new[] { required }, new[] { HandlerKeys.StatGetInt, HandlerKeys.StatSave }, work, details);
        }

        private Task GetStat(string type)
        {
            if (getName.Text.Trim().Length == 0)
            {
                MessageBox.Show("Type the stat name first, for example CHAR_ABILITY_1_UNLCK.", "Read a stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return Task.CompletedTask;
            }

            string name = FullName(getPrefix, getName.Text);
            string handler = type == "int" ? HandlerKeys.StatGetInt : type == "bool" ? HandlerKeys.StatGetBool : HandlerKeys.StatGetFloat;

            return host.RunAsync($"Get {type} {name}", new[] { handler }, new string[0], ctx =>
            {
                string text;
                bool ok;
                switch (type)
                {
                    case "int":
                        ok = ctx.Engine.TryGetInt(name, out int i);
                        text = ok ? i.ToString(CultureInfo.InvariantCulture) : "unreadable";
                        break;
                    case "bool":
                        ok = ctx.Engine.TryGetBool(name, out bool b);
                        text = ok ? b.ToString() : "unreadable";
                        break;
                    default:
                        ok = ctx.Engine.TryGetFloat(name, out float f);
                        text = ok ? f.ToString("R", CultureInfo.InvariantCulture) : "unreadable";
                        break;
                }

                ctx.Log($"{name} ({type}) = {text}");
                BeginInvoke(new Action(() => result.Text = $"{name} = {text}"));
                return new OptionResult { Written = ok ? 1 : 0 };
            }, null, changesStats: false);
        }

        private Task SetPacked()
        {
            int index = (int)packedIndex.Value;
            bool value = packedValue.SelectedIndex == 0;
            return host.RunAsync($"Set packed[{index}] = {value}", new[] { HandlerKeys.PackedSetBool },
                new[] { HandlerKeys.PackedGetBool, HandlerKeys.StatSave },
                ctx => Single(ctx, r => ctx.WritePackedBool(r, index, value)));
        }

        private Task GetPacked()
        {
            int index = (int)packedIndex.Value;
            return host.RunAsync($"Get packed[{index}]", new[] { HandlerKeys.PackedGetBool }, new string[0], ctx =>
            {
                ctx.Engine.TryGetPackedBool(index, ctx.Slot, out bool value);
                ctx.Log($"packed[{index}] slot {ctx.Slot} = {value}");
                BeginInvoke(new Action(() => result.Text = $"packed[{index}] = {value}"));
                return new OptionResult { Written = 1 };
            }, null, changesStats: false);
        }

        private static OptionResult Single(OptionContext ctx, Action<OptionResult> write)
        {
            OptionResult r = new OptionResult();
            write(r);
            ctx.SaveIfRequested(r);
            return r;
        }

        private static Task Invalid(string expected)
        {
            MessageBox.Show($"The value must be {expected}.", "Set a stat", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return Task.CompletedTask;
        }

        // ---- lists ----------------------------------------------------------------------

        private bool TryLoadList(string title, out string fileName, out StatListParseResult parsed)
        {
            fileName = null;
            parsed = null;

            string text;
            using (OpenFileDialog dialog = new OpenFileDialog { Title = title, Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return false;

                try
                {
                    FileInfo info = new FileInfo(dialog.FileName);
                    if (info.Length > 4 * 1024 * 1024)
                    {
                        MessageBox.Show("The file is too large for a list (4 MB at most).", title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        return false;
                    }

                    text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                    fileName = Path.GetFileName(dialog.FileName);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Cannot read the file: " + ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return false;
                }
            }

            parsed = StatList.Parse(text);
            return true;
        }

        private static string ErrorSummary(StatListParseResult parsed)
        {
            const int shown = 12;
            string text = string.Join("\n", parsed.Errors.Take(shown));
            int more = parsed.Errors.Count - shown;
            return more > 0 ? text + $"\n... and {more:N0} more." : text;
        }

        private void CheckList()
        {
            if (!TryLoadList("Check a list", out string fileName, out StatListParseResult parsed))
                return;

            if (parsed.Errors.Count > 0)
            {
                MessageBox.Show($"{fileName}\n\n{parsed.Errors.Count:N0} problem(s) found:\n\n{ErrorSummary(parsed)}\n\nNothing was written.",
                    "Check a list", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (parsed.Entries.Count == 0)
            {
                MessageBox.Show($"{fileName}\n\nThe file has no writes.", "Check a list", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            MessageBox.Show($"{fileName}\n\nThe list is valid: {StatList.Describe(parsed.Entries)}\n\nNothing was written.",
                "Check a list", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async Task RunList()
        {
            if (!TryLoadList("Run a list", out string fileName, out StatListParseResult parsed))
                return;

            if (parsed.Errors.Count > 0)
            {
                MessageBox.Show($"{fileName}\n\n{parsed.Errors.Count:N0} problem(s) found:\n\n{ErrorSummary(parsed)}\n\nNothing was written.",
                    "Run a list", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (parsed.Entries.Count == 0)
            {
                MessageBox.Show($"{fileName}\n\nThe file has no writes.", "Run a list", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<StatEntry> entries = parsed.Entries;
            string details = $"{fileName}: {StatList.Describe(entries)}\n\n{ListWarning}";
            await host.RunAsync($"List {fileName} ({entries.Count:N0} writes)", StatList.RequiredHandlers(entries), StatList.OptionalHandlers,
                ctx => StatList.Run(ctx, entries), details);
        }
    }
}
