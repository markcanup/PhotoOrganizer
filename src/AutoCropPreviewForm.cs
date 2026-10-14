using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class AutoCropPreviewForm : Form
    {
        private readonly Image _original, _cropped;

        public AutoCropPreviewForm(PreparedImageEdit edit)
        {
            Text = "Autocrop preview - " + Path.GetFileName(edit.SourcePath);
            StartPosition = FormStartPosition.CenterParent;
            ClientSize = new Size(1000, 650);
            MinimumSize = new Size(650, 400);
            _original = PhotoMetadataHelper.LoadBitmap(edit.SourcePath);
            try { _cropped = PhotoMetadataHelper.LoadBitmap(edit.PreparedPath); }
            catch { _original.Dispose(); throw; }

            TableLayoutPanel images = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
            images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            images.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            images.Controls.Add(CreatePanel("Original", _original), 0, 0);
            images.Controls.Add(CreatePanel("Proposed crop", _cropped), 1, 0);
            Controls.Add(images);
            FlowLayoutPanel buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 48,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(8) };
            Button cancel = new Button { Text = "Cancel batch", AutoSize = true, DialogResult = DialogResult.Cancel };
            Button skip = new Button { Text = "Skip file", AutoSize = true, DialogResult = DialogResult.No };
            Button apply = new Button { Text = "Apply crop", AutoSize = true, DialogResult = DialogResult.OK };
            buttons.Controls.Add(cancel); buttons.Controls.Add(skip); buttons.Controls.Add(apply);
            Controls.Add(buttons);
            AcceptButton = apply; CancelButton = cancel;
        }

        private static Panel CreatePanel(string title, Image image)
        {
            Panel panel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8) };
            panel.Controls.Add(new PictureBox { Dock = DockStyle.Fill, Image = image, SizeMode = PictureBoxSizeMode.Zoom, BackColor = Color.Black });
            panel.Controls.Add(new Label { Dock = DockStyle.Top, Height = 30, TextAlign = ContentAlignment.MiddleCenter,
                Text = title + "  " + image.Width + " x " + image.Height });
            return panel;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) { if (_original != null) _original.Dispose(); if (_cropped != null) _cropped.Dispose(); }
            base.Dispose(disposing);
        }
    }
}
