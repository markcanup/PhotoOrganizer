using System;
using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class RenameRuleEditorForm : Form
    {
        private readonly TextBox _nameTextBox;
        private readonly ComboBox _typeComboBox;
        private readonly Label _value1Label;
        private readonly TextBox _value1TextBox;
        private readonly Label _value2Label;
        private readonly TextBox _value2TextBox;

        private RenameRuleEditorForm(RenameRule rule)
        {
            Text = "Rename Rule";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(560, 360);

            Controls.Add(CreateLabel("Rule name", 16, 18));
            _nameTextBox = CreateTextBox(rule.Name, 160, 14, 380);
            Controls.Add(_nameTextBox);

            Controls.Add(CreateLabel("Rule type", 16, 54));
            _typeComboBox = new ComboBox();
            _typeComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _typeComboBox.Location = new Point(160, 50);
            _typeComboBox.Size = new Size(380, 23);
            foreach (RenameRuleType type in RenameRuleCatalog.GetAll())
            {
                _typeComboBox.Items.Add(type);
            }
            _typeComboBox.Format += TypeComboBox_Format;
            _typeComboBox.SelectedItem = rule.RuleType;
            _typeComboBox.SelectedIndexChanged += TypeComboBox_SelectedIndexChanged;
            Controls.Add(_typeComboBox);

            _value1Label = CreateLabel("Text", 16, 92);
            Controls.Add(_value1Label);
            _value1TextBox = CreateTextBox(rule.Value1, 160, 88, 380);
            Controls.Add(_value1TextBox);

            _value2Label = CreateLabel("Replace with", 16, 128);
            Controls.Add(_value2Label);
            _value2TextBox = CreateTextBox(rule.Value2, 160, 124, 380);
            Controls.Add(_value2TextBox);

            Label helpLabel = new Label();
            helpLabel.Location = new Point(16, 170);
            helpLabel.Size = new Size(524, 100);
            helpLabel.Text = "Available macros:\r\n%date% = last modified date in YYYYMMDD\r\n%dateh% = last modified date in YYYY-MM-DD\r\n%time% = last modified time in HH-MM-SS\r\n%char##% = first ## characters of the filename\r\n%% = a single percent sign";
            Controls.Add(helpLabel);

            Button okButton = new Button();
            okButton.Text = "Save";
            okButton.Location = new Point(384, 314);
            okButton.DialogResult = DialogResult.OK;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(465, 314);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
            UpdateFieldLabels();
        }

        public RenameRule Rule
        {
            get
            {
                return new RenameRule
                {
                    Name = _nameTextBox.Text.Trim(),
                    RuleType = (RenameRuleType)_typeComboBox.SelectedItem,
                    Value1 = _value1TextBox.Text,
                    Value2 = _value2TextBox.Text
                };
            }
        }

        public static bool TryEdit(IWin32Window owner, RenameRule existingRule, out RenameRule rule)
        {
            using (RenameRuleEditorForm form = new RenameRuleEditorForm(existingRule ?? new RenameRule()))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    string validationMessage = form.ValidateInputs();
                    if (validationMessage != null)
                    {
                        MessageBox.Show(owner, validationMessage, "Invalid Rename Rule", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        rule = null;
                        return false;
                    }

                    rule = form.Rule;
                    return true;
                }
            }

            rule = null;
            return false;
        }

        private string ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(_nameTextBox.Text))
            {
                return "Please enter a rule name.";
            }

            RenameRuleType type = (RenameRuleType)_typeComboBox.SelectedItem;
            if (type == RenameRuleType.SubstituteText && _value1TextBox.Text.Length == 0)
            {
                return "Please enter the text to look for.";
            }

            if ((type == RenameRuleType.AddTextToStart || type == RenameRuleType.AddTextToEnd || type == RenameRuleType.ReplaceFullFilename) && _value1TextBox.Text.Length == 0)
            {
                return "Please enter text for this rename rule.";
            }

            if (type == RenameRuleType.RemoveText && _value1TextBox.Text.Length == 0)
            {
                return "Please enter text to remove.";
            }

            string macroError = ValidateMacroText(_value1TextBox.Text) ?? ValidateMacroText(_value2TextBox.Text);
            return macroError;
        }

        private static string ValidateMacroText(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != '%')
                {
                    continue;
                }

                if (i + 1 < text.Length && text[i + 1] == '%')
                {
                    i++;
                    continue;
                }

                int end = text.IndexOf('%', i + 1);
                if (end < 0)
                {
                    return "A % macro is missing its closing %.";
                }

                string token = text.Substring(i, (end - i) + 1);
                if (string.Equals(token, "%date%", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "%dateh%", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(token, "%time%", StringComparison.OrdinalIgnoreCase)
                    || Regex.IsMatch(token, @"^%char\d+%$", RegexOptions.IgnoreCase))
                {
                    i = end;
                    continue;
                }

                return "Unsupported macro: " + token;
            }

            return null;
        }

        private void TypeComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdateFieldLabels();
        }

        private void UpdateFieldLabels()
        {
            RenameRuleType type = (RenameRuleType)_typeComboBox.SelectedItem;
            _value2Label.Visible = type == RenameRuleType.SubstituteText;
            _value2TextBox.Visible = type == RenameRuleType.SubstituteText;

            switch (type)
            {
                case RenameRuleType.AddTextToStart:
                    _value1Label.Text = "Text to add";
                    break;
                case RenameRuleType.AddTextToEnd:
                    _value1Label.Text = "Text to add";
                    break;
                case RenameRuleType.RemoveText:
                    _value1Label.Text = "Text to remove";
                    break;
                case RenameRuleType.SubstituteText:
                    _value1Label.Text = "Look for";
                    _value2Label.Text = "Replace with";
                    break;
                case RenameRuleType.ReplaceFullFilename:
                    _value1Label.Text = "New filename";
                    break;
            }
        }

        private void TypeComboBox_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is RenameRuleType)
            {
                e.Value = RenameRuleCatalog.GetDisplayName((RenameRuleType)e.ListItem);
            }
        }

        private static Label CreateLabel(string text, int x, int y)
        {
            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(x, y);
            label.Text = text;
            return label;
        }

        private static TextBox CreateTextBox(string text, int x, int y, int width)
        {
            TextBox textBox = new TextBox();
            textBox.Location = new Point(x, y);
            textBox.Size = new Size(width, 23);
            textBox.Text = text ?? string.Empty;
            return textBox;
        }
    }
}
