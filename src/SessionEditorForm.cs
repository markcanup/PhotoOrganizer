using System;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class SessionEditorForm : Form
    {
        private readonly TextBox _nameTextBox;
        private readonly ListBox _sourceListBox;
        private readonly ListBox _destinationListBox;
        private readonly CheckedListBox _actionsCheckedListBox;
        private readonly CheckBox _showFileNameCheckBox;
        private readonly CheckBox _recurseSubdirectoriesCheckBox;
        private readonly CheckBox _highlightDateDifferencesCheckBox;
        private readonly ComboBox _sortOrderComboBox;
        private readonly int _initialThumbnailSize;
        private readonly string _sessionId;
        private readonly int _initialInfoPanePercent;
        private string _lastBrowsedFolder;

        private SessionEditorForm(OrganizerSession session, string defaultBrowseFolder)
        {
            _initialThumbnailSize = session.ThumbnailSize;
            _sessionId = session.SessionId;
            _initialInfoPanePercent = session.InfoPanePercent;
            _lastBrowsedFolder = defaultBrowseFolder ?? string.Empty;

            Text = "Edit Session";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(760, 520);

            Controls.Add(CreateLabel("Session name", 16, 18));
            _nameTextBox = CreateTextBox(session.Name, 120, 14, 610);
            Controls.Add(_nameTextBox);

            GroupBox sourcesGroup = new GroupBox();
            sourcesGroup.Text = "Source folders";
            sourcesGroup.Location = new Point(16, 50);
            sourcesGroup.Size = new Size(350, 320);
            Controls.Add(sourcesGroup);

            _sourceListBox = new ListBox();
            _sourceListBox.Location = new Point(14, 28);
            _sourceListBox.Size = new Size(320, 210);
            foreach (string source in session.GetSourceFolders())
            {
                _sourceListBox.Items.Add(source);
            }
            sourcesGroup.Controls.Add(_sourceListBox);

            Button addSourceButton = new Button();
            addSourceButton.Text = "Browse...";
            addSourceButton.Location = new Point(36, 248);
            addSourceButton.Click += AddSourceButton_Click;
            sourcesGroup.Controls.Add(addSourceButton);

            Button removeSourceButton = new Button();
            removeSourceButton.Text = "Remove";
            removeSourceButton.Location = new Point(128, 248);
            removeSourceButton.Click += RemoveSourceButton_Click;
            sourcesGroup.Controls.Add(removeSourceButton);

            _recurseSubdirectoriesCheckBox = new CheckBox();
            _recurseSubdirectoriesCheckBox.AutoSize = true;
            _recurseSubdirectoriesCheckBox.Location = new Point(14, 286);
            _recurseSubdirectoriesCheckBox.Text = "Recurse subdirectories of source";
            _recurseSubdirectoriesCheckBox.Checked = session.RecurseSubdirectories;
            sourcesGroup.Controls.Add(_recurseSubdirectoriesCheckBox);

            GroupBox destinationsGroup = new GroupBox();
            destinationsGroup.Text = "Destination folders";
            destinationsGroup.Location = new Point(16, 380);
            destinationsGroup.Size = new Size(350, 120);
            Controls.Add(destinationsGroup);

            _destinationListBox = new ListBox();
            _destinationListBox.Location = new Point(14, 28);
            _destinationListBox.Size = new Size(320, 50);
            foreach (string destination in session.DestinationFolders) _destinationListBox.Items.Add(destination);
            destinationsGroup.Controls.Add(_destinationListBox);

            Button addDestinationButton = new Button();
            addDestinationButton.Text = "Browse...";
            addDestinationButton.Location = new Point(36, 84);
            addDestinationButton.Click += AddDestinationButton_Click;
            destinationsGroup.Controls.Add(addDestinationButton);

            Button removeDestinationButton = new Button();
            removeDestinationButton.Text = "Remove";
            removeDestinationButton.Location = new Point(128, 84);
            removeDestinationButton.Click += RemoveDestinationButton_Click;
            destinationsGroup.Controls.Add(removeDestinationButton);

            GroupBox optionsGroup = new GroupBox();
            optionsGroup.Text = "Session options";
            optionsGroup.Location = new Point(384, 50);
            optionsGroup.Size = new Size(346, 390);
            Controls.Add(optionsGroup);

            Label actionsHintLabel = new Label();
            actionsHintLabel.Location = new Point(14, 24);
            actionsHintLabel.Size = new Size(312, 30);
            actionsHintLabel.Text = "Checked actions appear in the right-click menu.";
            optionsGroup.Controls.Add(actionsHintLabel);

            _actionsCheckedListBox = new CheckedListBox();
            _actionsCheckedListBox.Location = new Point(14, 58);
            _actionsCheckedListBox.Size = new Size(312, 124);
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
            _showFileNameCheckBox.Location = new Point(14, 196);
            _showFileNameCheckBox.Text = "Show file name under thumbnail";
            _showFileNameCheckBox.Checked = session.ShowFileName;
            optionsGroup.Controls.Add(_showFileNameCheckBox);

            _highlightDateDifferencesCheckBox = new CheckBox();
            _highlightDateDifferencesCheckBox.AutoSize = true;
            _highlightDateDifferencesCheckBox.Location = new Point(14, 220);
            _highlightDateDifferencesCheckBox.Text = "Highlight date differences";
            _highlightDateDifferencesCheckBox.Checked = session.HighlightDateDifferences;
            optionsGroup.Controls.Add(_highlightDateDifferencesCheckBox);

            optionsGroup.Controls.Add(CreateLabel("Sort order", 14, 252));
            _sortOrderComboBox = new ComboBox();
            _sortOrderComboBox.FormattingEnabled = true;
            _sortOrderComboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            _sortOrderComboBox.Location = new Point(14, 276);
            _sortOrderComboBox.Size = new Size(312, 23);
            foreach (SessionSortOrder sortOrder in SessionSortCatalog.GetAll()) _sortOrderComboBox.Items.Add(sortOrder);
            _sortOrderComboBox.Format += SortOrderComboBox_Format;
            _sortOrderComboBox.SelectedItem = session.SortOrder;
            optionsGroup.Controls.Add(_sortOrderComboBox);

            Label sourceHintLabel = new Label();
            sourceHintLabel.Location = new Point(14, 316);
            sourceHintLabel.Size = new Size(312, 50);
            sourceHintLabel.Text = "Add one or more source folders. Photos from every configured source folder appear together in the grid.";
            optionsGroup.Controls.Add(sourceHintLabel);

            Button okButton = new Button();
            okButton.Text = "Save";
            okButton.Location = new Point(548, 470);
            okButton.DialogResult = DialogResult.OK;
            Controls.Add(okButton);

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(629, 470);
            cancelButton.DialogResult = DialogResult.Cancel;
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public OrganizerSession Session
        {
            get
            {
                System.Collections.Generic.List<string> sources = _sourceListBox.Items.Cast<string>().ToList();
                return new OrganizerSession
                {
                    Name = _nameTextBox.Text.Trim(),
                    SessionId = _sessionId,
                    SourceFolder = sources.FirstOrDefault() ?? string.Empty,
                    SourceFolders = sources,
                    DestinationFolders = _destinationListBox.Items.Cast<string>().ToList(),
                    VisibleActions = _actionsCheckedListBox.CheckedItems.Cast<SessionActionType>().ToList(),
                    ShowFileName = _showFileNameCheckBox.Checked,
                    HighlightDateDifferences = _highlightDateDifferencesCheckBox.Checked,
                    ThumbnailSize = _initialThumbnailSize <= 0 ? 150 : _initialThumbnailSize,
                    SortOrder = _sortOrderComboBox.SelectedItem is SessionSortOrder ? (SessionSortOrder)_sortOrderComboBox.SelectedItem : SessionSortOrder.FileNameAscending,
                    RecurseSubdirectories = _recurseSubdirectoriesCheckBox.Checked,
                    InfoPanePercent = _initialInfoPanePercent <= 0 ? 25 : _initialInfoPanePercent
                };
            }
        }

        public string LastBrowsedFolder
        {
            get { return _lastBrowsedFolder; }
        }

        public static bool TryEdit(IWin32Window owner, OrganizerSession session, string defaultBrowseFolder, out OrganizerSession updatedSession, out string lastBrowsedFolder)
        {
            using (SessionEditorForm form = new SessionEditorForm(session == null ? new OrganizerSession() : session.Clone(), defaultBrowseFolder))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    OrganizerSession candidate = form.Session;
                    if (string.IsNullOrWhiteSpace(candidate.Name))
                    {
                        MessageBox.Show(owner, "Please enter a session name.", "Missing Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        updatedSession = null;
                        lastBrowsedFolder = form.LastBrowsedFolder;
                        return false;
                    }

                    if (candidate.SourceFolders.Count == 0)
                    {
                        MessageBox.Show(owner, "Please add at least one source folder.", "Missing Source", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        updatedSession = null;
                        lastBrowsedFolder = form.LastBrowsedFolder;
                        return false;
                    }

                    updatedSession = candidate;
                    lastBrowsedFolder = form.LastBrowsedFolder;
                    return true;
                }

                updatedSession = null;
                lastBrowsedFolder = form.LastBrowsedFolder;
                return false;
            }
        }

        private void AddSourceButton_Click(object sender, EventArgs e)
        {
            string selectedPath;
            if (TryBrowseFolder("Select a source folder", false, out selectedPath))
            {
                AddUniqueItem(_sourceListBox, selectedPath);
            }
        }

        private void RemoveSourceButton_Click(object sender, EventArgs e)
        {
            if (_sourceListBox.SelectedItem != null)
            {
                _sourceListBox.Items.Remove(_sourceListBox.SelectedItem);
            }
        }

        private void AddDestinationButton_Click(object sender, EventArgs e)
        {
            string selectedPath;
            if (TryBrowseFolder("Select a destination folder", true, out selectedPath))
            {
                AddUniqueItem(_destinationListBox, selectedPath);
            }
        }

        private void RemoveDestinationButton_Click(object sender, EventArgs e)
        {
            if (_destinationListBox.SelectedItem != null)
            {
                _destinationListBox.Items.Remove(_destinationListBox.SelectedItem);
            }
        }

        private bool TryBrowseFolder(string description, bool allowCreate, out string selectedPath)
        {
            using (FolderBrowserDialog dialog = new FolderBrowserDialog())
            {
                dialog.Description = description;
                dialog.ShowNewFolderButton = allowCreate;
                dialog.SelectedPath = GetBrowseStartFolder();
                if (dialog.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dialog.SelectedPath))
                {
                    _lastBrowsedFolder = dialog.SelectedPath;
                    selectedPath = dialog.SelectedPath;
                    return true;
                }
            }

            selectedPath = null;
            return false;
        }

        private string GetBrowseStartFolder()
        {
            if (!string.IsNullOrWhiteSpace(_lastBrowsedFolder) && Directory.Exists(_lastBrowsedFolder))
            {
                return _lastBrowsedFolder;
            }

            string pictures = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
            return Directory.Exists(pictures) ? pictures : string.Empty;
        }

        private void ActionsCheckedListBox_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is SessionActionType) e.Value = SessionActionCatalog.GetDisplayName((SessionActionType)e.ListItem);
        }

        private void SortOrderComboBox_Format(object sender, ListControlConvertEventArgs e)
        {
            if (e.ListItem is SessionSortOrder) e.Value = SessionSortCatalog.GetDisplayName((SessionSortOrder)e.ListItem);
        }

        private static void AddUniqueItem(ListBox listBox, string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            if (!listBox.Items.Cast<string>().Any(item => string.Equals(item, path, StringComparison.OrdinalIgnoreCase)))
            {
                listBox.Items.Add(path);
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
