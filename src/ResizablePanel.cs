using System;
using System.Drawing;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class ResizablePanel : Panel
    {
        private const int EdgeSize = 8;
        private bool _resizingRight;
        private bool _resizingBottom;
        private Point _mouseDownScreen;
        private Size _startingSize;

        public ResizablePanel()
        {
            BorderStyle = BorderStyle.FixedSingle;
            MinimumSize = new Size(250, 250);
            BackColor = Color.White;
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_resizingRight || _resizingBottom)
            {
                Point current = PointToScreen(e.Location);
                int deltaX = current.X - _mouseDownScreen.X;
                int deltaY = current.Y - _mouseDownScreen.Y;
                int newWidth = _resizingRight ? Math.Max(MinimumSize.Width, _startingSize.Width + deltaX) : Width;
                int newHeight = _resizingBottom ? Math.Max(MinimumSize.Height, _startingSize.Height + deltaY) : Height;

                if (Parent != null)
                {
                    newWidth = Math.Min(newWidth, Parent.ClientSize.Width - Left - 12);
                    newHeight = Math.Min(newHeight, Parent.ClientSize.Height - Top - 12);
                }

                Size = new Size(newWidth, newHeight);
                return;
            }

            bool onRight = e.X >= Width - EdgeSize;
            bool onBottom = e.Y >= Height - EdgeSize;

            if (onRight && onBottom)
            {
                Cursor = Cursors.SizeNWSE;
            }
            else if (onRight)
            {
                Cursor = Cursors.SizeWE;
            }
            else if (onBottom)
            {
                Cursor = Cursors.SizeNS;
            }
            else
            {
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            _resizingRight = e.X >= Width - EdgeSize;
            _resizingBottom = e.Y >= Height - EdgeSize;
            if (_resizingRight || _resizingBottom)
            {
                _mouseDownScreen = PointToScreen(e.Location);
                _startingSize = Size;
            }
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _resizingRight = false;
            _resizingBottom = false;
        }
    }
}
