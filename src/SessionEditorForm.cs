using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class SessionEditorForm : Form
    {
        private readonly TextBox _nameTextBox;
        private readonly TextBox _sourceFolderTextBox;
        private readonly ListBox _destinationListBox;
        private readonly CheckedListBox _actionsCheckedListBox;
        private readonly CheckBox _showFileNameCheckBox;
        private readonly CheckBox _recurseSubdirectoriesCheckBox;
        private readonly CheckBox _highlightDateDifferencesCheckBox;
        private readonly ComboBox _sortOrderComboBox;
        private readonly int _initialThumbnailSize;

        private SessionEditorForm(OrganizerSession session)
        {
            _initialThumbnailSize = session.ThumbnailSize;
            Text = "Edit Session";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(740, 470);

            Controls.Add(CreateLabel("Session name", 16, 18));
            _nameTextBox = CreateTextBox(session.Name, 120, 14, 590);
            Controls.Add(_nameTextBox);

            Controls.Add(CreateLabel("Source folder", 16, 54));
            _sourceFolderTextBox = CreateTextBox(session.SourceFolder, 120, 50, 500);
            Controls.Add(_sourceFolderTextBox);
            Button browseButton = new Button();
            browseButton.Text = "Browse...";
            browseButton.Location = new Point(630, 49);
            browseButton.Size = new Size(80, 26);
            browseButton.Click += BrowseButton_Click;
            Controls.Add(browseButton);

            _recurseSubdirectoriesCheckBox = new CheckBox();
            _recurseSubdirectoriesCheckBox.AutoSize = true;
            _recurseSubdirectoriesCheckBox.Location = new Point(120, 78);
            _recurseSubdirectoriesCheckBox.Text = "Recurse subdirectories of source";
            _recurseSubdirectoriesCheckBox.Checked = session.RecurseSubdirectories;
            Controls.Add(_recurseSubdirectoriesCheckBox);

            GroupBox destinationsGroup = new GroupBox();
            destinationsGroup.Text = "Destination folders";
            destinationsGroup.Location = new Point(16, 108);
            destinationsGroup.Size = new Size(330, 289);
            Controls.Add(destinationsGroup);

            _destinationListBox = new ListBox();
            _destinationListBox.Location = new Point(14, 28);
            _destinationListBox.Size = new Size(300, 210);
            foreach (string destination in session.DestinationFolders) _destinationListBox.Items.Add(destination);
            destinationsGroup.Controls.Add(_destinationListBox);

            Button addDestinationButton = new Button();
            addDestinationButton.Text = "Browse...";
            addDestinationButton.Location = new Point(35, 252);
            addDestinationButton.Click += AddDestinationButton_Click;
            destinationsGroup.Controls.Add(addDestinationButton);

            Button removeDestinationButton = new Button();
            removeDestinationButton.Text = "Remove";
            removeDestinationButton.Location = new Point(127, 252);
            removeDestinationButton.Click += RemoveDestinationButton_Click;
            destinationsGroup.Controls.Add(removeDestinationButton);

            GroupBox optionsGroup = new GroupBox();
            optionsGroup.Text = "Session options";
            optionsGroup.Location = new Point(364, 108);
            optionsGroup.Size = new Size(346, 274);
            Controls.Add(optionsGroup);

            Label actionsHintLabel = new Label();
            actionsHintLabel.Location = new Point(14, 24);
            actionsHintLabel.Size = new Size(312, 30);
            actionsHintLabel.Text = "Checked actions appear in the right-click menu.";
            optionsGroup.Controls.Add(actionsHintLabel);

            _actionsCheckedListBox = new CheckedListBox();
            _actionsCheckedListBox.Location = new Point(14, 58);
            _actionsCheckedListBox.Size = new Size(312, 94);
            _actionsCheckedListBox.FormattingEnabled = true;
            foreach (SessionActionType action in SessionActionCatalog.GetAll())
            {
                int index = _actionsCheckedListBox.Items.Add(action);
                _actionsCheckedListBox.SetItemChecked(index, session.VisibleActions.Contains(action));
            }
            _actionsCheckedListBox.Format += ActionsCheckedListBox_Format;
            optionsGroup.Controls.Add(_actionsCheckedListBox);

            _showFileNameCheckBox = new CheckBox();
            _showFileNameCheckBox.AutoSize = true;
            _showFileNameCheckBox.Location = new Point(14, 162);
            _showFileNameCheckBox.Text = "Show file name under thumbnail";
            _showFileNameCheckBox.Checked = session.ShowFileName;
            optionsGroup.Controls.Add(_showFileNameCheckBox);

            _highlightDateDifferencesCheckBox = new CheckBox();
            _highlightDateDifferencesCheckBox.AutoSize = true;
            _highlightDateDifferencesCheckBox.Location = new Point(14, 186);
            _highlightDateDifferencesCheckBox.Text = "Highlight date differences";
            _highlightDateDifferencesCheckBox.Checked = session.HighlightDateDifferences;
            optionsGroup.Controls.Add(_highlightDateDifferencesCheckBox);

            optionsGroup.Controls.Add(CreateLabel("Sort order", 14, 212));
            _sortOrderComboBox = new ComboBox();
            _sortOrderComboBox.FormattingEnabled = true;
            _sortOrderComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _sortOrderComboBox.Location = new Point(14, 234);
            _sortOrderComboBox.Size = new Size(312, 23);
            foreach (SessionSortOrder sortOrder in SessionSortCatalog.GetAll()) _sortOrderComboBox.Items.Add(sortOrder);
            _sortOrderComboBox.Format += SortOrderComboBox_Format;
            _sortOrderComboBox.SelectedItem = session.SortOrder;
            optionsGroup.Controls.Add(_sortOrderComboBox);

            Button okButton = new Button();
            okButton.Text = "Save";
            okButton.Location = new Point(548, 424);
            okButton.DialogResult = DialogResult.OK;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(629, 424);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public OrganizerSession Session
        {
            get
            {
                return new OrganizerSession
                {
                    Name = _nameTextBox.Text.Trim(),
                    SourceFolder = _sourceFolderTextBox.Text.Trim(),
                    DestinationFolders = _destinationListBox.Items.Cast<string>().ToList(),
                    VisibleActions = _actionsCheckedListBox.CheckedItems.Cast<SessionActionType>().ToList(),
                    ShowFileName = _showFileNameCheckBox.Checked,
                    HighlightDateDifferences = _highlightDateDifferencesCheckBox.Checked,
                    ThumbnailSize = _initialThumbnailSize <= 0 ? 150 : _initialThumbnailSize,
                    SortOrder = _sortOrderComboBox.SelectedItem is SessionSortOrder ? (SessionSortOrder)_sortOrderComboBox.SelectedItem : SessionSortOrder.FileNameAscending,
                    RecurseSubdirectories = _recurseSubdirectoriesCheckBox.Checked
                };
            }
        }

        public static bool TryEdit(IWin32Window owner, OrganizerSession session, out OrganizerSession updatedSession)
        {
            using (SessionEditorForm form = new SessionEditorForm(session == null ? new OrganizerSession() : session.Clone()))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    OrganizerSession candidate = form.Session;
                    if (string.IsNullOrWhiteSpace(candidate.Name))
                    {
                        MessageBox.Show(owner, "Please enter a session name.", "Missing Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        updatedSession = null;
                        return false;
                    }

                    updatedSession = candidate;
                    return true;
                }
            }

            updatedSession = null;
            return false;
        }

        private void BrowseButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select the source folder";
                dialog.ShowNewFolderButton = false;
                dialog.SelectedPath = _sourceFolderTextBox.Text;
                if (dialog.ShowDialog(this) == DialogResult.OK) _sourceFolderTextBox.Text = dialog.SelectedPath;
            }
        }

        private void AddDestinationButton_Click(object sender, EventArgs e)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = "Select a destination folder";
                dialog.ShowNewFolderButton = true;
                if (dialog.ShowDialog(this) == DialogResult.OK && dialog.SelectedPath.Length > 0) _destinationListBox.Items.Add(dialog.SelectedPath);
            }
        }

        private void RemoveDestinationButton_Click(object sender, EventArgs e)
        {
            if (_destinationListBox.SelectedItem != null) _destinationListBox.Items.Remove(_destinationListBox.SelectedItem);
        }

        private void ActionsCheckedListBox_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is SessionActionType) e.Value = SessionActionCatalog.GetDisplayName((SessionActionType)e.ListItem);
        }

        private void SortOrderComboBox_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is SessionSortOrder) e.Value = SessionSortCatalog.GetDisplayName((SessionSortOrder)e.ListItem);
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
