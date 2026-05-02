using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class SessionListForm : Form
    {
        private readonly ListBox _listBox;

        private SessionListForm(IEnumerable<OrganizerSession> sessions)
        {
            Text = "Open Session";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(420, 290);

            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(16, 16);
            label.Text = "Choose a saved session";

            _listBox = new ListBox();
            _listBox.Location = new Point(16, 42);
            _listBox.Size = new Size(388, 184);
            _listBox.DisplayMember = "Name";
            foreach (OrganizerSession session in sessions.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                _listBox.Items.Add(session.Clone());
            }

            Button openButton = new Button();
            openButton.Text = "Open";
            openButton.Location = new Point(242, 245);
            openButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(323, 245);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(label);
            Controls.Add(_listBox);
            Controls.Add(openButton);
            Controls.Add(cancelButton);

            AcceptButton = openButton;
            CancelButton = cancelButton;
        }

        public OrganizerSession SelectedSession
        {
            get { return _listBox.SelectedItem as OrganizerSession; }
        }

        public static bool TrySelect(IWin32Window owner, IEnumerable<OrganizerSession> sessions, out OrganizerSession session)
        {
            using (SessionListForm form = new SessionListForm(sessions))
            {
                if (form._listBox.Items.Count > 0)
                {
                    form._listBox.SelectedIndex = 0;
                }

                if (form.ShowDialog(owner) == DialogResult.OK && form.SelectedSession != null)
                {
                    session = form.SelectedSession;
                    return true;
                }
            }

            session = null;
            return false;
        }
    }
}
