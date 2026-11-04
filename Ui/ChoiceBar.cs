using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace Arise_RTM
{
    /// <summary>A row of toggle buttons where one at most is selected (-1 = none). Styled like the toolbar's.</summary>
    internal sealed class ChoiceBar : Panel
    {
        private readonly List<RadioButton> buttons = new List<RadioButton>();
        private bool changing;

        public event EventHandler SelectedChanged;

        public ChoiceBar(IEnumerable<string> labels, int itemWidth, int itemHeight, int selected)
        {
            BackColor = Theme.Panel;
            Tag = "keep";

            int index = 0;
            foreach (string label in labels)
            {
                RadioButton button = new RadioButton
                {
                    Text = label, Location = new Point(index * itemWidth, 0), Size = new Size(itemWidth - 4, itemHeight),
                    Checked = index == selected
                };
                button.CheckedChanged += (s, e) => OnButtonChanged((RadioButton)s);
                buttons.Add(button);
                Controls.Add(button);
                index++;
            }

            Size = new Size(Math.Max(0, index * itemWidth - 4), itemHeight);
            Restyle();
        }

        public int SelectedIndex
        {
            get { return buttons.FindIndex(button => button.Checked); }
            set
            {
                changing = true;
                try
                {
                    for (int i = 0; i < buttons.Count; i++)
                        buttons[i].Checked = i == value;
                }
                finally
                {
                    changing = false;
                }

                Restyle();
            }
        }

        private void OnButtonChanged(RadioButton button)
        {
            Restyle();
            if (!changing && button.Checked)
                SelectedChanged?.Invoke(this, EventArgs.Empty);
        }

        private void Restyle()
        {
            foreach (RadioButton button in buttons)
                Theme.StyleToggle(button);
        }
    }
}
