using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>
    /// Dark, flat, minimal. Near-black surfaces, hairline borders, light text, and the Rockstar yellow used only
    /// for the primary action, the selected state and focus. No gradients, no rounded corners, no shadows.
    /// </summary>
    internal static class Theme
    {
        public static readonly Color Bg = Color.FromArgb(16, 16, 16);
        public static readonly Color Panel = Color.FromArgb(23, 23, 23);
        public static readonly Color Tile = Color.FromArgb(31, 31, 31);
        public static readonly Color TileHover = Color.FromArgb(42, 42, 42);
        public static readonly Color Input = Color.FromArgb(35, 35, 35);
        public static readonly Color Line = Color.FromArgb(42, 42, 42);
        public static readonly Color LineStrong = Color.FromArgb(62, 62, 62);
        public static readonly Color Text = Color.FromArgb(237, 237, 237);
        public static readonly Color Muted = Color.FromArgb(142, 142, 142);
        public static readonly Color Yellow = Color.FromArgb(252, 175, 23);
        public static readonly Color YellowHover = Color.FromArgb(255, 196, 66);
        public static readonly Color Ok = Color.FromArgb(95, 203, 143);
        public static readonly Color Bad = Color.FromArgb(229, 83, 75);

        public static readonly Font Body = new Font("Segoe UI", 8.25f);
        public static readonly Font Bold = new Font("Segoe UI", 8.25f, FontStyle.Bold);
        public static readonly Font Small = new Font("Segoe UI", 7.25f, FontStyle.Bold);
        public static readonly Font Mono = new Font("Consolas", 8f);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr SendMessage(IntPtr handle, int message, IntPtr wParam, string lParam);

        /// <summary>Greyed hint shown inside an empty single-line text box.</summary>
        public static void Cue(TextBox box, string text)
        {
            const int EM_SETCUEBANNER = 0x1501;
            if (box.IsHandleCreated)
                SendMessage(box.Handle, EM_SETCUEBANNER, IntPtr.Zero, text);
            else
                box.HandleCreated += (s, e) => SendMessage(box.Handle, EM_SETCUEBANNER, IntPtr.Zero, text);
        }

        public static void StyleButton(Button button, bool primary = false)
        {
            button.UseMnemonic = false;
            button.FlatStyle = FlatStyle.Flat;
            button.Font = primary ? Bold : Body;
            button.ForeColor = primary ? Color.Black : Text;
            button.BackColor = primary ? Yellow : Tile;
            button.FlatAppearance.BorderColor = primary ? Yellow : LineStrong;
            button.FlatAppearance.MouseOverBackColor = primary ? YellowHover : TileHover;
            button.FlatAppearance.MouseDownBackColor = Yellow;
            button.Cursor = Cursors.Hand;
        }

        public static void StyleToggle(RadioButton button)
        {
            button.Appearance = Appearance.Button;
            button.FlatStyle = FlatStyle.Flat;
            button.TextAlign = ContentAlignment.MiddleCenter;
            button.UseMnemonic = false;
            button.Cursor = Cursors.Hand;
            button.FlatAppearance.BorderColor = button.Checked ? Yellow : LineStrong;
            button.FlatAppearance.CheckedBackColor = Yellow;
            button.FlatAppearance.MouseOverBackColor = button.Checked ? Yellow : TileHover;
            button.Font = button.Checked ? Bold : Body;
            button.BackColor = button.Checked ? Yellow : Tile;
            button.ForeColor = button.Checked ? Color.Black : Text;
        }

        /// <summary>Themes every control below <paramref name="root"/> (Tag="primary": yellow button; Tag="keep": left alone).</summary>
        public static void Apply(Control root)
        {
            foreach (Control child in root.Controls)
            {
                if (Equals(child.Tag, "keep") || child is TileButton || child is ProgressStrip)
                    continue;

                if (child is Button button)
                    StyleButton(button, Equals(button.Tag, "primary"));
                else if (child is RadioButton radio)
                    StyleToggle(radio);
                else if (child is CheckBox check)
                {
                    check.ForeColor = Text;
                    check.FlatStyle = FlatStyle.Flat;
                    check.FlatAppearance.BorderColor = LineStrong;
                    check.FlatAppearance.CheckedBackColor = Yellow;
                    check.Cursor = Cursors.Hand;
                }
                else if (child is ComboBox combo)
                {
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.BackColor = Input;
                    combo.ForeColor = Text;
                }
                else if (child is TextBox box)
                {
                    if (!box.ReadOnly || box.BackColor == SystemColors.Control)
                        box.BackColor = Input;

                    if (box.ForeColor == SystemColors.WindowText || box.ForeColor == SystemColors.ControlText)
                        box.ForeColor = Text;

                    box.BorderStyle = BorderStyle.FixedSingle;
                }
                else if (child is Label label)
                {
                    if (label.ForeColor == SystemColors.ControlText)
                        label.ForeColor = Text;
                }
                else if (child is Section)
                    child.BackColor = Panel;
                else if (child is System.Windows.Forms.Panel)
                    child.BackColor = Bg;

                if (!(child is ComboBox))
                    Apply(child);
            }
        }
    }

    /// <summary>Flat tile: text on the left, optional count and small outlined badges on the right.</summary>
    internal sealed class TileButton : Button
    {
        private bool hover, pressed;

        public string Detail { get; set; }
        public bool Flow { get; set; }
        public bool Globals { get; set; }
        public bool Primary { get; set; }

        /// <summary>Yellow outline and text; fills with yellow on hover. For secondary actions.</summary>
        public bool Outline { get; set; }

        public bool Centered { get; set; }
        public bool Dimmed { get; set; }

        public TileButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            Height = 22;
            Font = Theme.Body;
            Cursor = Cursors.Hand;
            Margin = new Padding(0);
            UseMnemonic = false;
        }

        protected override void OnMouseEnter(EventArgs e) { hover = true; Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { hover = false; pressed = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { pressed = true; Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e) { pressed = false; Invalidate(); base.OnMouseUp(e); }
        protected override void OnGotFocus(EventArgs e) { Invalidate(); base.OnGotFocus(e); }
        protected override void OnLostFocus(EventArgs e) { Invalidate(); base.OnLostFocus(e); }
        protected override void OnEnabledChanged(EventArgs e) { Invalidate(); base.OnEnabledChanged(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            bool accent = Outline && Enabled;
            bool filled = Enabled && (Primary || pressed || (accent && hover));
            Color back = !Enabled ? Color.FromArgb(20, 20, 20)
                : pressed ? Theme.Yellow
                : accent ? (hover ? Theme.Yellow : Theme.Tile)
                : Primary ? (hover ? Theme.YellowHover : Theme.Yellow)
                : hover ? Theme.TileHover
                : Dimmed ? Color.FromArgb(26, 26, 26)
                : Theme.Tile;

            using (Brush fill = new SolidBrush(back))
                g.FillRectangle(fill, 0, 0, Width, Height);

            Color border = !Enabled ? Theme.Line
                : (Focused || (hover && !filled)) ? Theme.Yellow
                : (Primary || accent) ? Theme.Yellow
                : Theme.LineStrong;
            using (Pen pen = new Pen(border))
                g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);

            Color ink = !Enabled ? Color.FromArgb(90, 90, 90) : (filled ? Color.Black : (accent ? Theme.Yellow : (Dimmed ? Theme.Muted : Theme.Text)));
            Color sub = filled ? Color.FromArgb(60, 60, 60) : Theme.Muted;
            int right = Width - 6;

            if (!string.IsNullOrEmpty(Detail))
            {
                Size size = TextRenderer.MeasureText(g, Detail, Theme.Body, Size.Empty, TextFormatFlags.NoPadding);
                TextRenderer.DrawText(g, Detail, Theme.Body, new Point(right - size.Width, (Height - size.Height) / 2), sub, TextFormatFlags.NoPadding);
                right -= size.Width + 6;
            }

            if (Globals)
                right = DrawBadge(g, "G", right, Theme.Muted);

            if (Flow)
                right = DrawBadge(g, "!", right, filled ? Color.Black : Theme.Yellow);

            TextFormatFlags flags = TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix |
                                    (Centered ? TextFormatFlags.HorizontalCenter : TextFormatFlags.Left);
            Rectangle bounds = Centered ? new Rectangle(0, 0, Width, Height) : new Rectangle(8, 0, Math.Max(10, right - 10), Height);
            TextRenderer.DrawText(g, Text, (Primary || Outline) ? Theme.Bold : Theme.Body, bounds, ink, flags);
        }

        private int DrawBadge(Graphics g, string letter, int right, Color color)
        {
            const int size = 12;
            Rectangle badge = new Rectangle(right - size, (Height - size) / 2, size, size);
            using (Pen pen = new Pen(color))
                g.DrawRectangle(pen, badge.X, badge.Y, badge.Width - 1, badge.Height - 1);

            TextRenderer.DrawText(g, letter, Theme.Small, badge, color,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
            return right - size - 4;
        }
    }

    /// <summary>Text box that only accepts a whole number inside [Minimum, Maximum]; arrows and the mouse wheel step it.</summary>
    internal sealed class NumberBox : TextBox
    {
        public decimal Minimum { get; set; } = 0;
        public decimal Maximum { get; set; } = 100;

        public NumberBox()
        {
            BorderStyle = BorderStyle.FixedSingle;
            BackColor = Theme.Input;
            ForeColor = Theme.Text;
            TextAlign = HorizontalAlignment.Right;
            Text = "0";
        }

        public decimal Value
        {
            get
            {
                decimal parsed;
                return decimal.TryParse(Text.Trim(), out parsed) ? Clamp(parsed) : Minimum;
            }
            set { Text = Clamp(value).ToString("0"); }
        }

        private decimal Clamp(decimal value)
        {
            return Math.Max(Minimum, Math.Min(Maximum, Math.Round(value)));
        }

        protected override void OnKeyPress(KeyPressEventArgs e)
        {
            if (!char.IsControl(e.KeyChar) && !char.IsDigit(e.KeyChar) && !(e.KeyChar == '-' && Minimum < 0))
                e.Handled = true;

            base.OnKeyPress(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)
            {
                Value = Value + (e.KeyCode == Keys.Up ? 1 : -1);
                SelectionStart = Text.Length;
                e.Handled = true;
            }

            base.OnKeyDown(e);
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            Value = Value + (e.Delta > 0 ? 1 : -1);
            base.OnMouseWheel(e);
        }

        protected override void OnLeave(EventArgs e)
        {
            Value = Value;
            base.OnLeave(e);
        }
    }

    /// <summary>Thin progress line: yellow fill, or a sliding segment while the total is unknown.</summary>
    internal sealed class ProgressStrip : Control
    {
        private readonly Timer timer = new Timer { Interval = 30 };
        private int offset;
        private bool indeterminate;

        public int Maximum { get; set; } = 1;
        public int Value { get; set; }

        public bool Indeterminate
        {
            get => indeterminate;
            set
            {
                indeterminate = value;
                timer.Enabled = value;
                Invalidate();
            }
        }

        public ProgressStrip()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            Height = 6;
            timer.Tick += (s, e) => { offset = (offset + 14) % (Width + 140); Invalidate(); };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.Clear(Theme.Tile);
            using (Brush bar = new SolidBrush(Theme.Yellow))
            {
                if (indeterminate)
                    e.Graphics.FillRectangle(bar, offset - 140, 0, 140, Height);
                else if (Maximum > 0 && Value > 0)
                    e.Graphics.FillRectangle(bar, 0, 0, (int)((long)Width * Math.Min(Value, Maximum) / Maximum), Height);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
                timer.Dispose();

            base.Dispose(disposing);
        }
    }

    /// <summary>Panel with a muted uppercase title and a hairline under it (children start below the first 20 px).</summary>
    internal class Section : System.Windows.Forms.Panel
    {
        private const int TitleHeight = 19;

        public Section()
        {
            BackColor = Theme.Panel;
            DoubleBuffered = true;
            ResizeRedraw = true;
            Font = Theme.Body;
        }

        public override string Text
        {
            get => base.Text;
            set { base.Text = value; Invalidate(); }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            using (Pen border = new Pen(Theme.Line))
                e.Graphics.DrawRectangle(border, 0, 0, Width - 1, Height - 1);

            using (Pen divider = new Pen(Theme.Line))
                e.Graphics.DrawLine(divider, 0, TitleHeight, Width, TitleHeight);

            TextRenderer.DrawText(e.Graphics, Text.ToUpperInvariant(), Theme.Small, new Point(8, 3), Theme.Muted, TextFormatFlags.NoPadding);
        }
    }

    /// <summary>Text-only tab strip (selected tab: white text and a yellow underline) over a content area; no native tab control.</summary>
    internal sealed class ThemedTabs : System.Windows.Forms.Panel
    {
        private const int HeaderHeight = 28;
        private readonly List<System.Windows.Forms.Panel> pages = new List<System.Windows.Forms.Panel>();
        private readonly List<Rectangle> hitAreas = new List<Rectangle>();
        private readonly System.Windows.Forms.Panel content = new System.Windows.Forms.Panel { Dock = DockStyle.Fill, BackColor = Theme.Bg };
        private int selected = -1;

        public ThemedTabs()
        {
            DoubleBuffered = true;
            BackColor = Theme.Bg;
            Padding = new Padding(0, HeaderHeight, 0, 0);
            Controls.Add(content);
        }

        public int SelectedIndex
        {
            get => selected;
            set
            {
                if (value < 0 || value >= pages.Count)
                    return;

                selected = value;
                for (int i = 0; i < pages.Count; i++)
                    pages[i].Visible = i == selected;

                Invalidate();
            }
        }

        public void AddPage(System.Windows.Forms.Panel page)
        {
            page.Dock = DockStyle.Fill;
            page.BackColor = Theme.Bg;
            page.Visible = false;
            pages.Add(page);
            content.Controls.Add(page);
            if (selected < 0)
                SelectedIndex = 0;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
            hitAreas.Clear();

            using (Pen line = new Pen(Theme.Line))
                e.Graphics.DrawLine(line, 0, HeaderHeight - 1, Width, HeaderHeight - 1);

            int x = 2;
            for (int i = 0; i < pages.Count; i++)
            {
                Font font = i == selected ? Theme.Bold : Theme.Body;
                int width = TextRenderer.MeasureText(pages[i].Text, font).Width + 28;
                Rectangle area = new Rectangle(x, 0, width, HeaderHeight);
                hitAreas.Add(area);

                TextRenderer.DrawText(e.Graphics, pages[i].Text, font, area, i == selected ? Theme.Text : Theme.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPrefix);

                if (i == selected)
                {
                    using (Brush accent = new SolidBrush(Theme.Yellow))
                        e.Graphics.FillRectangle(accent, area.X + 8, HeaderHeight - 3, area.Width - 16, 3);
                }

                x += width;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            for (int i = 0; i < hitAreas.Count; i++)
            {
                if (hitAreas[i].Contains(e.Location))
                {
                    SelectedIndex = i;
                    return;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            Cursor = hitAreas.Exists(a => a.Contains(e.Location)) ? Cursors.Hand : Cursors.Default;
        }
    }
}
