using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class FullscreenViewerForm : Form
    {
        private readonly List<string> _paths;
        private readonly PictureBox _pictureBox;
        private int _currentIndex;

        public FullscreenViewerForm(List<string> paths, int currentIndex)
        {
            _paths = paths;
            _currentIndex = Math.Max(0, Math.Min(currentIndex, _paths.Count - 1));
            BackColor = Color.Black;
            WindowState = FormWindowState.Maximized;
            KeyPreview = true;
            Text = "Fullscreen";

            _pictureBox = new PictureBox();
            _pictureBox.Dock = DockStyle.Fill;
            _pictureBox.SizeMode = PictureBoxSizeMode.Zoom;
            Controls.Add(_pictureBox);

            Load += FullscreenViewerForm_Load;
            KeyDown += FullscreenViewerForm_KeyDown;
            FormClosed += FullscreenViewerForm_FormClosed;
        }

        private void FullscreenViewerForm_Load(object sender, EventArgs e)
        {
            ShowCurrentImage();
        }

        private void FullscreenViewerForm_FormClosed(object sender, FormClosedEventArgs e)
        {
            if (_pictureBox.Image != null)
            {
                _pictureBox.Image.Dispose();
                _pictureBox.Image = null;
            }
        }

        private void FullscreenViewerForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Escape)
            {
                Close();
                return;
            }

            if (e.KeyCode == Keys.Right)
            {
                _currentIndex = (_currentIndex + 1) % _paths.Count;
                ShowCurrentImage();
                return;
            }

            if (e.KeyCode == Keys.Left)
            {
                _currentIndex = (_currentIndex + _paths.Count - 1) % _paths.Count;
                ShowCurrentImage();
            }
        }

        private void ShowCurrentImage()
        {
            if (_pictureBox.Image != null)
            {
                _pictureBox.Image.Dispose();
                _pictureBox.Image = null;
            }

            string path = _paths[_currentIndex];
            using (Image image = PhotoMetadataHelper.LoadPreviewImage(path))
            {
                _pictureBox.Image = new Bitmap(image);
            }

            Text = "Fullscreen - " + Path.GetFileName(path);
        }
    }
}
