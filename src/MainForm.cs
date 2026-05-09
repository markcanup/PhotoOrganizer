using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace PictureOrganizer
{
    public sealed class MainForm : Form
    {
        private readonly ThumbnailGridControl _grid;
        private readonly SplitContainer _mainSplitContainer;
        private readonly Panel _detailPanel;
        private readonly Label _sessionNameValueLabel;
        private readonly TextBox _sourceValueTextBox;
        private readonly TextBox _destinationsValueTextBox;
        private readonly Label _gridCountValueLabel;
        private readonly Label _lastActionValueLabel;
        private readonly Button _saveSessionButton;
        private readonly Button _editSessionButton;
        private readonly Button _refreshButton;
        private readonly Button _cancelLoadButton;
        private readonly ProgressBar _loadingProgressBar;
        private readonly Label _selectionCountValueLabel;
        private readonly Panel _singleSelectionPanel;
        private readonly ListBox _multiSelectionListBox;
        private readonly ToolStrip _actionsToolStrip;
        private readonly Panel _actionsPanel;
        private readonly Label _fileNameValueLabel;
        private readonly Label _folderValueLabel;
        private readonly Label _formatValueLabel;
        private readonly Label _fileSizeValueLabel;
        private readonly Label _lastModifiedValueLabel;
        private readonly Label _exifDateValueLabel;
        private readonly Label _ratingValueLabel;
        private readonly Label _dimensionsValueLabel;
        private readonly Label _instructionsLabel;
        private readonly FlowLayoutPanel _detailFlow;
        private OrganizerSession _currentSession;
        private AppConfig _appConfig;
        private bool _sessionDirty;
        private bool _suppressSessionDirtyTracking;
        private bool _startupLoadPending;
        private CancellationTokenSource _loadCancellationSource;
        private int _loadSequence;

        public MainForm()
        {
            Text = "Photo Organizer";
            StartPosition = FormStartPosition.CenterScreen;
            ClientSize = new Size(1280, 780);
            MinimumSize = new Size(1000, 720);
            WindowState = FormWindowState.Maximized;
            MenuStrip menuStrip = BuildMenuStrip();
            menuStrip.Dock = DockStyle.Top;
            MainMenuStrip = menuStrip;
            Controls.Add(menuStrip);

            _mainSplitContainer = new SplitContainer();
            _mainSplitContainer.Dock = DockStyle.Fill;
            _mainSplitContainer.SplitterWidth = 6;
            _mainSplitContainer.Panel1MinSize = 200;
            _mainSplitContainer.Panel2MinSize = 20;
            _mainSplitContainer.SplitterMoved += MainSplitContainer_SplitterMoved;
            _mainSplitContainer.SplitterMoving += MainSplitContainer_SplitterMoving;
            Controls.Add(_mainSplitContainer);

            Panel gridHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12) };
            _mainSplitContainer.Panel1.Controls.Add(gridHost);
            Panel gridBorder = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            gridHost.Controls.Add(gridBorder);

            _detailPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(12), BackColor = SystemColors.Control };
            _mainSplitContainer.Panel2.Controls.Add(_detailPanel);

            _grid = new ThumbnailGridControl { Dock = DockStyle.Fill };
            _grid.SelectionChanged += Grid_SelectionChanged;
            _grid.ItemContextMenuRequested += Grid_ItemContextMenuRequested;
            _grid.ItemDoubleClicked += Grid_ItemDoubleClicked;
            _grid.ThumbnailSizeChanged += Grid_ThumbnailSizeChanged;
            _grid.DeleteRequested += Grid_DeleteRequested;
            gridBorder.Controls.Add(_grid);

            _detailFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, WrapContents = false, AutoScroll = true };
            _detailPanel.Controls.Add(_detailFlow);
            _detailFlow.Controls.Add(CreateHeaderPair("Current session:", out _sessionNameValueLabel));
            _detailFlow.Controls.Add(CreateTextBoxPair("Source(s):", out _sourceValueTextBox, 94));
            _detailFlow.Controls.Add(CreateTextBoxPair("Destination(s):", out _destinationsValueTextBox, 94));
            _detailFlow.Controls.Add(CreateHeaderPair("Images in grid:", out _gridCountValueLabel));
            _detailFlow.Controls.Add(CreateHeaderPair("Last action:", out _lastActionValueLabel));
            _loadingProgressBar = new ProgressBar { Width = 320, Height = 18, Visible = false, Margin = new Padding(0, 0, 0, 8) };
            _detailFlow.Controls.Add(_loadingProgressBar);

            FlowLayoutPanel buttonRow = new FlowLayoutPanel { AutoSize = true, Margin = new Padding(0, 8, 0, 6) };
            _saveSessionButton = new Button { Text = "Save Session", Size = new Size(100, 32) };
            _editSessionButton = new Button { Text = "Edit Session", Size = new Size(100, 32) };
            _refreshButton = new Button { Text = "Refresh Photos", Size = new Size(110, 32) };
            _cancelLoadButton = new Button { Text = "Cancel Load", Size = new Size(95, 32), Visible = false };
            _saveSessionButton.Click += SaveSessionMenuItem_Click;
            _editSessionButton.Click += EditSessionMenuItem_Click;
            _refreshButton.Click += RefreshButton_Click;
            _cancelLoadButton.Click += CancelLoadButton_Click;
            buttonRow.Controls.Add(_saveSessionButton);
            buttonRow.Controls.Add(_editSessionButton);
            buttonRow.Controls.Add(_refreshButton);
            buttonRow.Controls.Add(_cancelLoadButton);
            _detailFlow.Controls.Add(buttonRow);
            _detailFlow.Controls.Add(CreateDivider());
            _detailFlow.Controls.Add(CreateHeaderPair("Images selected:", out _selectionCountValueLabel));

            _singleSelectionPanel = new Panel { AutoSize = true, Margin = new Padding(0, 6, 0, 0), Size = new Size(320, 340) };
            _singleSelectionPanel.Controls.Add(CreateValueRow("Filename:", 0, out _fileNameValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("Folder:", 42, out _folderValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("Image format:", 84, out _formatValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("File size:", 126, out _fileSizeValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("File last modified:", 168, out _lastModifiedValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("EXIF date taken:", 210, out _exifDateValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("Rating:", 252, out _ratingValueLabel));
            _singleSelectionPanel.Controls.Add(CreateValueRow("Image dimensions:", 294, out _dimensionsValueLabel));
            _detailFlow.Controls.Add(_singleSelectionPanel);

            _multiSelectionListBox = new ListBox { Width = 320, Height = 96, Margin = new Padding(0, 8, 0, 0), Visible = false };
            _detailFlow.Controls.Add(_multiSelectionListBox);

            _actionsPanel = new Panel { Width = 320, Height = 80, Margin = new Padding(0, 8, 0, 0), Visible = false };
            _actionsPanel.Controls.Add(new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Location = new Point(0, 0), Text = "Actions:" });
            _actionsToolStrip = new ToolStrip
            {
                Dock = DockStyle.None,
                Location = new Point(0, 22),
                GripStyle = ToolStripGripStyle.Hidden,
                LayoutStyle = ToolStripLayoutStyle.VerticalStackWithOverflow,
                AutoSize = false,
                Width = 320,
                Height = 54,
                ShowItemToolTips = true
            };
            _actionsPanel.Controls.Add(_actionsToolStrip);
            _detailFlow.Controls.Add(_actionsPanel);

            _detailFlow.Controls.Add(CreateDivider());
            _instructionsLabel = new Label { AutoSize = false, Width = 320, Height = 160 };
            _detailFlow.Controls.Add(_instructionsLabel);

            FormClosing += MainForm_FormClosing;
            Shown += MainForm_Shown;
            Resize += MainForm_Resize;
            _detailPanel.Resize += DetailPanel_Resize;
            _appConfig = SessionConfigStore.Load();
            _currentSession = new OrganizerSession();
            LoadLastSession();
            ApplySessionDisplaySettings();
            RefreshSessionSummary();
            UpdateSelectionDetails();
            SetStatus("Ready");
            _startupLoadPending = true;
        }

        private MenuStrip BuildMenuStrip()
        {
            MenuStrip menu = new MenuStrip();
            ToolStripMenuItem file = new ToolStripMenuItem("File");
            file.DropDownItems.Add("New session", null, NewSessionMenuItem_Click);
            file.DropDownItems.Add("Open session", null, OpenSessionMenuItem_Click);
            file.DropDownItems.Add("Edit current session", null, EditSessionMenuItem_Click);
            file.DropDownItems.Add("Save session", null, SaveSessionMenuItem_Click);
            file.DropDownItems.Add("Renaming rules...", null, RenamingRulesMenuItem_Click);
            file.DropDownItems.Add(new ToolStripSeparator());
            file.DropDownItems.Add("Exit", null, ExitMenuItem_Click);
            menu.Items.Add(file);
            menu.Items.Add(new ToolStripMenuItem("Undo", null, UndoMenuItem_Click));
            menu.Items.Add(new ToolStripMenuItem("Help", null, HelpMenuItem_Click));
            return menu;
        }

        private void LoadLastSession()
        {
            if (_appConfig.Sessions.Count == 0) return;
            OrganizerSession last = _appConfig.Sessions.FirstOrDefault(item => string.Equals(item.Name, _appConfig.LastSessionName, StringComparison.OrdinalIgnoreCase))
                ?? _appConfig.Sessions.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).FirstOrDefault();
            if (last != null) { _currentSession = last.Clone(); _sessionDirty = false; }
        }

        private void NewSessionMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardOrSave()) return;
            CancelActiveLoad();
            _currentSession = new OrganizerSession();
            _sessionDirty = false;
            ApplySessionDisplaySettings();
            RefreshSessionSummary();
            _grid.SetItems(new PhotoItem[0]);
            UpdateSelectionDetails();
            EditSessionMenuItem_Click(sender, e);
        }

        private void OpenSessionMenuItem_Click(object sender, EventArgs e)
        {
            if (!ConfirmDiscardOrSave()) return;
            CancelActiveLoad();
            OrganizerSession selected;
            if (!SessionListForm.TrySelect(this, _appConfig.Sessions, out selected)) return;
            _currentSession = selected.Clone();
            _sessionDirty = false;
            _appConfig.LastSessionName = _currentSession.Name;
            SessionConfigStore.Save(_appConfig);
            ApplySessionDisplaySettings();
            RefreshSessionSummary();
            RefreshPhotos();
        }

        private void EditSessionMenuItem_Click(object sender, EventArgs e)
        {
            OrganizerSession previous = _currentSession.Clone();
            OrganizerSession edited;
            string lastBrowsedFolder;
            if (!SessionEditorForm.TryEdit(this, _currentSession, SessionConfigStore.GetDefaultBrowseFolder(_appConfig, GetPreferredBrowseFolder()), out edited, out lastBrowsedFolder)) return;
            PersistLastBrowsedFolder(lastBrowsedFolder);
            bool sameSource = AreSourceFoldersEqual(edited.GetSourceFolders(), _currentSession.GetSourceFolders());
            bool recurseChanged = edited.RecurseSubdirectories != _currentSession.RecurseSubdirectories;
            bool sortChanged = edited.SortOrder != _currentSession.SortOrder;
            bool showFileNameChanged = edited.ShowFileName != _currentSession.ShowFileName;
            bool highlightChanged = edited.HighlightDateDifferences != _currentSession.HighlightDateDifferences;
            edited.ThumbnailSize = _currentSession.ThumbnailSize;
            edited.InfoPanePercent = _currentSession.InfoPanePercent;
            edited.SessionId = _currentSession.SessionId;
            _currentSession = edited;
            _sessionDirty = true;
            AppendSessionSettingsLog(previous, edited);
            ApplySessionDisplaySettings();
            RefreshSessionSummary();
            if (!sameSource || recurseChanged)
            {
                RefreshPhotos();
                return;
            }

            if (showFileNameChanged)
            {
                _grid.Invalidate();
            }

            if (highlightChanged)
            {
                _grid.Invalidate();
            }

            if (sortChanged)
            {
                ResortGrid();
                return;
            }

            UpdateSelectionDetails();
        }

        private void SaveSessionMenuItem_Click(object sender, EventArgs e) { SaveCurrentSession(); }
        private void UndoMenuItem_Click(object sender, EventArgs e) { ShowUndoWindow(); }
        private void HelpMenuItem_Click(object sender, EventArgs e) { ShowHelpWindow(); }
        private void RenamingRulesMenuItem_Click(object sender, EventArgs e)
        {
            if (RenameRulesManagerForm.ShowManager(this))
            {
                _appConfig = SessionConfigStore.Load();
            }
        }
        private void ExitMenuItem_Click(object sender, EventArgs e) { Close(); }

        private void MainForm_Resize(object sender, EventArgs e)
        {
            if (_currentSession == null)
            {
                return;
            }

            ApplySplitRatioFromSession();
        }

        private void DetailPanel_Resize(object sender, EventArgs e)
        {
            UpdateDetailLayout();
            _detailPanel.BringToFront();
        }

        private void MainSplitContainer_SplitterMoving(object sender, SplitterCancelEventArgs e)
        {
            int width = Math.Max(1, _mainSplitContainer.ClientSize.Width);
            int minPanel2 = Math.Max(1, (int)Math.Round(width * 0.01));
            int maxPanel2 = Math.Max(minPanel2, (int)Math.Round(width * 0.50));
            int maxSplitterDistance = width - maxPanel2;
            int minSplitterDistance = Math.Max(1, width - minPanel2);
            int clamped = Math.Max(maxSplitterDistance, Math.Min(minSplitterDistance, e.SplitX));
            e.SplitX = clamped;
        }

        private void MainSplitContainer_SplitterMoved(object sender, SplitterEventArgs e)
        {
            if (_currentSession == null)
            {
                return;
            }

            SaveCurrentSplitRatio();
            UpdateDetailLayout();
        }

        private void MainForm_Shown(object sender, EventArgs e)
        {
            if (!_startupLoadPending) return;
            _startupLoadPending = false;
            UpdateDetailLayout();
            if (_appConfig.Sessions.Count == 0)
            {
                EditSessionMenuItem_Click(sender, e);
                return;
            }
            RefreshPhotos();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            CancelActiveLoad();
            if (!ConfirmDiscardOrSave()) e.Cancel = true;
        }

        private bool ConfirmDiscardOrSave()
        {
            if (!_sessionDirty) return true;
            DialogResult result = MessageBox.Show(this, "Save changes to the current session before continuing?", "Unsaved Session", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel) return false;
            return result != DialogResult.Yes || SaveCurrentSession();
        }

        private bool SaveCurrentSession()
        {
            if (string.IsNullOrWhiteSpace(_currentSession.Name))
            {
                string sessionName;
                if (!TextPromptForm.TryPrompt(this, "Save Session", "Session name", _currentSession.Name, out sessionName)) return false;
                _currentSession.Name = sessionName;
            }
            if (string.IsNullOrWhiteSpace(_currentSession.Name))
            {
                MessageBox.Show(this, "Please provide a session name before saving.", "Missing Name", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return false;
            }
            SessionConfigStore.SaveSession(_currentSession);
            _appConfig = SessionConfigStore.Load();
            _sessionDirty = false;
            RefreshSessionSummary();
            SetStatus("Session saved to " + AppPaths.ConfigFilePath);
            return true;
        }

        private void RefreshButton_Click(object sender, EventArgs e) { RefreshPhotos(); }
        private void CancelLoadButton_Click(object sender, EventArgs e) { CancelActiveLoad(); }
        private void Grid_SelectionChanged(object sender, EventArgs e) { UpdateSelectionDetails(); }
        private void Grid_ItemDoubleClicked(object sender, ItemDoubleClickEventArgs e) { OpenFullscreen(e.FilePath); }
        private void Grid_DeleteRequested(object sender, EventArgs e) { DeleteSelectedItems(); }

        private void Grid_ThumbnailSizeChanged(object sender, EventArgs e)
        {
            if (_suppressSessionDirtyTracking || _currentSession == null)
            {
                return;
            }

            if (_currentSession.ThumbnailSize == _grid.ThumbnailSize)
            {
                return;
            }

            _currentSession.ThumbnailSize = _grid.ThumbnailSize;
            _sessionDirty = true;
            RefreshSessionSummary();
        }

        private void Grid_ItemContextMenuRequested(object sender, ItemContextMenuEventArgs e)
        {
            if (_grid.SelectedCount == 0) return;
            BuildContextMenu().Show(_grid, e.Location);
        }

        private ContextMenuStrip BuildContextMenu()
        {
            ContextMenuStrip menu = new ContextMenuStrip();
            List<PhotoItem> selectedItems = GetSelectedItems();
            List<string> selectedPaths = selectedItems.Select(item => item.FilePath).ToList();
            bool single = selectedItems.Count == 1;
            bool containsPdf = selectedItems.Any(item => item.IsPdf);
            bool allJpeg = selectedItems.Count > 0 && selectedItems.All(item => PhotoMetadataHelper.IsJpegFile(item.FilePath));
            bool allPng = selectedItems.Count > 0 && selectedItems.All(item => PhotoMetadataHelper.IsPngFile(item.FilePath));

            foreach (SessionActionType action in _currentSession.VisibleActions)
            {
                switch (action)
                {
                    case SessionActionType.View:
                    case SessionActionType.Fullscreen:
                    case SessionActionType.Compare:
                        ToolStripMenuItem view = BuildViewMenuItem(selectedPaths, single, containsPdf);
                        if (!menu.Items.OfType<ToolStripItem>().Any(item => string.Equals(item.Name, "ViewActionItem", StringComparison.Ordinal)))
                        {
                            view.Name = "ViewActionItem";
                            menu.Items.Add(view);
                        }
                        break;
                    case SessionActionType.Copy:
                        menu.Items.Add(BuildDestinationMenu("Copy", _currentSession.DestinationFolders, delegate(string d)
                        {
                            ExecuteAction(delegate
                            {
                                FileTransferResult result = ImageFileOperations.CopyFiles(selectedPaths, d, ResolveConflict);
                                foreach (FileTransferPair pair in result.CompletedTransfers)
                                {
                                    AppendChangeLog(ChangeLogKind.Copy, "Copied " + Path.GetFileName(pair.SourcePath) + " -> " + Path.GetFileName(pair.DestinationPath), new PathPairChange
                                    {
                                        SourcePath = pair.SourcePath,
                                        DestinationPath = pair.DestinationPath
                                    });
                                }
                                SetStatus(BuildTransferStatus("Copied", result));
                            }, "Copied.");
                        }));
                        break;
                    case SessionActionType.Move:
                        menu.Items.Add(BuildDestinationMenu("Move", _currentSession.DestinationFolders, delegate(string d)
                        {
                            ExecuteAction(delegate
                            {
                                FileTransferResult result = ImageFileOperations.MoveFiles(selectedPaths, d, ResolveConflict);
                                foreach (FileTransferPair pair in result.CompletedTransfers)
                                {
                                    AppendChangeLog(ChangeLogKind.Move, "Moved " + Path.GetFileName(pair.SourcePath) + " -> " + Path.GetFileName(pair.DestinationPath), new PathPairChange
                                    {
                                        SourcePath = pair.SourcePath,
                                        DestinationPath = pair.DestinationPath
                                    });
                                }
                                _grid.RemoveItems(result.SourcePathsRemoved);
                                PromptToDeleteEmptySourceFolders(result.SourcePathsRemoved);
                                UpdateSelectionDetails();
                                SetStatus(BuildTransferStatus("Moved", result));
                            }, "Moved.");
                        }));
                        break;
                    case SessionActionType.DateUpdate:
                        ToolStripMenuItem dateItem = CreateActionMenuItem(SessionActionType.DateUpdate, "Date update", delegate
                        {
                            DateTime initial = PhotoMetadataHelper.GetBestDate(selectedPaths[0]);
                            DateTime selected;
                            if (DateUpdateForm.TryPrompt(this, initial, out selected))
                            {
                                ExecuteAction(delegate
                                {
                                    foreach (string path in selectedPaths)
                                    {
                                        DateUpdateFileChange change = new DateUpdateFileChange
                                        {
                                            FilePath = path,
                                            OldDate = PhotoMetadataHelper.GetBestDate(path),
                                            NewDate = selected
                                        };
                                        PhotoMetadataHelper.UpdateDateTakenAndModified(path, selected);
                                        AppendChangeLog(ChangeLogKind.DateUpdate, "Updated date for " + Path.GetFileName(path), change);
                                        UpdateGridItem(path);
                                    }
                                }, "Updated dates.");
                            }
                        });
                        dateItem.Enabled = !containsPdf;
                        menu.Items.Add(dateItem);
                        break;
                    case SessionActionType.Rename:
                        menu.Items.Add(BuildRenameMenu(selectedPaths, single));
                        break;
                    case SessionActionType.Convert:
                        menu.Items.Add(BuildConvertMenu(selectedPaths, containsPdf, allJpeg, allPng));
                        break;
                    case SessionActionType.Autocrop:
                        ToolStripMenuItem crop = CreateActionMenuItem(SessionActionType.Autocrop, "Autocrop", delegate
                        {
                            ExecuteAction(delegate { ImageFileOperations.AutoCropFiles(selectedPaths); UpdateGridItems(selectedPaths); }, "Autocropped.");
                        });
                        crop.Enabled = !containsPdf;
                        menu.Items.Add(crop);
                        break;
                    case SessionActionType.Rotate:
                        menu.Items.Add(BuildRotateMenu(selectedPaths, containsPdf));
                        break;
                    case SessionActionType.Delete:
                        menu.Items.Add(CreateActionMenuItem(SessionActionType.Delete, "Delete", delegate { DeleteSelectedItems(); }));
                        break;
                    case SessionActionType.Rating:
                        menu.Items.Add(BuildRatingMenu(selectedPaths));
                        break;
                    case SessionActionType.Edit:
                        ToolStripMenuItem edit = CreateActionMenuItem(SessionActionType.Edit, "Edit", delegate { ImageFileOperations.OpenExternalEditor(selectedPaths[0]); });
                        edit.Enabled = single;
                        menu.Items.Add(edit);
                        break;
                }
            }
            return menu;
        }

        private void RebuildActionsInfoPanel()
        {
            _actionsToolStrip.Items.Clear();
            _actionsPanel.Visible = _currentSession != null && _currentSession.ShowActionsInInfoPanel;
            if (!_actionsPanel.Visible)
            {
                UpdateDetailLayout();
                return;
            }

            if (_grid.SelectedCount == 0)
            {
                ToolStripLabel placeholder = new ToolStripLabel("Select one or more images to show actions.");
                placeholder.AutoSize = false;
                placeholder.Width = Math.Max(180, _actionsToolStrip.Width - 8);
                _actionsToolStrip.Items.Add(placeholder);
                UpdateDetailLayout();
                return;
            }

            ContextMenuStrip menu = BuildContextMenu();
            foreach (ToolStripMenuItem item in menu.Items.OfType<ToolStripMenuItem>())
            {
                _actionsToolStrip.Items.Add(CreateToolStripActionItem(item));
            }
            UpdateDetailLayout();
        }

        private ToolStripItem CreateToolStripActionItem(ToolStripMenuItem sourceItem)
        {
            if (sourceItem.DropDownItems.Count > 0)
            {
                ToolStripDropDownButton button = new ToolStripDropDownButton(sourceItem.Text, sourceItem.Image);
                button.Enabled = sourceItem.Enabled;
                button.AutoSize = false;
                button.Width = Math.Max(180, _actionsToolStrip.Width - 8);
                foreach (ToolStripMenuItem child in sourceItem.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    button.DropDownItems.Add(CreateClonedMenuItem(child));
                }

                return button;
            }

            ToolStripButton actionButton = new ToolStripButton(sourceItem.Text, sourceItem.Image);
            actionButton.Enabled = sourceItem.Enabled;
            actionButton.AutoSize = false;
            actionButton.Width = Math.Max(180, _actionsToolStrip.Width - 8);
            actionButton.TextAlign = ContentAlignment.MiddleLeft;
            actionButton.Click += delegate { sourceItem.PerformClick(); };
            return actionButton;
        }

        private ToolStripMenuItem CreateClonedMenuItem(ToolStripMenuItem sourceItem)
        {
            ToolStripMenuItem clone = new ToolStripMenuItem(sourceItem.Text, sourceItem.Image);
            clone.Enabled = sourceItem.Enabled;
            if (sourceItem.DropDownItems.Count > 0)
            {
                foreach (ToolStripMenuItem child in sourceItem.DropDownItems.OfType<ToolStripMenuItem>())
                {
                    clone.DropDownItems.Add(CreateClonedMenuItem(child));
                }
            }
            else
            {
                clone.Click += delegate { sourceItem.PerformClick(); };
            }

            return clone;
        }

        private ToolStripMenuItem BuildDestinationMenu(string label, IEnumerable<string> destinations, Action<string> action)
        {
            ToolStripMenuItem menu = CreateActionMenuItem(string.Equals(label, "Copy", StringComparison.OrdinalIgnoreCase) ? SessionActionType.Copy : SessionActionType.Move, label + " ->", null);
            List<string> available = destinations == null ? new List<string>() : destinations.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (available.Count == 0)
            {
                menu.DropDownItems.Add(new ToolStripMenuItem("No destinations configured") { Enabled = false });
                return menu;
            }
            foreach (string destination in available)
            {
                string capture = destination;
                menu.DropDownItems.Add(new ToolStripMenuItem(GetDestinationMenuDisplayPath(capture), null, delegate { action(capture); }));
            }
            return menu;
        }

        private ToolStripMenuItem BuildRenameMenu(List<string> selectedPaths, bool single)
        {
            ToolStripMenuItem menu = CreateActionMenuItem(SessionActionType.Rename, "Rename ->", null);
            ToolStripMenuItem editItem = new ToolStripMenuItem("Edit", null, delegate
            {
                string filePath = selectedPaths[0];
                string newName;
                if (TextPromptForm.TryPrompt(this, "Rename File", "File name", Path.GetFileNameWithoutExtension(filePath), out newName))
                {
                    ExecuteAction(delegate
                    {
                        string renamed = ImageFileOperations.RenameFile(filePath, newName);
                        AppendChangeLog(ChangeLogKind.Rename, "Renamed " + Path.GetFileName(filePath) + " -> " + Path.GetFileName(renamed), new PathPairChange { SourcePath = filePath, DestinationPath = renamed });
                        ReplaceGridItem(filePath, renamed);
                        _grid.SelectSingle(renamed);
                    }, "Renamed.");
                }
            }) { Enabled = single };
            menu.DropDownItems.Add(editItem);
            List<RenameRule> rules = SessionConfigStore.Load().RenameRules;
            if (rules.Count == 0)
            {
                menu.DropDownItems.Add(new ToolStripMenuItem("No rename rules configured") { Enabled = false });
                return menu;
            }
            foreach (RenameRule rule in rules)
            {
                RenameRule capture = rule.Clone();
                menu.DropDownItems.Add(new ToolStripMenuItem(capture.Name, null, delegate
                {
                    ExecuteAction(delegate
                    {
                        List<string> renamed = ImageFileOperations.ApplyRenameRule(selectedPaths, capture);
                        for (int i = 0; i < selectedPaths.Count && i < renamed.Count; i++)
                        {
                            AppendChangeLog(ChangeLogKind.Rename, "Renamed " + Path.GetFileName(selectedPaths[i]) + " -> " + Path.GetFileName(renamed[i]), new PathPairChange { SourcePath = selectedPaths[i], DestinationPath = renamed[i] });
                            ReplaceGridItem(selectedPaths[i], renamed[i]);
                        }
                        if (renamed.Count == 1) _grid.SelectSingle(renamed[0]);
                    }, "Renamed.");
                }));
            }
            return menu;
        }

        private ToolStripMenuItem BuildConvertMenu(List<string> selectedPaths, bool containsPdf, bool allJpeg, bool allPng)
        {
            ToolStripMenuItem menu = CreateActionMenuItem(SessionActionType.Convert, "Convert ->", null);
            ToolStripMenuItem jpeg = new ToolStripMenuItem("JPEG", null, delegate
            {
                ExecuteAction(delegate
                {
                    List<string> createdPaths = ImageFileOperations.ConvertFiles(selectedPaths, ImageFormat.Jpeg);
                    for (int i = 0; i < selectedPaths.Count && i < createdPaths.Count; i++)
                    {
                        AppendChangeLog(ChangeLogKind.Convert, "Converted " + Path.GetFileName(selectedPaths[i]) + " -> " + Path.GetFileName(createdPaths[i]), new ConvertFileChange { SourcePath = selectedPaths[i], CreatedPath = createdPaths[i] });
                    }
                    AddGridItems(createdPaths);
                }, "Converted.");
            }) { Enabled = !containsPdf && !allJpeg };
            ToolStripMenuItem png = new ToolStripMenuItem("PNG", null, delegate
            {
                ExecuteAction(delegate
                {
                    List<string> createdPaths = ImageFileOperations.ConvertFiles(selectedPaths, ImageFormat.Png);
                    for (int i = 0; i < selectedPaths.Count && i < createdPaths.Count; i++)
                    {
                        AppendChangeLog(ChangeLogKind.Convert, "Converted " + Path.GetFileName(selectedPaths[i]) + " -> " + Path.GetFileName(createdPaths[i]), new ConvertFileChange { SourcePath = selectedPaths[i], CreatedPath = createdPaths[i] });
                    }
                    AddGridItems(createdPaths);
                }, "Converted.");
            }) { Enabled = !containsPdf && !allPng };
            menu.DropDownItems.Add(jpeg);
            menu.DropDownItems.Add(png);
            return menu;
        }

        private ToolStripMenuItem BuildRotateMenu(List<string> selectedPaths, bool containsPdf)
        {
            ToolStripMenuItem menu = CreateActionMenuItem(SessionActionType.Rotate, "Rotate ->", null);
            menu.Enabled = !containsPdf;
            menu.DropDownItems.Add(new ToolStripMenuItem("90 degrees clockwise", null, delegate { ExecuteRotate(selectedPaths, RotateFlipType.Rotate90FlipNone, "Rotated " + selectedPaths.Count + " file(s) 90 degrees clockwise."); }));
            menu.DropDownItems.Add(new ToolStripMenuItem("90 degrees counter-clockwise", null, delegate { ExecuteRotate(selectedPaths, RotateFlipType.Rotate270FlipNone, "Rotated " + selectedPaths.Count + " file(s) 90 degrees counter-clockwise."); }));
            menu.DropDownItems.Add(new ToolStripMenuItem("180 degrees", null, delegate { ExecuteRotate(selectedPaths, RotateFlipType.Rotate180FlipNone, "Rotated " + selectedPaths.Count + " file(s) 180 degrees."); }));
            return menu;
        }

        private ToolStripMenuItem BuildRatingMenu(List<string> selectedPaths)
        {
            bool supported = selectedPaths.Count > 0 && selectedPaths.All(PhotoMetadataHelper.SupportsShellRating);
            ToolStripMenuItem menu = CreateActionMenuItem(SessionActionType.Rating, "Rating ->", null);
            menu.Enabled = supported;
            menu.DropDownItems.Add(new ToolStripMenuItem("Clear", null, delegate { ApplyRating(selectedPaths, null); }));
            for (int rating = 1; rating <= 5; rating++)
            {
                int capture = rating;
                menu.DropDownItems.Add(new ToolStripMenuItem(capture + " star" + (capture == 1 ? string.Empty : "s"), null, delegate { ApplyRating(selectedPaths, capture); }));
            }
            return menu;
        }

        private ToolStripMenuItem BuildViewMenuItem(List<string> selectedPaths, bool single, bool containsPdf)
        {
            string label = single ? "Fullscreen" : selectedPaths.Count == 2 ? "Compare" : "View";
            bool enabled = single || (selectedPaths.Count == 2 && !containsPdf);
            ToolStripMenuItem menuItem = CreateActionMenuItem(SessionActionType.View, label, delegate
            {
                if (single)
                {
                    OpenFullscreen(selectedPaths[0]);
                }
                else if (selectedPaths.Count == 2 && !containsPdf)
                {
                    OpenCompare(selectedPaths);
                }
            });
            menuItem.Enabled = enabled;
            return menuItem;
        }

        private ToolStripMenuItem CreateActionMenuItem(SessionActionType actionType, string text, Action action)
        {
            ToolStripMenuItem menuItem = new ToolStripMenuItem(text);
            menuItem.Image = ActionIconCatalog.GetIcon(actionType);
            if (action != null)
            {
                menuItem.Click += delegate { action(); };
            }

            return menuItem;
        }

        private void OpenFullscreen(string selectedPath)
        {
            int index = _grid.IndexOf(selectedPath);
            using (FullscreenViewerForm viewer = new FullscreenViewerForm(_grid.Items.Select(item => item.FilePath).ToList(), index)) viewer.ShowDialog(this);
        }

        private void OpenCompare(List<string> selectedPaths)
        {
            if (selectedPaths == null || selectedPaths.Count != 2)
            {
                return;
            }

            using (CompareViewerForm viewer = new CompareViewerForm(selectedPaths[0], selectedPaths[1])) viewer.ShowDialog(this);
        }

        private void RefreshSessionSummary()
        {
            _sessionNameValueLabel.Text = (string.IsNullOrWhiteSpace(_currentSession.Name) ? "New session" : _currentSession.Name) + (_sessionDirty ? " *" : string.Empty);
            List<string> sourceFolders = _currentSession.GetSourceFolders();
            _sourceValueTextBox.Text = sourceFolders.Count == 0 ? "Not set" : string.Join(Environment.NewLine, sourceFolders.ToArray());
            _destinationsValueTextBox.Text = _currentSession.DestinationFolders.Count == 0 ? "None" : string.Join(Environment.NewLine, _currentSession.DestinationFolders.ToArray());
            RebuildActionsInfoPanel();
        }

        private async void RefreshPhotos()
        {
            CancelActiveLoad();
            List<string> sourceFolders = _currentSession.GetSourceFolders().Where(Directory.Exists).ToList();
            if (sourceFolders.Count == 0)
            {
                _grid.SetItems(new PhotoItem[0]);
                UpdateSelectionDetails();
                SetStatus("Choose at least one valid source folder.");
                return;
            }

            int loadId = ++_loadSequence;
            CancellationTokenSource cancellationSource = new CancellationTokenSource();
            _loadCancellationSource = cancellationSource;
            CancellationToken cancellationToken = cancellationSource.Token;

            try
            {
                BeginProgress(0, "Scanning files...");
                List<string> filePaths = await Task.Run(delegate
                {
                    return ApplySortOrder(EnumerateSupportedFiles(sourceFolders, _currentSession.RecurseSubdirectories, cancellationToken)).ToList();
                }, cancellationToken);
                if (!IsCurrentLoad(loadId, cancellationSource)) return;

                _grid.SetItems(filePaths.Select(PhotoMetadataHelper.CreatePlaceholderPhotoItem).ToList());
                UpdateSelectionDetails();

                if (filePaths.Count == 0)
                {
                    SetStatus("No files found.");
                    return;
                }

                BeginProgress(filePaths.Count, "Loading 0 of " + filePaths.Count + "...");
                for (int i = 0; i < filePaths.Count; i++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string filePath = filePaths[i];
                    PhotoItem item;
                    try
                    {
                        item = await Task.Run(delegate { return CreatePhotoItem(filePath); }, cancellationToken);
                    }
                    catch (Exception)
                    {
                        UpdateProgress(i + 1, filePaths.Count, "Loading " + (i + 1) + " of " + filePaths.Count + "...");
                        continue;
                    }

                    if (!IsCurrentLoad(loadId, cancellationSource))
                    {
                        item.Dispose();
                        return;
                    }

                    if (File.Exists(filePath))
                    {
                        _grid.RefreshItem(filePath, item);
                    }
                    else
                    {
                        item.Dispose();
                    }

                    UpdateProgress(i + 1, filePaths.Count, "Loading " + (i + 1) + " of " + filePaths.Count + "...");
                }

                if (!IsCurrentLoad(loadId, cancellationSource)) return;
                UpdateSelectionDetails();
                SetStatus("Loaded " + filePaths.Count + " item(s).");
            }
            catch (OperationCanceledException)
            {
                if (IsCurrentLoad(loadId, cancellationSource))
                {
                    SetStatus("Loading cancelled.");
                }
            }
            catch (Exception ex)
            {
                if (IsCurrentLoad(loadId, cancellationSource))
                {
                    _grid.SetItems(new PhotoItem[0]);
                    UpdateSelectionDetails();
                    SetStatus("Error loading files: " + ex.Message);
                }
            }
            finally
            {
                if (ReferenceEquals(_loadCancellationSource, cancellationSource))
                {
                    _loadCancellationSource = null;
                }

                cancellationSource.Dispose();
                if (loadId == _loadSequence)
                {
                    EndProgress();
                }
            }
        }

        private void UpdateGridItems(IEnumerable<string> filePaths) { foreach (string filePath in filePaths) UpdateGridItem(filePath); }

        private void UpdateGridItem(string filePath)
        {
            if (!File.Exists(filePath)) { _grid.RemoveItems(new[] { filePath }); UpdateSelectionDetails(); return; }
            _grid.RefreshItem(filePath, CreatePhotoItem(filePath));
            UpdateSelectionDetails();
        }

        private void ReplaceGridItem(string existingPath, string newPath) { _grid.ReplaceItem(existingPath, CreatePhotoItem(newPath)); UpdateSelectionDetails(); }
        private void AddGridItems(IEnumerable<string> filePaths)
        {
            _grid.AddItems(filePaths.Where(File.Exists).Select(CreatePhotoItem).ToList());
            ResortGrid();
            UpdateSelectionDetails();
        }

        private PhotoItem CreatePhotoItem(string filePath)
        {
            return PhotoMetadataHelper.CreatePhotoItem(filePath, 360);
        }

        private IEnumerable<string> EnumerateSupportedFiles(IEnumerable<string> sourceFolders, bool recurseSubdirectories, CancellationToken cancellationToken)
        {
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (string sourceFolder in sourceFolders.Where(path => !string.IsNullOrWhiteSpace(path)).Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!Directory.Exists(sourceFolder))
                {
                    continue;
                }

                if (!recurseSubdirectories)
                {
                    foreach (string filePath in Directory.GetFiles(sourceFolder))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (PhotoMetadataHelper.IsSupportedSourceFile(filePath) && seen.Add(filePath))
                        {
                            yield return filePath;
                        }
                    }

                    continue;
                }

                Stack<string> pending = new Stack<string>();
                pending.Push(sourceFolder);
                while (pending.Count > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    string folder = pending.Pop();
                    foreach (string filePath in Directory.GetFiles(folder))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (PhotoMetadataHelper.IsSupportedSourceFile(filePath) && seen.Add(filePath))
                        {
                            yield return filePath;
                        }
                    }

                    foreach (string childFolder in Directory.GetDirectories(folder))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        pending.Push(childFolder);
                    }
                }
            }
        }

        private bool IsCurrentLoad(int loadId, CancellationTokenSource cancellationSource)
        {
            return loadId == _loadSequence
                && ReferenceEquals(_loadCancellationSource, cancellationSource)
                && !cancellationSource.IsCancellationRequested;
        }

        private void ExecuteAction(Action action, string successMessage)
        {
            try { action(); if (!_loadingProgressBar.Visible) SetStatus(successMessage); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Action Failed", MessageBoxButtons.OK, MessageBoxIcon.Error); SetStatus("Action failed."); }
        }

        private void SetStatus(string message)
        {
            _lastActionValueLabel.Text = message;
            _instructionsLabel.Text = "Selection\r\nCtrl-click toggles photos.\r\nShift-click selects a range.\r\nDouble-click opens fullscreen.\r\nRight-click opens the action menu.\r\nDrag a thumbnail edge to resize all thumbnails.\r\nUse the pane divider to resize the info panel.";
        }

        private void ApplySessionDisplaySettings()
        {
            bool previousSuppression = _suppressSessionDirtyTracking;
            _suppressSessionDirtyTracking = true;
            try
            {
                _grid.ThumbnailSize = _currentSession.ThumbnailSize;
                _grid.ShowFileName = _currentSession.ShowFileName;
                _grid.HighlightDateDifferences = _currentSession.HighlightDateDifferences;
                ApplySplitRatioFromSession();
                RebuildActionsInfoPanel();
            }
            finally
            {
                _suppressSessionDirtyTracking = previousSuppression;
            }
        }

        private void UpdateSelectionDetails()
        {
            List<PhotoItem> selected = GetSelectedItems();
            _gridCountValueLabel.Text = _grid.Items.Count.ToString();
            _selectionCountValueLabel.Text = selected.Count.ToString();
            _multiSelectionListBox.Items.Clear();
            if (selected.Count == 1)
            {
                PhotoItem item = selected[0];
                _singleSelectionPanel.Visible = true;
                _multiSelectionListBox.Visible = false;
                _fileNameValueLabel.Text = item.DisplayName;
                _folderValueLabel.Text = item.FolderPath;
                _formatValueLabel.Text = item.FileExtension;
                _fileSizeValueLabel.Text = FormatFileSize(item.FileSizeBytes);
                _lastModifiedValueLabel.Text = item.LastWriteTime.ToString("yyyy-MM-dd hh:mm:ss tt");
                _exifDateValueLabel.Text = !item.MetadataLoaded ? "Loading..." : item.IsPdf ? item.PageCount + " page(s)" : item.ExifDateTaken.HasValue ? item.ExifDateTaken.Value.ToString("yyyy-MM-dd hh:mm:ss tt") : "Not available";
                _ratingValueLabel.Text = item.IsPdf ? string.Empty : !item.MetadataLoaded ? "Loading..." : !PhotoMetadataHelper.SupportsShellRating(item.FilePath) ? "Not supported" : item.Rating.HasValue ? item.Rating.Value + " star(s)" : "Not rated";
                _dimensionsValueLabel.Text = !item.MetadataLoaded ? "Loading..." : item.PixelSize.Width + " x " + item.PixelSize.Height;
                RebuildActionsInfoPanel();
                return;
            }
            ClearSingleSelectionDetails();
            if (selected.Count > 1)
            {
                _singleSelectionPanel.Visible = false;
                _multiSelectionListBox.Visible = true;
                foreach (PhotoItem item in selected) _multiSelectionListBox.Items.Add(item.DisplayName);
                RebuildActionsInfoPanel();
                return;
            }
            _singleSelectionPanel.Visible = false;
            _multiSelectionListBox.Visible = false;
            RebuildActionsInfoPanel();
        }

        private List<PhotoItem> GetSelectedItems()
        {
            List<string> selectedPaths = _grid.SelectedPaths;
            return _grid.Items.Where(item => selectedPaths.Contains(item.FilePath, StringComparer.OrdinalIgnoreCase)).OrderBy(item => item.DisplayName, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void ClearSingleSelectionDetails()
        {
            _fileNameValueLabel.Text = string.Empty;
            _folderValueLabel.Text = string.Empty;
            _formatValueLabel.Text = string.Empty;
            _fileSizeValueLabel.Text = string.Empty;
            _lastModifiedValueLabel.Text = string.Empty;
            _exifDateValueLabel.Text = string.Empty;
            _ratingValueLabel.Text = string.Empty;
            _dimensionsValueLabel.Text = string.Empty;
        }

        private void BeginProgress(int maximum, string message)
        {
            _loadingProgressBar.Visible = true;
            _cancelLoadButton.Visible = true;
            _loadingProgressBar.Minimum = 0;
            _loadingProgressBar.Maximum = Math.Max(1, maximum);
            _loadingProgressBar.Value = 0;
            _loadingProgressBar.Style = maximum > 0 ? ProgressBarStyle.Continuous : ProgressBarStyle.Marquee;
            UseWaitCursor = true;
            SetStatus(message);
            _loadingProgressBar.Refresh();
            _detailPanel.Refresh();
            Application.DoEvents();
        }

        private void UpdateProgress(int value, int maximum, string message)
        {
            _loadingProgressBar.Style = ProgressBarStyle.Continuous;
            _cancelLoadButton.Visible = true;
            _loadingProgressBar.Maximum = Math.Max(1, maximum);
            _loadingProgressBar.Value = Math.Max(0, Math.Min(_loadingProgressBar.Maximum, value));
            SetStatus(message);
            _loadingProgressBar.Refresh();
            _detailPanel.Refresh();
            Application.DoEvents();
        }

        private void EndProgress()
        {
            _loadingProgressBar.Visible = false;
            _cancelLoadButton.Visible = false;
            _loadingProgressBar.Value = 0;
            UseWaitCursor = false;
        }

        private void CancelActiveLoad()
        {
            if (_loadCancellationSource != null && !_loadCancellationSource.IsCancellationRequested)
            {
                _loadCancellationSource.Cancel();
            }
        }

        private ConflictResolutionChoice ResolveConflict(string sourcePath, string destinationPath)
        {
            ConflictResolutionChoice choice;
            return ConflictResolutionForm.TryResolve(this, sourcePath, destinationPath, out choice) ? choice : new ConflictResolutionChoice { Resolution = ConflictResolutionOption.Rename, FollowUp = ConflictFollowUpOption.CancelOperation };
        }

        private void ApplyRating(IEnumerable<string> filePaths, int? rating)
        {
            ExecuteAction(delegate
            {
                foreach (string path in filePaths.Where(PhotoMetadataHelper.SupportsShellRating))
                {
                    int? previous = ShellRatingHelper.TryReadRating(path);
                    RatingFileChange change = new RatingFileChange
                    {
                        FilePath = path,
                        OldRating = previous.GetValueOrDefault(),
                        OldRatingHasValue = previous.HasValue,
                        NewRating = rating.GetValueOrDefault(),
                        NewRatingHasValue = rating.HasValue
                    };
                    ShellRatingHelper.WriteRating(path, rating);
                    AppendChangeLog(ChangeLogKind.Rating, "Updated rating for " + Path.GetFileName(path), change);
                    UpdateGridItem(path);
                }
            }, rating.HasValue ? "Rating updated." : "Rating cleared.");
        }

        private void DeleteSelectedItems()
        {
            List<string> selectedPaths = _grid.SelectedPaths;
            if (selectedPaths.Count == 0)
            {
                return;
            }

            if (MessageBox.Show(this, "Delete " + selectedPaths.Count + " item(s) from the source folder?", "Delete Files", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            ExecuteAction(delegate
            {
                foreach (string path in selectedPaths)
                {
                    string entryId = Guid.NewGuid().ToString("N");
                    string backupPath = ChangeLogStore.CreateBackupPath(entryId, path);
                    File.Copy(path, backupPath, true);
                    if (!RecycleBinHelper.TrySendFileToRecycleBin(path))
                    {
                        File.Delete(path);
                    }
                    AppendChangeLog(ChangeLogKind.Delete, "Deleted " + Path.GetFileName(path), new DeleteBackupChange { OriginalPath = path, BackupPath = backupPath }, entryId);
                }
                _grid.RemoveItems(selectedPaths);
                PromptToDeleteEmptySourceFolders(selectedPaths);
                UpdateSelectionDetails();
            }, "Deleted.");
        }

        private void ExecuteRotate(List<string> selectedPaths, RotateFlipType rotateFlipType, string summary)
        {
            ExecuteAction(delegate
            {
                List<KeyValuePair<string, RotateBackupChange>> logs = new List<KeyValuePair<string, RotateBackupChange>>();
                foreach (string path in selectedPaths)
                {
                    string entryId = Guid.NewGuid().ToString("N");
                    string backupPath = ChangeLogStore.CreateBackupPath(entryId, path);
                    File.Copy(path, backupPath, true);
                    logs.Add(new KeyValuePair<string, RotateBackupChange>(entryId, new RotateBackupChange { FilePath = path, BackupPath = backupPath }));
                }
                ImageFileOperations.RotateFiles(selectedPaths, rotateFlipType);
                foreach (KeyValuePair<string, RotateBackupChange> log in logs)
                {
                    AppendChangeLog(ChangeLogKind.Rotate, summary + " " + Path.GetFileName(log.Value.FilePath), log.Value, log.Key);
                }
                UpdateGridItems(selectedPaths);
            }, "Rotated.");
        }

        private IEnumerable<string> ApplySortOrder(IEnumerable<string> filePaths)
        {
            switch (_currentSession.SortOrder)
            {
                case SessionSortOrder.FileNameDescending:
                    return filePaths.OrderByDescending(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
                case SessionSortOrder.ModifiedDateAscending:
                    return filePaths.OrderBy(path => File.GetLastWriteTime(path));
                case SessionSortOrder.ModifiedDateDescending:
                    return filePaths.OrderByDescending(path => File.GetLastWriteTime(path));
                case SessionSortOrder.FileSizeAscending:
                    return filePaths.OrderBy(path => new FileInfo(path).Length);
                case SessionSortOrder.FileSizeDescending:
                    return filePaths.OrderByDescending(path => new FileInfo(path).Length);
                default:
                    return filePaths.OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase);
            }
        }

        private void ResortGrid()
        {
            _grid.SortItems(ComparePhotoItems);
            UpdateSelectionDetails();
        }

        private int ComparePhotoItems(PhotoItem first, PhotoItem second)
        {
            switch (_currentSession.SortOrder)
            {
                case SessionSortOrder.FileNameDescending:
                    return string.Compare(second.DisplayName, first.DisplayName, StringComparison.OrdinalIgnoreCase);
                case SessionSortOrder.ModifiedDateAscending:
                    return first.LastWriteTime.CompareTo(second.LastWriteTime);
                case SessionSortOrder.ModifiedDateDescending:
                    return second.LastWriteTime.CompareTo(first.LastWriteTime);
                case SessionSortOrder.FileSizeAscending:
                    return first.FileSizeBytes.CompareTo(second.FileSizeBytes);
                case SessionSortOrder.FileSizeDescending:
                    return second.FileSizeBytes.CompareTo(first.FileSizeBytes);
                default:
                    return string.Compare(first.DisplayName, second.DisplayName, StringComparison.OrdinalIgnoreCase);
            }
        }

        private string BuildTransferStatus(string verb, FileTransferResult result)
        {
            if (result.Cancelled) return verb + " stopped after " + result.DestinationPathsWritten.Count + " file(s).";
            if (result.SkippedPaths.Count > 0) return verb + " " + result.DestinationPathsWritten.Count + " file(s), skipped " + result.SkippedPaths.Count + ".";
            return verb + " " + result.DestinationPathsWritten.Count + " file(s).";
        }

        private string GetDestinationMenuDisplayPath(string destinationPath)
        {
            foreach (string sourceFolder in _currentSession.GetSourceFolders())
            {
                string source = EnsureTrailingSeparator(Path.GetFullPath(sourceFolder));
                string destination = Path.GetFullPath(destinationPath);
                if (!destination.StartsWith(source, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                string relative = destination.Substring(source.Length).TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
                return relative.Length == 0 ? "." : ".\\" + relative;
            }

            return destinationPath;
        }

        private void ShowHelpWindow()
        {
            using (HelpViewerForm helpForm = new HelpViewerForm())
            {
                helpForm.ShowDialog(this);
            }
        }

        private void PersistLastBrowsedFolder(string folderPath)
        {
            if (string.IsNullOrWhiteSpace(folderPath) || !Directory.Exists(folderPath))
            {
                return;
            }

            if (string.Equals(_appConfig.LastBrowsedFolder, folderPath, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            _appConfig.LastBrowsedFolder = folderPath;
            SessionConfigStore.Save(_appConfig);
            _appConfig = SessionConfigStore.Load();
        }

        private string GetPreferredBrowseFolder()
        {
            string currentSource = _currentSession.GetSourceFolders().FirstOrDefault(Directory.Exists);
            if (!string.IsNullOrWhiteSpace(currentSource))
            {
                return currentSource;
            }

            string currentDestination = _currentSession.DestinationFolders.FirstOrDefault(Directory.Exists);
            if (!string.IsNullOrWhiteSpace(currentDestination))
            {
                return currentDestination;
            }

            return _appConfig.LastBrowsedFolder;
        }

        private static bool AreSourceFoldersEqual(IEnumerable<string> left, IEnumerable<string> right)
        {
            List<string> leftList = (left ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            List<string> rightList = (right ?? Enumerable.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                .ToList();
            return leftList.SequenceEqual(rightList, StringComparer.OrdinalIgnoreCase);
        }

        private void PromptToDeleteEmptySourceFolders(IEnumerable<string> removedFilePaths)
        {
            List<string> sourceFolders = _currentSession.GetSourceFolders();
            List<string> affectedRoots = sourceFolders
                .Where(root => removedFilePaths.Any(path => IsPathInSourceRoot(path, root)))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (string root in affectedRoots)
            {
                if (!Directory.Exists(root))
                {
                    continue;
                }

                bool hasFiles = Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Any();
                if (hasFiles)
                {
                    continue;
                }

                if (MessageBox.Show(this, "The source folder is now empty. Delete it?" + Environment.NewLine + Environment.NewLine + root, "Delete Empty Source Folder", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                {
                    continue;
                }

                OrganizerSession previous = _currentSession.Clone();
                Directory.Delete(root, true);
                _currentSession.SourceFolders = _currentSession.GetSourceFolders().Where(path => !string.Equals(path, root, StringComparison.OrdinalIgnoreCase)).ToList();
                _currentSession.SourceFolder = _currentSession.SourceFolders.FirstOrDefault() ?? string.Empty;
                _sessionDirty = true;
                AppendSessionSettingsLog(previous, _currentSession);
                RefreshSessionSummary();
                SetStatus("Deleted empty source folder " + root);
            }
        }

        private static bool IsPathInSourceRoot(string filePath, string sourceRoot)
        {
            if (string.IsNullOrWhiteSpace(filePath) || string.IsNullOrWhiteSpace(sourceRoot))
            {
                return false;
            }

            string directory = Path.GetDirectoryName(Path.GetFullPath(filePath)) ?? string.Empty;
            string root = EnsureTrailingSeparator(Path.GetFullPath(sourceRoot));
            string fullDirectory = EnsureTrailingSeparator(directory);
            return fullDirectory.StartsWith(root, StringComparison.OrdinalIgnoreCase);
        }

        private void AppendSessionSettingsLog(OrganizerSession previousSession, OrganizerSession updatedSession)
        {
            if (previousSession == null || updatedSession == null)
            {
                return;
            }

            string previousJson = ChangeLogStore.Serialize(previousSession);
            string updatedJson = ChangeLogStore.Serialize(updatedSession);
            if (string.Equals(previousJson, updatedJson, StringComparison.Ordinal))
            {
                return;
            }

            AppendChangeLog(ChangeLogKind.SessionSettings, "Updated session settings.", new SessionSettingsChangePayload
            {
                PreviousSession = previousSession.Clone(),
                UpdatedSession = updatedSession.Clone()
            });
        }

        private void AppendChangeLog<T>(ChangeLogKind kind, string summary, T payload, string entryId = null)
        {
            ChangeLogStore.Append(new ChangeLogEntry
            {
                EntryId = string.IsNullOrWhiteSpace(entryId) ? Guid.NewGuid().ToString("N") : entryId,
                SessionId = _currentSession.SessionId,
                SessionName = _currentSession.Name,
                TimestampUtc = DateTime.UtcNow,
                Kind = kind,
                Summary = summary,
                PayloadJson = payload == null ? string.Empty : ChangeLogStore.Serialize(payload)
            });
        }

        private void ShowUndoWindow()
        {
            List<ChangeLogEntry> entries = ChangeLogStore.LoadForSession(_currentSession.SessionId);
            if (entries.Count == 0)
            {
                MessageBox.Show(this, "There are no logged changes for the current session.", "Undo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            List<ChangeLogEntry> selectedEntries;
            UndoWindowAction action = UndoLogForm.ShowWindow(this, entries, out selectedEntries);
            if (selectedEntries.Count == 0 || action == UndoWindowAction.Close)
            {
                return;
            }

            if (action == UndoWindowAction.ClearSelected)
            {
                ClearLogEntries(selectedEntries);
                SetStatus("Cleared " + selectedEntries.Count + " undo entry(s).");
                return;
            }

            List<ChangeLogEntry> succeeded = new List<ChangeLogEntry>();
            List<string> failures = new List<string>();
            foreach (ChangeLogEntry entry in selectedEntries.OrderByDescending(item => item.TimestampUtc))
            {
                try
                {
                    RollbackEntry(entry);
                    succeeded.Add(entry);
                }
                catch (Exception ex)
                {
                    failures.Add(entry.Summary + ": " + ex.Message);
                }
            }

            if (succeeded.Count > 0)
            {
                ClearLogEntries(succeeded);
                SetStatus("Undid " + succeeded.Count + " change(s).");
            }

            if (failures.Count > 0)
            {
                MessageBox.Show(this, string.Join(Environment.NewLine, failures.ToArray()), "Undo Issues", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
        }

        private void ClearLogEntries(IEnumerable<ChangeLogEntry> entries)
        {
            List<ChangeLogEntry> list = entries == null ? new List<ChangeLogEntry>() : entries.ToList();
            ChangeLogStore.RemoveEntries(list.Select(item => item.EntryId));
            foreach (ChangeLogEntry entry in list)
            {
                ChangeLogStore.DeleteBackupFolder(entry.EntryId);
            }
        }

        private void RollbackEntry(ChangeLogEntry entry)
        {
            switch (entry.Kind)
            {
                case ChangeLogKind.Copy:
                    RollbackCopy(GetPathChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.Move:
                    RollbackMove(GetPathChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.DateUpdate:
                    RollbackDateUpdate(GetDateUpdateChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.Rename:
                    RollbackRename(GetPathChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.Convert:
                    RollbackConvert(GetConvertedPaths(entry.PayloadJson));
                    break;
                case ChangeLogKind.Rotate:
                    RollbackRotate(GetRotateChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.Delete:
                    RollbackDelete(GetDeleteChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.Rating:
                    RollbackRating(GetRatingChanges(entry.PayloadJson));
                    break;
                case ChangeLogKind.SessionSettings:
                    RollbackSessionSettings(ChangeLogStore.Deserialize<SessionSettingsChangePayload>(entry.PayloadJson));
                    break;
            }
        }

        private void RollbackCopy(List<PathPairChange> paths)
        {
            foreach (PathPairChange path in paths)
            {
                if (File.Exists(path.DestinationPath))
                {
                    File.Delete(path.DestinationPath);
                    _grid.RemoveItems(new[] { path.DestinationPath });
                }
            }

            UpdateSelectionDetails();
        }

        private void RollbackMove(List<PathPairChange> paths)
        {
            foreach (PathPairChange path in paths)
            {
                if (!File.Exists(path.DestinationPath))
                {
                    continue;
                }

                if (File.Exists(path.SourcePath))
                {
                    throw new InvalidOperationException("Cannot restore moved file because the original path is already occupied: " + path.SourcePath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path.SourcePath));
                File.Move(path.DestinationPath, path.SourcePath);
                _grid.RemoveItems(new[] { path.DestinationPath });
                AddGridItems(new[] { path.SourcePath });
            }

            UpdateSelectionDetails();
        }

        private void RollbackDateUpdate(List<DateUpdateFileChange> files)
        {
            foreach (DateUpdateFileChange file in files)
            {
                if (File.Exists(file.FilePath))
                {
                    PhotoMetadataHelper.UpdateDateTakenAndModified(file.FilePath, file.OldDate);
                    UpdateGridItem(file.FilePath);
                }
            }
        }

        private void RollbackRename(List<PathPairChange> paths)
        {
            foreach (PathPairChange path in paths)
            {
                if (!File.Exists(path.DestinationPath))
                {
                    continue;
                }

                if (File.Exists(path.SourcePath))
                {
                    throw new InvalidOperationException("Cannot restore renamed file because the original path is already occupied: " + path.SourcePath);
                }

                Directory.CreateDirectory(Path.GetDirectoryName(path.SourcePath));
                File.Move(path.DestinationPath, path.SourcePath);
                ReplaceGridItem(path.DestinationPath, path.SourcePath);
            }
        }

        private void RollbackConvert(List<string> createdPaths)
        {
            foreach (string filePath in createdPaths)
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                }
            }

            _grid.RemoveItems(createdPaths);
            UpdateSelectionDetails();
        }

        private void RollbackRotate(List<RotateBackupChange> files)
        {
            foreach (RotateBackupChange file in files)
            {
                if (File.Exists(file.BackupPath))
                {
                    File.Copy(file.BackupPath, file.FilePath, true);
                    UpdateGridItem(file.FilePath);
                }
            }
        }

        private void RollbackDelete(List<DeleteBackupChange> files)
        {
            foreach (DeleteBackupChange file in files)
            {
                if (!File.Exists(file.BackupPath))
                {
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(file.OriginalPath));
                if (File.Exists(file.OriginalPath))
                {
                    throw new InvalidOperationException("Cannot restore deleted file because the original path is already occupied: " + file.OriginalPath);
                }

                File.Copy(file.BackupPath, file.OriginalPath, true);
                AddGridItems(new[] { file.OriginalPath });
            }

            UpdateSelectionDetails();
        }

        private void RollbackRating(List<RatingFileChange> files)
        {
            foreach (RatingFileChange file in files)
            {
                if (!File.Exists(file.FilePath) || !PhotoMetadataHelper.SupportsShellRating(file.FilePath))
                {
                    continue;
                }

                ShellRatingHelper.WriteRating(file.FilePath, file.OldRatingHasValue ? (int?)file.OldRating : null);
                UpdateGridItem(file.FilePath);
            }
        }

        private void RollbackSessionSettings(SessionSettingsChangePayload payload)
        {
            if (payload == null || payload.PreviousSession == null)
            {
                return;
            }

            OrganizerSession previous = payload.PreviousSession.Clone();
            SessionConfigStore.SaveSession(previous);
            _appConfig = SessionConfigStore.Load();
            if (string.Equals(_currentSession.SessionId, previous.SessionId, StringComparison.OrdinalIgnoreCase))
            {
                bool requiresRefresh = !AreSourceFoldersEqual(_currentSession.GetSourceFolders(), previous.GetSourceFolders())
                    || _currentSession.RecurseSubdirectories != previous.RecurseSubdirectories;
                bool highlightChanged = _currentSession.HighlightDateDifferences != previous.HighlightDateDifferences;
                _currentSession = previous;
                ApplySessionDisplaySettings();
                RefreshSessionSummary();
                if (requiresRefresh)
                {
                    RefreshPhotos();
                }
                else
                {
                    ResortGrid();
                    if (highlightChanged)
                    {
                        _grid.Invalidate();
                    }
                    UpdateSelectionDetails();
                }
            }
        }

        private List<PathPairChange> GetPathChanges(string payloadJson)
        {
            PathPairChange single;
            if (ChangeLogStore.TryDeserialize<PathPairChange>(payloadJson, out single))
            {
                return new List<PathPairChange> { single };
            }

            CopyMoveChangePayload movePayload;
            if (ChangeLogStore.TryDeserialize<CopyMoveChangePayload>(payloadJson, out movePayload))
            {
                return movePayload.Paths ?? new List<PathPairChange>();
            }

            RenameChangePayload renamePayload;
            return ChangeLogStore.TryDeserialize<RenameChangePayload>(payloadJson, out renamePayload)
                ? renamePayload.Paths ?? new List<PathPairChange>()
                : new List<PathPairChange>();
        }

        private List<DateUpdateFileChange> GetDateUpdateChanges(string payloadJson)
        {
            DateUpdateFileChange single;
            if (ChangeLogStore.TryDeserialize<DateUpdateFileChange>(payloadJson, out single))
            {
                return new List<DateUpdateFileChange> { single };
            }

            DateUpdateChangePayload payload;
            return ChangeLogStore.TryDeserialize<DateUpdateChangePayload>(payloadJson, out payload)
                ? payload.Files ?? new List<DateUpdateFileChange>()
                : new List<DateUpdateFileChange>();
        }

        private List<string> GetConvertedPaths(string payloadJson)
        {
            ConvertFileChange single;
            if (ChangeLogStore.TryDeserialize<ConvertFileChange>(payloadJson, out single))
            {
                return new List<string> { single.CreatedPath };
            }

            ConvertChangePayload payload;
            return ChangeLogStore.TryDeserialize<ConvertChangePayload>(payloadJson, out payload)
                ? payload.CreatedPaths ?? new List<string>()
                : new List<string>();
        }

        private List<RotateBackupChange> GetRotateChanges(string payloadJson)
        {
            RotateBackupChange single;
            if (ChangeLogStore.TryDeserialize<RotateBackupChange>(payloadJson, out single))
            {
                return new List<RotateBackupChange> { single };
            }

            RotateChangePayload payload;
            return ChangeLogStore.TryDeserialize<RotateChangePayload>(payloadJson, out payload)
                ? payload.Files ?? new List<RotateBackupChange>()
                : new List<RotateBackupChange>();
        }

        private List<DeleteBackupChange> GetDeleteChanges(string payloadJson)
        {
            DeleteBackupChange single;
            if (ChangeLogStore.TryDeserialize<DeleteBackupChange>(payloadJson, out single))
            {
                return new List<DeleteBackupChange> { single };
            }

            DeleteChangePayload payload;
            return ChangeLogStore.TryDeserialize<DeleteChangePayload>(payloadJson, out payload)
                ? payload.Files ?? new List<DeleteBackupChange>()
                : new List<DeleteBackupChange>();
        }

        private List<RatingFileChange> GetRatingChanges(string payloadJson)
        {
            RatingFileChange single;
            if (ChangeLogStore.TryDeserialize<RatingFileChange>(payloadJson, out single))
            {
                return new List<RatingFileChange> { single };
            }

            RatingChangePayload payload;
            return ChangeLogStore.TryDeserialize<RatingChangePayload>(payloadJson, out payload)
                ? payload.Files ?? new List<RatingFileChange>()
                : new List<RatingFileChange>();
        }

        private Control CreateHeaderPair(string title, out Label valueLabel, int height = 60)
        {
            Panel panel = new Panel { Width = 320, Height = height, Margin = new Padding(0, 0, 0, 6) };
            Label titleLabel = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Location = new Point(0, 0), Text = title };
            valueLabel = new Label { Location = new Point(0, 22), Size = new Size(320, height - 22), AutoSize = false };
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(valueLabel);
            return panel;
        }

        private Control CreateTextBoxPair(string title, out TextBox valueTextBox, int height)
        {
            Panel panel = new Panel { Width = 320, Height = height, Margin = new Padding(0, 0, 0, 6) };
            Label titleLabel = new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Location = new Point(0, 0), Text = title };
            valueTextBox = new TextBox
            {
                Location = new Point(0, 22),
                Size = new Size(320, height - 22),
                Multiline = true,
                ReadOnly = true,
                ScrollBars = ScrollBars.Vertical,
                BorderStyle = BorderStyle.FixedSingle
            };
            panel.Controls.Add(titleLabel);
            panel.Controls.Add(valueTextBox);
            return panel;
        }

        private Control CreateDivider() { return new Panel { BackColor = Color.Silver, Width = 320, Height = 1, Margin = new Padding(0, 8, 0, 8) }; }

        private Control CreateValueRow(string title, int top, out Label valueLabel)
        {
            Panel panel = new Panel { Location = new Point(0, top), Size = new Size(320, 38) };
            panel.Controls.Add(new Label { AutoSize = true, Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold), Location = new Point(0, 0), Text = title });
            valueLabel = new Label { Location = new Point(0, 18), Size = new Size(320, 18) };
            panel.Controls.Add(valueLabel);
            return panel;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " bytes";
            if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.0") + " KB";
            return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
        }

        private void UpdateDetailLayout()
        {
            int width = Math.Max(220, _detailPanel.ClientSize.Width - 24);
            foreach (Control child in _detailFlow.Controls)
            {
                child.Width = width;
                Panel panel = child as Panel;
                if (panel != null)
                {
                    foreach (Control nested in panel.Controls)
                    {
                        Label label = nested as Label;
                        if (label != null && !label.AutoSize)
                        {
                            label.Width = width;
                            Size measured = TextRenderer.MeasureText(label.Text + " ", label.Font, new Size(width, 0), TextFormatFlags.WordBreak);
                            if (label.Height != measured.Height + 4)
                            {
                                label.Height = measured.Height + 4;
                            }
                        }

                        TextBox textBox = nested as TextBox;
                        if (textBox != null)
                        {
                            textBox.Width = width;
                            int lineCount = Math.Max(3, textBox.Lines.Length);
                            int preferredHeight = Math.Min(180, Math.Max(72, 24 + (lineCount * textBox.Font.Height)));
                            if (textBox.Height != preferredHeight)
                            {
                                textBox.Height = preferredHeight;
                            }
                        }
                    }

                    int bottom = 0;
                    foreach (Control nested in panel.Controls)
                    {
                        bottom = Math.Max(bottom, nested.Bottom);
                    }

                    if (bottom > 0)
                    {
                        panel.Height = bottom + 4;
                    }
                }

                if (ReferenceEquals(child, _actionsPanel))
                {
                    _actionsToolStrip.Width = width;
                    foreach (ToolStripItem item in _actionsToolStrip.Items)
                    {
                        item.AutoSize = false;
                        item.Width = Math.Max(180, width - 8);
                    }

                    int itemsHeight = Math.Max(30, _actionsToolStrip.Items.Count * 28);
                    _actionsToolStrip.Height = Math.Min(180, itemsHeight);
                    _actionsPanel.Height = _actionsToolStrip.Bottom + 4;
                }
            }
        }

        private void ApplySplitRatioFromSession()
        {
            if (_currentSession == null || _mainSplitContainer == null || _mainSplitContainer.ClientSize.Width <= 0)
            {
                return;
            }

            int totalWidth = _mainSplitContainer.ClientSize.Width;
            int panel2Width = Math.Max(1, (int)Math.Round(totalWidth * (_currentSession.InfoPanePercent / 100d)));
            panel2Width = Math.Max(1, Math.Min((int)Math.Round(totalWidth * 0.50), panel2Width));
            int splitterDistance = Math.Max((int)Math.Round(totalWidth * 0.50), totalWidth - panel2Width);
            splitterDistance = Math.Min(totalWidth - 1, splitterDistance);
            if (splitterDistance > 0 && splitterDistance < totalWidth && _mainSplitContainer.SplitterDistance != splitterDistance)
            {
                _mainSplitContainer.SplitterDistance = splitterDistance;
            }
        }

        private void SaveCurrentSplitRatio()
        {
            if (_suppressSessionDirtyTracking || _currentSession == null || _mainSplitContainer == null || _mainSplitContainer.ClientSize.Width <= 0)
            {
                return;
            }

            int totalWidth = Math.Max(1, _mainSplitContainer.ClientSize.Width);
            int panel2Width = totalWidth - _mainSplitContainer.SplitterDistance;
            int percent = (int)Math.Round((panel2Width * 100d) / totalWidth);
            int normalizedPercent = Math.Max(1, Math.Min(50, percent));
            if (_currentSession.InfoPanePercent == normalizedPercent)
            {
                return;
            }

            _currentSession.InfoPanePercent = normalizedPercent;
            _sessionDirty = true;
            RefreshSessionSummary();
        }

        private static string EnsureTrailingSeparator(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return string.Empty;
            }

            if (path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                || path.EndsWith(Path.AltDirectorySeparatorChar.ToString(), StringComparison.Ordinal))
            {
                return path;
            }

            return path + Path.DirectorySeparatorChar;
        }
    }
}
