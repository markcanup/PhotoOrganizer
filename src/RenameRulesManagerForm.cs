using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class RenameRulesManagerForm : Form
    {
        private readonly ListBox _rulesListBox;
        private readonly AppConfig _config;

        private RenameRulesManagerForm(AppConfig config)
        {
            _config = config;
            Text = "Renaming Rules";
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ClientSize = new Size(460, 330);

            Label label = new Label();
            label.AutoSize = true;
            label.Location = new Point(16, 16);
            label.Text = "Shared renaming rules";
            Controls.Add(label);

            _rulesListBox = new ListBox();
            _rulesListBox.Location = new Point(16, 42);
            _rulesListBox.Size = new Size(428, 200);
            RefreshList();
            Controls.Add(_rulesListBox);

            Button addButton = new Button();
            addButton.Text = "Add";
            addButton.Location = new Point(126, 256);
            addButton.Click += AddButton_Click;
            Controls.Add(addButton);

            Button editButton = new Button();
            editButton.Text = "Edit";
            editButton.Location = new Point(207, 256);
            editButton.Click += EditButton_Click;
            Controls.Add(editButton);

            Button removeButton = new Button();
            removeButton.Text = "Remove";
            removeButton.Location = new Point(288, 256);
            removeButton.Click += RemoveButton_Click;
            Controls.Add(removeButton);

            Button closeButton = new Button();
            closeButton.Text = "Close";
            closeButton.Location = new Point(369, 292);
            closeButton.DialogResult = DialogResult.OK;
            Controls.Add(closeButton);
            AcceptButton = closeButton;
        }

        public static bool ShowManager(IWin32Window owner)
        {
            AppConfig config = SessionConfigStore.Load();
            using (RenameRulesManagerForm form = new RenameRulesManagerForm(config))
            {
                if (form.ShowDialog(owner) == DialogResult.OK)
                {
                    SessionConfigStore.Save(config);
                    return true;
                }
            }

            return false;
        }

        private void AddButton_Click(object sender, EventArgs e)
        {
            RenameRule rule;
            if (RenameRuleEditorForm.TryEdit(this, new RenameRule(), out rule))
            {
                _config.RenameRules.Add(rule);
                SortRules();
                RefreshList();
            }
        }

        private void EditButton_Click(object sender, EventArgs e)
        {
            RenameRule selected = _rulesListBox.SelectedItem as RenameRule;
            if (selected == null)
            {
                return;
            }

            RenameRule updated;
            if (RenameRuleEditorForm.TryEdit(this, selected.Clone(), out updated))
            {
                int index = _config.RenameRules.FindIndex(item => string.Equals(item.Name, selected.Name, StringComparison.OrdinalIgnoreCase) && item.RuleType == selected.RuleType);
                if (index >= 0)
                {
                    _config.RenameRules[index] = updated;
                    SortRules();
                    RefreshList();
                }
            }
        }

        private void RemoveButton_Click(object sender, EventArgs e)
        {
            RenameRule selected = _rulesListBox.SelectedItem as RenameRule;
            if (selected == null)
            {
                return;
            }

            _config.RenameRules.Remove(selected);
            RefreshList();
        }

        private void SortRules()
        {
            _config.RenameRules = _config.RenameRules.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase).ToList();
        }

        private void RefreshList()
        {
            _rulesListBox.Items.Clear();
            foreach (RenameRule rule in _config.RenameRules.OrderBy(item => item.Name, StringComparer.OrdinalIgnoreCase))
            {
                _rulesListBox.Items.Add(rule);
            }
        }
    }
}
