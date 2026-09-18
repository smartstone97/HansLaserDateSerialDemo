using System;
using System.Drawing;
using System.Windows.Forms;

namespace HansLaserDateSerialDemo
{
    internal sealed class LanguageSelectionDialog : Form
    {
        private readonly FlowLayoutPanel _languageList;
        private string _selectedCultureName;

        public string SelectedCultureName => _selectedCultureName;

        public LanguageSelectionDialog(string currentCultureName)
        {
            _selectedCultureName = string.IsNullOrWhiteSpace(currentCultureName) ? "en" : currentCultureName;

            Text = GetText("language");
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(360, 260);
            Size = new Size(420, 360);
            Font = new Font("Microsoft YaHei UI", 9F);

            TableLayoutPanel shell = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 2,
                Padding = new Padding(14)
            };
            shell.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            shell.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Controls.Add(shell);

            _languageList = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(4)
            };
            _languageList.Resize += delegate
            {
                int width = _languageList.ClientSize.Width - _languageList.Padding.Left - _languageList.Padding.Right;
                foreach (Control child in _languageList.Controls)
                    child.Width = Math.Max(240, width - SystemInformation.VerticalScrollBarWidth);
            };
            shell.Controls.Add(_languageList, 0, 0);

            foreach (LanguageOption option in LanguageManager.GetAvailableLanguages())
                AddLanguageRadio(option);

            FlowLayoutPanel buttons = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(0, 10, 0, 0)
            };
            shell.Controls.Add(buttons, 0, 1);

            Button okButton = new Button { Width = 90, Height = 32, Text = GetText("save") };
            okButton.Click += delegate { DialogResult = DialogResult.OK; };
            buttons.Controls.Add(okButton);

            Button cancelButton = new Button { Width = 90, Height = 32, Text = GetText("cancel") };
            cancelButton.Click += delegate { DialogResult = DialogResult.Cancel; };
            buttons.Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        private void AddLanguageRadio(LanguageOption option)
        {
            RadioButton radio = new RadioButton
            {
                AutoSize = false,
                Height = 34,
                Margin = new Padding(0, 0, 0, 4),
                Text = option.DisplayName,
                Tag = option.CultureName,
                Checked = string.Equals(option.CultureName, _selectedCultureName, StringComparison.OrdinalIgnoreCase),
                TextAlign = ContentAlignment.MiddleLeft
            };
            radio.CheckedChanged += delegate
            {
                if (radio.Checked)
                    _selectedCultureName = (string)radio.Tag;
            };
            _languageList.Controls.Add(radio);
        }

        private static string GetText(string key)
        {
            return Resources.ResourceManager.GetString(key, Resources.Culture) ?? key;
        }
    }
}
