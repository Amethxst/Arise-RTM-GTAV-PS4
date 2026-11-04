using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>The 20 outfit slots, always visible. A slot without an outfit is grey and cannot be selected.</summary>
    internal sealed class SlotList : Control
    {
        public const int RowHeight = 23;
        private const int NumberWidth = 28;

        private readonly List<string> names = new List<string>();
        private int selected = -1;
        private int hover = -1;

        public event EventHandler SelectedChanged;

        public SlotList()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint | ControlStyles.ResizeRedraw | ControlStyles.Selectable, true);
            TabStop = true;
        }

        public int SlotCount { get; set; } = 20;

        public int SelectedSlot
        {
            get { return selected; }
            set
            {
                if (selected == value)
                    return;

                selected = value;
                Invalidate();
                SelectedChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public string NameOf(int slot)
        {
            return slot >= 0 && slot < names.Count ? names[slot] : string.Empty;
        }

        public bool HasOutfit(int slot)
        {
            return NameOf(slot).Trim().Length > 0;
        }

        /// <summary>Replaces the names; the selection stays on the same slot when it still has an outfit.</summary>
        public void SetNames(IList<string> newNames, int preferredSlot)
        {
            names.Clear();
            for (int i = 0; i < SlotCount; i++)
                names.Add(newNames != null && i < newNames.Count ? (newNames[i] ?? string.Empty) : string.Empty);

            int target = HasOutfit(preferredSlot) ? preferredSlot : -1;
            if (target < 0)
            {
                for (int i = 0; i < SlotCount && target < 0; i++)
                    target = HasOutfit(i) ? i : -1;
            }

            bool changed = target != selected;
            selected = target;
            Invalidate();
            if (changed)
                SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        public void Clear()
        {
            names.Clear();
            bool changed = selected != -1;
            selected = -1;
            Invalidate();
            if (changed)
                SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        protected override Size DefaultSize => new Size(290, SlotCount * RowHeight + 2);

        protected override void OnPaint(PaintEventArgs e)
        {
            Graphics g = e.Graphics;
            g.Clear(Theme.Panel);

            for (int slot = 0; slot < SlotCount; slot++)
            {
                Rectangle row = new Rectangle(0, 1 + slot * RowHeight, Width, RowHeight);
                bool has = HasOutfit(slot);
                bool isSelected = slot == selected;

                if (isSelected)
                {
                    using (SolidBrush fill = new SolidBrush(Theme.TileHover))
                        g.FillRectangle(fill, row);
                    using (SolidBrush accent = new SolidBrush(Theme.Yellow))
                        g.FillRectangle(accent, row.X, row.Y, 3, row.Height);
                }
                else if (has && slot == hover)
                {
                    using (SolidBrush fill = new SolidBrush(Theme.Tile))
                        g.FillRectangle(fill, row);
                }

                Rectangle number = new Rectangle(8, row.Y, NumberWidth, row.Height);
                TextRenderer.DrawText(g, (slot + 1).ToString("00"), Theme.Body, number, has ? Theme.Text : Theme.LineStrong,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);

                Rectangle content = new Rectangle(NumberWidth + 10, row.Y + 1, Width - NumberWidth - 16, row.Height - 2);
                TextRenderer.DrawText(g, has ? NameOf(slot) : "empty", Theme.Body, content, has ? Theme.Text : Theme.LineStrong,
                    TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
            }
        }

        private int SlotAt(Point point)
        {
            int slot = (point.Y - 1) / RowHeight;
            return slot >= 0 && slot < SlotCount ? slot : -1;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            int slot = SlotAt(e.Location);
            if (slot != hover)
            {
                hover = slot;
                Cursor = slot >= 0 && HasOutfit(slot) ? Cursors.Hand : Cursors.Default;
                Invalidate();
            }

            base.OnMouseMove(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            hover = -1;
            Invalidate();
            base.OnMouseLeave(e);
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            Focus();
            int slot = SlotAt(e.Location);
            if (slot >= 0 && HasOutfit(slot))
                SelectedSlot = slot;

            base.OnMouseDown(e);
        }

        protected override bool IsInputKey(Keys keyData)
        {
            return keyData == Keys.Up || keyData == Keys.Down || base.IsInputKey(keyData);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            int step = e.KeyCode == Keys.Down ? 1 : e.KeyCode == Keys.Up ? -1 : 0;
            if (step != 0)
            {
                for (int slot = selected + step; slot >= 0 && slot < SlotCount; slot += step)
                {
                    if (HasOutfit(slot))
                    {
                        SelectedSlot = slot;
                        break;
                    }
                }

                e.Handled = true;
            }

            base.OnKeyDown(e);
        }
    }
}
