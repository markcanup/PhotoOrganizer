using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class UndoLogForm : Form
    {
        private readonly ListBox _listBox;
        private readonly Button _undoButton;
        private readonly Button _clearButton;

        private UndoLogForm(IEnumerable<ChangeLogEntry> entries)
        {
            Text = "Undo";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(860, 430);

            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(16, 16);
            label.Text = "Recent changes";
            Controls.Add(label);

            _listBox = new ListBox();
            _listBox.Location = new Point(16, 42);
            _listBox.Size = new Size(828, 310);
            _listBox.SelectionMode = SelectionMode.MultiExtended;
            _listBox.HorizontalScrollbar = true;
            _listBox.FormattingEnabled = true;
            foreach (ChangeLogEntry entry in entries)
            {
                _listBox.Items.Add(entry);
            }
            _listBox.Format += ListBox_Format;
            Controls.Add(_listBox);

            _undoButton = new Button();
            _undoButton.Text = "Undo Selected";
            _undoButton.Location = new Point(488, 376);
            _undoButton.DialogResult = DialogResult.OK;
            Controls.Add(_undoButton);

            _clearButton = new Button();
            _clearButton.Text = "Clear Selected";
            _clearButton.Location = new Point(613, 376);
            _clearButton.DialogResult = DialogResult.Retry;
            Controls.Add(_clearButton);

            Button closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Location = new Point(738, 376);
            closeButton.DialogResult = DialogResult.Cancel;
            Controls.Add(closeButton);

            AcceptButton = _undoButton;
            CancelButton = closeButton;
        }

        public List<ChangeLogEntry> SelectedEntries
        {
            get { return _listBox.SelectedItems.Cast<ChangeLogEntry>().ToList(); }
        }

        private void ListBox_Format(object sender, ListControlConvertEventArgs e)
        {
            ChangeLogEntry entry = e.ListItem as ChangeLogEntry;
            if (entry != null)
            {
                e.Value = ChangeLogFormatter.ToDisplayText(entry);
            }
        }

        public static UndoWindowAction ShowWindow(IWin32Window owner, IEnumerable<ChangeLogEntry> entries, out List<ChangeLogEntry> selectedEntries)
        {
            using (UndoLogForm form = new UndoLogForm(entries))
            {
                DialogResult result = form.ShowDialog(owner);
                selectedEntries = form.SelectedEntries;
                if (result == DialogResult.OK)
                {
                    return UndoWindowAction.UndoSelected;
                }

                if (result == DialogResult.Retry)
                {
                    return UndoWindowAction.ClearSelected;
                }

                return UndoWindowAction.Close;
            }
        }
    }

    internal enum UndoWindowAction
    {
        Close,
        UndoSelected,
        ClearSelected
    }
}
