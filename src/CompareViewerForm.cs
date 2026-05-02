using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class CompareViewerForm : Form
    {
        private readonly string _leftPath;
        private readonly string _rightPath;
        private readonly PictureBox _leftPictureBox;
        private readonly PictureBox _rightPictureBox;

        public CompareViewerForm(string leftPath, string rightPath)
        {
            _leftPath = leftPath;
            _rightPath = rightPath;
            BackColor = Color.Black;
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            Text = "Compare";

            TableLayoutPanel layout = new TableLayoutPanel();
            layout.Dock = DockStyle.Fill;
            layout.ColumnCount = 2;
            layout.RowCount = 1;
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            layout.Controls.Add(CreateViewerPanel(_leftPath, out _leftPictureBox), 0, 0);
            layout.Controls.Add(CreateViewerPanel(_rightPath, out _rightPictureBox), 1, 0);
            Controls.Add(layout);

            Load += CompareViewerForm_Load;
            FormClosed += CompareViewerForm_FormClosed;
            KeyDown += CompareViewerForm_KeyDown;
        }

        private void CompareViewerForm_Load(object sender, EventArgs e)
        {
            _leftPictureBox.Image = LoadBitmap(_leftPath);
            _rightPictureBox.Image = LoadBitmap(_rightPath);
        }

        private void CompareViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            DisposeImage(_leftPictureBox);
            DisposeImage(_rightPictureBox);
        }

        private void CompareViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
            }
        }

        private static Panel CreateViewerPanel(string path, out PictureBox pictureBox)
        {
            Panel panel = new Panel();
            panel.Dock = DockStyle.Fill;
            panel.BackColor = Color.Black;

            pictureBox = new PictureBox();
            pictureBox.Dock = DockStyle.Fill;
            pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            pictureBox.BackColor = Color.Black;
            panel.Controls.Add(pictureBox);

            Label detailsLabel = new Label();
            detailsLabel.Dock = DockStyle.Bottom;
            detailsLabel.Height = 58;
            detailsLabel.TextAlign = ContentAlignment.MiddleCenter;
            detailsLabel.ForeColor = Color.White;
            detailsLabel.BackColor = Color.Black;
            detailsLabel.Text = BuildDetailsText(path);
            panel.Controls.Add(detailsLabel);

            return panel;
        }

        private static Bitmap LoadBitmap(string path)
        {
            using (Image image = PhotoMetadataHelper.LoadPreviewImage(path))
            {
                return new Bitmap(image);
            }
        }

        private static void DisposeImage(PictureBox pictureBox)
        {
            if (pictureBox.Image != null)
            {
                pictureBox.Image.Dispose();
                pictureBox.Image = null;
            }
        }

        private static string BuildDetailsText(string path)
        {
            FileInfo info = new FileInfo(path);
            Size size = PhotoMetadataHelper.GetPixelSize(path);
            return Path.GetFileName(path)
                + Environment.NewLine
                + FormatFileSize(info.Exists ? info.Length : 0L)
                + "    "
                + size.Width + " x " + size.Height;
        }

        private static string FormatFileSize(long bytes)
        {
            if (bytes < 1024) return bytes + " bytes";
            if (bytes < 1024 * 1024) return (bytes / 1024d).ToString("0.0") + " KB";
            return (bytes / (1024d * 1024d)).ToString("0.0") + " MB";
        }
    }
}
