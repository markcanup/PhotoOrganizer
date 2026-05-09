using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal enum ConflictResolutionOption
    {
        Rename,
        Overwrite,
        Skip
    }

    internal enum ConflictFollowUpOption
    {
        AskEach,
        ApplyToAll,
        CancelOperation
    }

    internal sealed class ConflictResolutionChoice
    {
        public ConflictResolutionOption Resolution { get; set; }
        public ConflictFollowUpOption FollowUp { get; set; }
    }

    internal sealed class ConflictResolutionForm : Form
    {
        private readonly RadioButton _renameRadioButton;
        private readonly RadioButton _overwriteRadioButton;
        private readonly RadioButton _skipRadioButton;
        private readonly RadioButton _askEachRadioButton;
        private readonly RadioButton _applyToAllRadioButton;
        private readonly RadioButton _cancelRadioButton;

        private ConflictResolutionForm(string sourcePath, string destinationPath)
        {
            Text = "File Name Conflict";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MinimizeBox = false;
            MaximizeBox = false;
            ClientSize = new Size(540, 380);

            Label messageLabel = new Label();
            messageLabel.Location = new Point(16, 16);
            messageLabel.AutoSize = false;
            messageLabel.Size = new Size(500, 110);
            messageLabel.Text = "A file with this name already exists in the destination folder.\r\nSource: " + Path.GetFileName(sourcePath) + "\r\nDestination: " + destinationPath;
            Size measuredMessage = TextRenderer.MeasureText(messageLabel.Text + " ", messageLabel.Font, new Size(messageLabel.Width, 0), TextFormatFlags.WordBreak);
            messageLabel.Height = Math.Max(110, measuredMessage.Height + 8);

            GroupBox resolutionGroup = new GroupBox();
            resolutionGroup.Text = "Resolution approach";
            resolutionGroup.Location = new Point(16, messageLabel.Bottom + 12);
            resolutionGroup.Size = new Size(240, 122);

            _renameRadioButton = new RadioButton();
            _renameRadioButton.Location = new Point(16, 28);
            _renameRadioButton.Size = new Size(180, 20);
            _renameRadioButton.Text = "Rename";
            _renameRadioButton.Checked = true;

            _overwriteRadioButton = new RadioButton();
            _overwriteRadioButton.Location = new Point(16, 56);
            _overwriteRadioButton.Size = new Size(180, 20);
            _overwriteRadioButton.Text = "Overwrite";

            _skipRadioButton = new RadioButton();
            _skipRadioButton.Location = new Point(16, 84);
            _skipRadioButton.Size = new Size(180, 20);
            _skipRadioButton.Text = "Skip";

            resolutionGroup.Controls.Add(_renameRadioButton);
            resolutionGroup.Controls.Add(_overwriteRadioButton);
            resolutionGroup.Controls.Add(_skipRadioButton);

            GroupBox nextGroup = new GroupBox();
            nextGroup.Text = "What to do next";
            nextGroup.Location = new Point(276, messageLabel.Bottom + 12);
            nextGroup.Size = new Size(240, 122);

            _askEachRadioButton = new RadioButton();
            _askEachRadioButton.Location = new Point(16, 28);
            _askEachRadioButton.Size = new Size(180, 20);
            _askEachRadioButton.Text = "Ask for each conflict";
            _askEachRadioButton.Checked = true;

            _applyToAllRadioButton = new RadioButton();
            _applyToAllRadioButton.Location = new Point(16, 56);
            _applyToAllRadioButton.Size = new Size(180, 20);
            _applyToAllRadioButton.Text = "Do this for all conflicts";

            _cancelRadioButton = new RadioButton();
            _cancelRadioButton.Location = new Point(16, 84);
            _cancelRadioButton.Size = new Size(180, 20);
            _cancelRadioButton.Text = "Cancel";

            nextGroup.Controls.Add(_askEachRadioButton);
            nextGroup.Controls.Add(_applyToAllRadioButton);
            nextGroup.Controls.Add(_cancelRadioButton);

            Label noteLabel = new Label();
            noteLabel.Location = new Point(16, resolutionGroup.Bottom + 10);
            noteLabel.Size = new Size(500, 34);
            noteLabel.Text = "Rename adds a number in parentheses after the base file name until the destination file name is unique.";

            Button okButton = new Button();
            okButton.Text = "OK";
            okButton.Location = new Point(354, noteLabel.Bottom + 12);
            okButton.DialogResult = DialogResult.OK;

            Button cancelButton = new Button();
            cancelButton.Text = "Cancel";
            cancelButton.Location = new Point(435, noteLabel.Bottom + 12);
            cancelButton.DialogResult = DialogResult.Cancel;

            Controls.Add(messageLabel);
            Controls.Add(resolutionGroup);
            Controls.Add(nextGroup);
            Controls.Add(noteLabel);
            Controls.Add(okButton);
            Controls.Add(cancelButton);

            AcceptButton = okButton;
            CancelButton = cancelButton;
        }

        public ConflictResolutionChoice Choice
        {
            get
            {
                return new ConflictResolutionChoice
                {
                    Resolution = _overwriteRadioButton.Checked
                        ? ConflictResolutionOption.Overwrite
                        : _skipRadioButton.Checked
                            ? ConflictResolutionOption.Skip
                            : ConflictResolutionOption.Rename,
                    FollowUp = _applyToAllRadioButton.Checked
                        ? ConflictFollowUpOption.ApplyToAll
                        : _cancelRadioButton.Checked
                            ? ConflictFollowUpOption.CancelOperation
                            : ConflictFollowUpOption.AskEach
                };
            }
        }

        public static bool TryResolve(IWin32Window owner, string sourcePath, string destinationPath, out ConflictResolutionChoice choice)
        {
            using (ConflictResolutionForm form = new ConflictResolutionForm(sourcePath, destinationPath))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    choice = form.Choice;
                    return true;
                }
            }

            choice = null;
            return false;
        }
    }
}
