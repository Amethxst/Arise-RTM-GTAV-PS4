using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>The one window: outfits editor (names, components, import from text or file; memory reads and writes only) and the stats section.</summary>
    public sealed class OutfitsPanel : Panel
    {
        private const int MaxOutfitNameBytes = 31;
        private const int OutfitSlotCount = 20;
        private const int Margin = 8;
        private const int TeleportHeight = 78;
        private const int RightColumnX = 706;
        private const int RightColumnWidth = 274;

        /// <summary>Height of the whole panel; the columns fill it.</summary>
        public const int DesiredHeight = 666;
        private const string NameCreatorUrl = "https://amethxst.dev/gta-name";

        private static readonly string[] ClothesParts = { "Mask", "Hair", "Gloves", "Legs", "Bag", "Shoes", "Misc", "Undershirt", "Armor", "Decals", "Top" };
        private static readonly string[] PropParts = { "Hat", "Glasses", "Earring", "Watches", "Bracelets" };

        private const string ImportFormatHelp =
            "Import on your PC: paste the text, or open a .txt file (see outfit_example.txt).\r\n\r\n" +
            "One line per component, draw/texture:";

        private const string ImportFormatExample =
            "name = My outfit\r\n" +
            "top = 7/0\r\n" +
            "undershirt = 15/0\r\n" +
            "legs = 10/0\r\n" +
            "feet = 1/0\r\n" +
            "accessories = 0/0\r\n" +
            "bags = 0/0\r\n" +
            "mask = 0/0\r\n" +
            "gloves = 0/0\r\n" +
            "decals = 0/0\r\n" +
            "armor = 0/0\r\n" +
            "hair = 2/0\r\n" +
            "hats = 0/0\r\n" +
            "glasses = 0/0\r\n" +
            "ears = 0/0\r\n" +
            "watches = 0/0\r\n" +
            "bracelets = 0/0";

        private const string ImportFormatNotes =
            "Missing lines keep the slot's current value; no name line keeps the name. " +
            "Outfits are written on the character loaded in the game.";

        private readonly IToolHost host;
        private readonly GTAOutfits outfits;
        private readonly Dictionary<string, TextBox> idBoxes = new Dictionary<string, TextBox>();
        private readonly Dictionary<string, TextBox> texBoxes = new Dictionary<string, TextBox>();

        private SlotList slotList;
        private TextBox nameBox;
        private Label nameBytesLabel;
        private Button saveNameButton, saveComponentsButton, applyImportButton, importFileButton;
        private StatsSection statsSection;
        private PasteTextBox importBox;
        private bool formattingImport;
        private bool attached;

        public OutfitsPanel(IToolHost host)
        {
            this.host = host;
            outfits = new GTAOutfits(host.Connection);
            Text = "Main";
            BuildUi();
        }

        /// <summary>Called when the game is ready: points the editor at the process and loads the slots.</summary>
        public void Attach(int pid)
        {
            outfits.SetProcess(pid);
            attached = true;
            LoadOutfits(-1);
        }

        private Task TeleportToShop()
        {
            return host.RunAsync("Teleport to clothing shop", ClothingShopTeleportOption.RequiredHandlerKeys,
                ClothingShopTeleportOption.OptionalHandlerKeys, ClothingShopTeleportOption.Apply, null, changesStats: false, needsCharacter: false);
        }

        /// <summary>Enables the stats buttons only while Character 1 or 2 is chosen in the toolbar.</summary>
        public void SetCharacterChosen(bool chosen)
        {
            statsSection.SetCharacterChosen(chosen);
        }

        public void Detach()
        {
            attached = false;
            outfits.SetProcess(-1);
            slotList.Clear();
            nameBox.Text = string.Empty;
            foreach (TextBox box in idBoxes.Values.Concat(texBoxes.Values))
                box.Text = string.Empty;
        }

        private void BuildUi()
        {
            Section nameGroup = new Section { Text = "Name", Location = new Point(10, 8), Size = new Size(340, 140) };
            nameBox = new TextBox { Location = new Point(12, 22), Width = 316 };
            nameBox.TextChanged += (s, e) => OnNameEdited();
            nameGroup.Controls.Add(nameBox);
            saveNameButton = new Button { Text = "Save name", Location = new Point(12, 50), Size = new Size(84, 24) };
            saveNameButton.Click += (s, e) => SaveName();
            nameGroup.Controls.Add(saveNameButton);
            nameBytesLabel = new Label { Text = "0/31", AutoSize = true, Location = new Point(104, 54), ForeColor = UiStyle.Muted };
            nameGroup.Controls.Add(nameBytesLabel);
            TileButton createName = new TileButton { Text = "Create your name here", Outline = true, Centered = true, Location = new Point(12, 84), Size = new Size(316, 24) };
            createName.Click += (s, e) => LinkOpener.Open(NameCreatorUrl, host.Log);
            nameGroup.Controls.Add(createName);
            nameGroup.Controls.Add(new Label
            {
                Text = "Opens the name creator in your browser.",
                Location = new Point(12, 114), Size = new Size(316, 16), ForeColor = UiStyle.Muted
            });
            Controls.Add(nameGroup);

            Section slotGroup = new Section { Text = "Outfit slots", Location = new Point(10, 156), Size = new Size(340, DesiredHeight - 156 - Margin) };
            slotList = new SlotList { Location = new Point(8, 22), Size = new Size(324, OutfitSlotCount * SlotList.RowHeight + 2), SlotCount = OutfitSlotCount };
            slotList.SelectedChanged += (s, e) => OnSlotChanged();
            slotGroup.Controls.Add(slotList);
            TileButton refreshButton = new TileButton { Text = "Refresh", Centered = true };
            refreshButton.SetBounds(slotGroup.Width - 78, 2, 70, 16);
            refreshButton.Click += (s, e) => RefreshOutfits();
            slotGroup.Controls.Add(refreshButton);
            Controls.Add(slotGroup);

            Section components = new Section { Text = "Components", Location = new Point(358, 8), Size = new Size(340, 300) };
            components.Controls.Add(Header("Draw", 72, 20));
            components.Controls.Add(Header("Tex", 122, 20));
            components.Controls.Add(Header("Draw", 232, 20));
            components.Controls.Add(Header("Tex", 282, 20));
            AddParts(components, ClothesParts, 10, 40, 72, 122);
            AddParts(components, PropParts, 176, 40, 232, 282);

            saveComponentsButton = new Button { Text = "Save components", Location = new Point(176, 160), Size = new Size(152, 28) };
            saveComponentsButton.Click += (s, e) => SaveComponents();
            components.Controls.Add(saveComponentsButton);
            Controls.Add(components);

            int importHeight = DesiredHeight - 314 - Margin - TeleportHeight - Margin;
            Section import = new Section { Text = "Import", Location = new Point(358, 314), Size = new Size(340, importHeight) };
            importBox = new PasteTextBox { Location = new Point(10, 22), Size = new Size(320, 168), Multiline = true, ScrollBars = ScrollBars.Vertical, AcceptsReturn = true };
            importBox.TextChanged += (s, e) => ReformatImportText();
            import.Controls.Add(importBox);
            applyImportButton = new Button { Text = "Apply pasted text", Location = new Point(10, 200), Size = new Size(156, 26) };
            applyImportButton.Click += (s, e) => ApplyImportedText(importBox.Text);
            importFileButton = new Button { Text = "Import from file...", Location = new Point(174, 200), Size = new Size(156, 26) };
            importFileButton.Click += (s, e) => ImportFile();
            import.Controls.AddRange(new Control[] { applyImportButton, importFileButton });
            import.Controls.Add(new Label
            {
                Text = "Writes into the selected slot. Nothing is downloaded.",
                Location = new Point(10, 234), Size = new Size(320, 16), ForeColor = UiStyle.Muted
            });
            Controls.Add(import);

            Section teleport = new Section { Text = "Teleport", Location = new Point(358, 314 + importHeight + Margin), Size = new Size(340, TeleportHeight) };
            TileButton teleportButton = new TileButton { Text = "Teleport to clothing shop", Outline = true, Centered = true };
            teleportButton.SetBounds(10, 28, 320, 24);
            teleportButton.Click += async (s, e) => await TeleportToShop();
            teleport.Controls.Add(teleportButton);
            teleport.Controls.Add(new Label { Text = "Moves your character straight to the clothing shop.", Location = new Point(10, 56), Size = new Size(320, 16), ForeColor = UiStyle.Muted });
            Controls.Add(teleport);

            Section format = new Section { Text = "Import format", Location = new Point(RightColumnX, Margin) };
            int formatY = 26;
            formatY = AddFlowLabel(format, ImportFormatHelp, UiStyle.Muted, null, formatY);
            formatY = AddFlowLabel(format, ImportFormatExample, Theme.Text, Theme.Mono, formatY);
            formatY = AddFlowLabel(format, ImportFormatNotes, UiStyle.Muted, null, formatY);
            format.Size = new Size(RightColumnWidth, formatY + 2);
            Controls.Add(format);

            int statsTop = format.Bottom + Margin;
            statsSection = new StatsSection(host)
            {
                Location = new Point(RightColumnX, statsTop),
                Size = new Size(RightColumnWidth, Math.Max(246, DesiredHeight - statsTop - Margin))
            };
            Controls.Add(statsSection);
        }

        /// <summary>Adds a label as tall as its text needs (asked to the label itself, with its real font, so it never gets cut) and returns the y below it.</summary>
        private static int AddFlowLabel(Control parent, string text, Color color, Font font, int y)
        {
            const int width = RightColumnWidth - 20;
            Label label = new Label { Text = text, Location = new Point(10, y), ForeColor = color, UseMnemonic = false, Width = width };
            label.Font = font ?? Theme.Body;
            label.Height = label.GetPreferredSize(new Size(width, 0)).Height + 4;

            parent.Controls.Add(label);
            return y + label.Height + 6;
        }

        private static Label Header(string text, int x, int y)
        {
            return new Label { Text = text, AutoSize = true, Location = new Point(x, y), Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold) };
        }

        private void AddParts(Control parent, string[] parts, int x, int y, int idX, int texX)
        {
            foreach (string part in parts)
            {
                parent.Controls.Add(new Label { Text = part, AutoSize = true, Location = new Point(x, y + 4) });
                TextBox id = new TextBox { Location = new Point(idX, y), Width = 46 };
                TextBox tex = new TextBox { Location = new Point(texX, y), Width = 46 };
                parent.Controls.Add(id);
                parent.Controls.Add(tex);
                idBoxes[part] = id;
                texBoxes[part] = tex;
                y += 22;
            }
        }

        // ---- slots ----------------------------------------------------------------------

        private int SelectedSlot()
        {
            return slotList.SelectedSlot;
        }

        /// <summary>Reads the slots again from the game (for example after going online or changing character).</summary>
        private void RefreshOutfits()
        {
            if (!attached)
            {
                host.SetStatus("Connect to the game first", Color.DarkOrange);
                return;
            }

            LoadOutfits(SelectedSlot());
            host.Log("Outfit list refreshed.");
            host.SetStatus("Outfit list refreshed", Color.Blue);
        }

        private void LoadOutfits(int preferredSlot)
        {
            try
            {
                nameBox.Text = string.Empty;
                List<string> names = outfits.ReadNames().Select(name => name?.Trim() ?? string.Empty).ToList();
                slotList.SetNames(names, preferredSlot);
                OnSlotChanged();
            }
            catch (Exception ex)
            {
                host.Log("Outfit list error: " + ex.Message);
            }
        }

        private void OnSlotChanged()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
            {
                nameBox.Text = string.Empty;
                return;
            }

            nameBox.Text = slotList.NameOf(slot);
            LoadComponents(slot);
        }

        private void LoadComponents(int slot)
        {
            try
            {
                GTAOutfits.OutfitComponents c = outfits.ReadComponents(slot);
                Fill("Mask", c.Mask, c.MaskTex); Fill("Hair", c.Hair, c.HairTex); Fill("Gloves", c.Gloves, c.GlovesTex);
                Fill("Legs", c.Legs, c.LegsTex); Fill("Bag", c.Bag, c.BagTex); Fill("Shoes", c.Shoes, c.ShoesTex);
                Fill("Misc", c.Misc, c.MiscTex); Fill("Undershirt", c.Undershirt, c.UndershirtTex); Fill("Armor", c.Armor, c.ArmorTex);
                Fill("Decals", c.Decals, c.DecalsTex); Fill("Top", c.Top, c.TopTex);
                Fill("Hat", c.Hat, c.HatTex); Fill("Glasses", c.Glasses, c.GlassesTex); Fill("Earring", c.Earring, c.EarringTex);
                Fill("Watches", c.Watches, c.WatchesTex); Fill("Bracelets", c.Bracelets, c.BraceletsTex);
            }
            catch (Exception ex)
            {
                host.Log("Component read error: " + ex.Message);
            }
        }

        private void Fill(string part, int draw, int tex)
        {
            idBoxes[part].Text = draw.ToString();
            texBoxes[part].Text = tex.ToString();
        }

        private int Draw(string part) { return int.Parse(idBoxes[part].Text); }

        private int Tex(string part) { return int.Parse(texBoxes[part].Text); }

        private void SaveComponents()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
                return;

            try
            {
                GTAOutfits.OutfitComponents c = new GTAOutfits.OutfitComponents
                {
                    Mask = Draw("Mask"), MaskTex = Tex("Mask"), Hair = Draw("Hair"), HairTex = Tex("Hair"),
                    Gloves = Draw("Gloves"), GlovesTex = Tex("Gloves"), Legs = Draw("Legs"), LegsTex = Tex("Legs"),
                    Bag = Draw("Bag"), BagTex = Tex("Bag"), Shoes = Draw("Shoes"), ShoesTex = Tex("Shoes"),
                    Misc = Draw("Misc"), MiscTex = Tex("Misc"), Undershirt = Draw("Undershirt"), UndershirtTex = Tex("Undershirt"),
                    Armor = Draw("Armor"), ArmorTex = Tex("Armor"), Decals = Draw("Decals"), DecalsTex = Tex("Decals"),
                    Top = Draw("Top"), TopTex = Tex("Top"), Hat = Draw("Hat"), HatTex = Tex("Hat"),
                    Glasses = Draw("Glasses"), GlassesTex = Tex("Glasses"), Earring = Draw("Earring"), EarringTex = Tex("Earring"),
                    Watches = Draw("Watches"), WatchesTex = Tex("Watches"), Bracelets = Draw("Bracelets"), BraceletsTex = Tex("Bracelets")
                };

                outfits.WriteComponents(slot, c);
                Notify($"Slot {slot + 1} components saved!");
                host.SetStatus($"Slot {slot + 1} components saved", Color.Blue);
                host.Log($"Outfit slot {slot + 1}: components saved.");
            }
            catch (FormatException)
            {
                MessageBox.Show("Enter only integer values in the fields.", "Input error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Component save error: " + ex.Message);
            }
        }

        // ---- name -----------------------------------------------------------------------

        private static int Utf8Bytes(string text)
        {
            return Encoding.UTF8.GetByteCount(text ?? string.Empty);
        }

        /// <summary>31 bytes: the limit of the game's name field. It cannot be changed.</summary>
        private static int CurrentNameLimit()
        {
            return MaxOutfitNameBytes;
        }

        private void OnNameEdited()
        {
            UpdateNameCounter();
        }

        private void UpdateNameCounter()
        {
            int bytes = Utf8Bytes(nameBox.Text);
            int limit = CurrentNameLimit();
            nameBytesLabel.Text = $"{bytes}/{limit}";
            nameBytesLabel.ForeColor = bytes > limit ? Theme.Bad : Theme.Muted;
            saveNameButton.Enabled = attached && SelectedSlot() >= 0 && bytes <= limit;
        }

        private void SaveName()
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
                return;

            try
            {
                outfits.WriteName(slot, nameBox.Text);
                Notify($"Outfit {slot + 1} updated!");
                LoadOutfits(slot);
                host.SetStatus($"Slot {slot + 1} name saved", Color.Blue);
                host.Log($"Outfit slot {slot + 1}: name saved.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Save error: " + ex.Message);
            }
        }

        // ---- import ---------------------------------------------------------------------

        private void ReformatImportText()
        {
            if (formattingImport || importBox.Text.IndexOf('\n') >= 0)
                return;

            if (OutfitImport.TryParse(importBox.Text, out OutfitImport.ImportedOutfit imported, out string ignored) && imported.Parts.Count >= 3)
                SetImportText(OutfitImport.Format(imported));
        }

        private void SetImportText(string text)
        {
            formattingImport = true;
            try
            {
                importBox.Text = text;
                importBox.SelectionStart = importBox.TextLength;
            }
            finally
            {
                formattingImport = false;
            }
        }

        private void ImportFile()
        {
            if (!attached)
                return;

            string text;
            using (OpenFileDialog dialog = new OpenFileDialog { Title = "Import outfit", Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*" })
            {
                if (dialog.ShowDialog(FindForm()) != DialogResult.OK)
                    return;

                try
                {
                    text = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    MessageBox.Show("Cannot read file: " + ex.Message, "Import error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
            }

            SetImportText(text);
            ApplyImportedText(text);
        }

        private void ApplyImportedText(string text)
        {
            int slot = SelectedSlot();
            if (!attached || slot < 0)
            {
                MessageBox.Show("Select an outfit slot first.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                MessageBox.Show("Paste the outfit text first.", "Import", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            if (!OutfitImport.TryParse(text, out OutfitImport.ImportedOutfit imported, out string parseError))
            {
                MessageBox.Show(parseError, "Import error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            SetImportText(OutfitImport.Format(imported));

            int nameLimit = CurrentNameLimit();
            if (imported.Name != null && Utf8Bytes(imported.Name) > nameLimit)
            {
                MessageBox.Show($"Name too long: {Utf8Bytes(imported.Name)}/{nameLimit} bytes.\nNothing was written.",
                    "Outfit name limit", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string current = slotList.NameOf(slot);
            if (!host.Confirm($"Overwrite slot {slot + 1} ({current}) with the imported outfit?\n\nName: {imported.Name ?? "(unchanged)"}\nComponents: {imported.Parts.Count}", "Confirm import"))
                return;

            try
            {
                if (imported.Name != null)
                    outfits.WriteName(slot, imported.Name);

                if (imported.Parts.Count > 0)
                    outfits.WriteComponents(slot, OutfitImport.ApplyTo(imported, outfits.ReadComponents(slot)));

                Notify($"Outfit {slot + 1} imported!");
                LoadOutfits(slot);
                host.SetStatus($"Slot {slot + 1} imported", Color.Blue);
                host.Log($"Outfit slot {slot + 1}: imported.");
            }
            catch (Exception ex)
            {
                MessageBox.Show("Import error: " + ex.Message);
            }
        }

        private void Notify(string message)
        {
            try { host.Connection.Notify(message); } catch { }
        }
    }
}
