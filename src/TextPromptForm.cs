using System;
using System.Drawing;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class TextPromptForm : Form
    {
        private readonly TextBox _valueTextBox;

        private TextPromptForm(string title, string label, string initialValue)
        {
            Text = title;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(460, 145);

            Label promptLabel = new Label();
            promptLabel.AutoSize = true;
            promptLabel.Location = new Point(16, 18);
            promptLabel.Text = label;

            _valueTextBox = new TextBox();
            _valueTextBox.Location = new Point(16, 44);
            _valueTextBox.Size = new Size(428, 23);
            _valueTextBox.Text = initialValue ?? string.Empty;

            Button okButton = new Button();
            okButton.Text = "Save";
            okButton.Location = new Point(288, 94);
            okButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(369, 94);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(promptLabel);
            Controls.Add(_valueTextBox);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public string Value
        {
            get { return _valueTextBox.Text.Trim(); }
        }

        public static bool TryPrompt(IWin32Window owner, string title, string label, string initialValue, out string value)
        {
            using (TextPromptForm form = new TextPromptForm(title, label, initialValue))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    value = form.Value;
                    return true;
                }
            }

            value = string.Empty;
            return false;
        }
    }
}
