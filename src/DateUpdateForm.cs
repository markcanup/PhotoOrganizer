using System;
using System.Drawing;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class DateUpdateForm : Form
    {
        private readonly DateTimePicker _picker;

        private DateUpdateForm(DateTime initialValue)
        {
            Text = "Date Update";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(350, 130);

            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(16, 18);
            label.Text = "Date taken / modified";

            _picker = new DateTimePicker();
            _picker.CustomFormat = "yyyy-MM-dd hh:mm:ss tt";
            _picker.Format = DateTimePickerFormat.Custom;
            _picker.Location = new Point(16, 44);
            _picker.Size = new Size(305, 23);
            _picker.Value = initialValue;

            Button saveButton = new Button();
            saveButton.Text = "Save";
            saveButton.Location = new Point(178, 88);
            saveButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(259, 88);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(label);
            Controls.Add(_picker);
            Controls.Add(saveButton);
            Controls.Add(cancelButton);

            AcceptButton = saveButton;
            CancelButton = cancelButton;
        }

        public DateTime SelectedValue
        {
            get { return _picker.Value; }
        }

        public static bool TryPrompt(IWin32Window owner, DateTime initialValue, out DateTime selectedValue)
        {
            using (DateUpdateForm form = new DateUpdateForm(initialValue))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    selectedValue = form.SelectedValue;
                    return true;
                }
            }

            selectedValue = initialValue;
            return false;
        }
    }
}
