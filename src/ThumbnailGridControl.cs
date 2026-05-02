using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal sealed class ThumbnailGridControl : ScrollableControl
    {
        private const int OuterPadding = 12;
        private const int CellSpacing = 12;
        private const int ResizeGrip = 8;
        private const int TextAreaHeight = 24;
        private readonly List<PhotoItem> _items;
        private readonly HashSet<string> _selectedPaths;
        private int _anchorIndex;
        private int _cellSize;
        private bool _showFileName;
        private bool _highlightDateDifferences;
        private bool _resizingThumbnails;
        private int _resizeStartSize;
        private int _resizeOriginX;
        private bool _forceHorizontalScroll;

        public event EventHandler SelectionChanged;
        public event EventHandler<ItemContextMenuEventArgs> ItemContextMenuRequested;
        public event EventHandler ThumbnailSizeChanged;
        public event EventHandler<ItemDoubleClickEventArgs> ItemDoubleClicked;
        public event EventHandler DeleteRequested;

        public ThumbnailGridControl()
        {
            DoubleBuffered = true;
            AutoScroll = true;
            BackColor = Color.White;
            _items = new List<PhotoItem>();
            _selectedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            _anchorIndex = -1;
            _cellSize = 150;
            SetStyle(ControlStyles.Selectable, true);
            TabStop = true;
        }

        public IList<PhotoItem> Items
        {
            get { return _items; }
        }

        public List<string> SelectedPaths
        {
            get { return _selectedPaths.ToList(); }
        }

        public int SelectedCount
        {
            get { return _selectedPaths.Count; }
        }

        public int ThumbnailSize
        {
            get { return _cellSize; }
            set
            {
                int newValue = Math.Max(80, Math.Min(360, value));
                if (_cellSize == newValue)
                {
                    return;
                }

                _cellSize = newValue;
                UpdateScrollSize();
                Invalidate();
            }
        }

        public bool ShowFileName
        {
            get { return _showFileName; }
            set
            {
                if (_showFileName == value)
                {
                    return;
                }

                _showFileName = value;
                UpdateScrollSize();
                Invalidate();
            }
        }

        public bool HighlightDateDifferences
        {
            get { return _highlightDateDifferences; }
            set
            {
                if (_highlightDateDifferences == value)
                {
                    return;
                }

                _highlightDateDifferences = value;
                Invalidate();
            }
        }

        public void SetItems(IEnumerable<PhotoItem> items)
        {
            DisposeItems(_items);
            _items.Clear();
            _selectedPaths.Clear();
            if (items != null)
            {
                _items.AddRange(items);
            }

            _anchorIndex = -1;
            UpdateScrollSize();
            Invalidate();
            RaiseSelectionChanged();
        }

        public void RemoveItems(IEnumerable<string> filePaths)
        {
            HashSet<string> toRemove = new HashSet<string>(filePaths, StringComparer.OrdinalIgnoreCase);
            List<PhotoItem> removed = _items.Where(item => toRemove.Contains(item.FilePath)).ToList();
            if (removed.Count == 0)
            {
                return;
            }

            foreach (PhotoItem item in removed)
            {
                _items.Remove(item);
                _selectedPaths.Remove(item.FilePath);
                item.Dispose();
            }

            _anchorIndex = -1;
            UpdateScrollSize();
            Invalidate();
            RaiseSelectionChanged();
        }

        public void AddItems(IEnumerable<PhotoItem> items)
        {
            List<PhotoItem> additions = items == null ? new List<PhotoItem>() : items.ToList();
            if (additions.Count == 0)
            {
                return;
            }

            _items.AddRange(additions);
            UpdateScrollSize();
            Invalidate();
        }

        public void ReplaceItem(string existingPath, PhotoItem updatedItem)
        {
            int index = IndexOf(existingPath);
            if (index < 0)
            {
                AddItems(new[] { updatedItem });
                return;
            }

            bool wasSelected = _selectedPaths.Remove(existingPath);
            _items[index].Dispose();
            _items[index] = updatedItem;
            if (wasSelected)
            {
                _selectedPaths.Add(updatedItem.FilePath);
            }

            UpdateScrollSize();
            Invalidate();
            RaiseSelectionChanged();
        }

        public void RefreshItem(string filePath, PhotoItem updatedItem)
        {
            ReplaceItem(filePath, updatedItem);
        }

        public void SortItems(Comparison<PhotoItem> comparison)
        {
            _items.Sort(comparison);
            UpdateScrollSize();
            Invalidate();
        }

        public void SelectSingle(string filePath)
        {
            _selectedPaths.Clear();
            if (!string.IsNullOrEmpty(filePath))
            {
                _selectedPaths.Add(filePath);
                _anchorIndex = _items.FindIndex(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
            }

            Invalidate();
            RaiseSelectionChanged();
        }

        public int IndexOf(string filePath)
        {
            return _items.FindIndex(item => string.Equals(item.FilePath, filePath, StringComparison.OrdinalIgnoreCase));
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateHorizontalScrollMode();
            UpdateScrollSize();
            Invalidate();
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);

            if (_resizingThumbnails)
            {
                int delta = e.X - _resizeOriginX;
                int requested = _resizeStartSize + delta;
                SetThumbnailSizeFromUser(requested);
                return;
            }

            int index = HitTest(e.Location);
            Cursor = index >= 0 && IsOnResizeEdge(index, e.Location)
                ? Cursors.SizeNWSE
                : Cursors.Default;
        }

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();

            int index = HitTest(e.Location);
            if (index < 0)
            {
                if (e.Button == MouseButtons.Left)
                {
                    _selectedPaths.Clear();
                    _anchorIndex = -1;
                    Invalidate();
                    RaiseSelectionChanged();
                }

                return;
            }

            if (e.Button == MouseButtons.Left && IsOnResizeEdge(index, e.Location))
            {
                _resizingThumbnails = true;
                _resizeStartSize = _cellSize;
                _resizeOriginX = e.X;
                Capture = true;
                return;
            }

            PhotoItem item = _items[index];
            bool isSelected = _selectedPaths.Contains(item.FilePath);
            bool shift = (ModifierKeys & Keys.Shift) == Keys.Shift;
            bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;

            if (e.Button == MouseButtons.Right)
            {
                if (!isSelected)
                {
                    _selectedPaths.Clear();
                    _selectedPaths.Add(item.FilePath);
                    _anchorIndex = index;
                    Invalidate();
                    RaiseSelectionChanged();
                }

                if (ItemContextMenuRequested != null)
                {
                    ItemContextMenuRequested(this, new ItemContextMenuEventArgs(index, item.FilePath, e.Location));
                }

                return;
            }

            if (shift && _anchorIndex >= 0)
            {
                _selectedPaths.Clear();
                int start = Math.Min(_anchorIndex, index);
                int end = Math.Max(_anchorIndex, index);
                for (int i = start; i <= end; i++)
                {
                    _selectedPaths.Add(_items[i].FilePath);
                }
            }
            else if (ctrl)
            {
                if (isSelected)
                {
                    _selectedPaths.Remove(item.FilePath);
                }
                else
                {
                    _selectedPaths.Add(item.FilePath);
                }

                _anchorIndex = index;
            }
            else
            {
                _selectedPaths.Clear();
                _selectedPaths.Add(item.FilePath);
                _anchorIndex = index;
            }

            Invalidate();
            RaiseSelectionChanged();
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);

            if (_resizingThumbnails)
            {
                _resizingThumbnails = false;
                Capture = false;
                Cursor = Cursors.Default;
            }
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int index = HitTest(e.Location);
            if (index >= 0 && ItemDoubleClicked != null)
            {
                ItemDoubleClicked(this, new ItemDoubleClickEventArgs(index, _items[index].FilePath));
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            Keys key = keyData & Keys.KeyCode;
            switch (key)
            {
                case Keys.Left:
                case Keys.Right:
                case Keys.Up:
                case Keys.Down:
                case Keys.PageUp:
                case Keys.PageDown:
                case Keys.Home:
                case Keys.End:
                case Keys.Delete:
                    return true;
                default:
                    return base.IsInputKey(keyData);
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            switch (e.KeyCode)
            {
                case Keys.Left:
                    e.Handled = TryMoveSelection(-1);
                    break;
                case Keys.Right:
                    e.Handled = TryMoveSelection(1);
                    break;
                case Keys.Up:
                    e.Handled = TryMoveSelection(-GetColumnCount());
                    break;
                case Keys.Down:
                    e.Handled = TryMoveSelection(GetColumnCount());
                    break;
                case Keys.Home:
                    e.Handled = TrySelectBoundaryItem(true);
                    break;
                case Keys.End:
                    e.Handled = TrySelectBoundaryItem(false);
                    break;
                case Keys.PageUp:
                    ScrollByPage(-1);
                    e.Handled = true;
                    break;
                case Keys.PageDown:
                    ScrollByPage(1);
                    e.Handled = true;
                    break;
                case Keys.Delete:
                    if (SelectedCount > 0 && DeleteRequested != null)
                    {
                        DeleteRequested(this, EventArgs.Empty);
                        e.Handled = true;
                    }
                    break;
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);

            e.Graphics.Clear(Color.White);
            if (_items.Count == 0)
            {
                using (Brush brush = new SolidBrush(Color.FromArgb(90, 90, 90)))
                {
                    e.Graphics.DrawString("No supported images found in the current session source folder.", Font, brush, new PointF(18, 18));
                }

                return;
            }

            Point scroll = AutoScrollPosition;
            int columns = GetColumnCount();
            for (int i = 0; i < _items.Count; i++)
            {
                Rectangle bounds = GetCellBounds(i, columns, scroll);
                if (!bounds.IntersectsWith(ClientRectangle))
                {
                    continue;
                }

                DrawCell(e.Graphics, _items[i], bounds, _selectedPaths.Contains(_items[i].FilePath));
            }
        }

        private void DrawCell(Graphics graphics, PhotoItem item, Rectangle bounds, bool selected)
        {
            Color borderColor = selected ? Color.RoyalBlue : _highlightDateDifferences && item.MetadataLoaded && item.HasDateDifference ? Color.Goldenrod : Color.Gray;
            using (Pen pen = new Pen(borderColor, selected ? 2f : 1f))
            {
                graphics.DrawRectangle(pen, bounds);
            }

            Rectangle imageBounds = GetImageBounds(bounds);
            if (item.Thumbnail != null)
            {
                Size scaled = GetScaledSize(item.Thumbnail.Size, imageBounds.Size);
                Rectangle target = new Rectangle(
                    imageBounds.Left + ((imageBounds.Width - scaled.Width) / 2),
                    imageBounds.Top + ((imageBounds.Height - scaled.Height) / 2),
                    scaled.Width,
                    scaled.Height);
                graphics.DrawImage(item.Thumbnail, target);
            }
            else if (!item.MetadataLoaded)
            {
                TextRenderer.DrawText(
                    graphics,
                    "Loading...",
                    Font,
                    imageBounds,
                    Color.FromArgb(120, 120, 120),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
            }

            if (_showFileName)
            {
                Rectangle textBounds = GetTextBounds(bounds);
                TextRenderer.DrawText(
                    graphics,
                    item.DisplayName,
                    Font,
                    textBounds,
                    Color.FromArgb(55, 55, 55),
                    TextFormatFlags.EndEllipsis | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
            }
        }

        private int HitTest(Point point)
        {
            Point translated = new Point(point.X - AutoScrollPosition.X, point.Y - AutoScrollPosition.Y);
            int columns = GetColumnCount();
            for (int i = 0; i < _items.Count; i++)
            {
                if (GetCellBounds(i, columns, Point.Empty).Contains(translated))
                {
                    return i;
                }
            }

            return -1;
        }

        private Rectangle GetCellBounds(int index, int columns, Point scrollOffset)
        {
            int row = index / columns;
            int column = index % columns;
            int left = OuterPadding + (column * (_cellSize + CellSpacing)) + scrollOffset.X;
            int top = OuterPadding + (row * (GetCellHeight() + CellSpacing)) + scrollOffset.Y;
            return new Rectangle(left, top, _cellSize, GetCellHeight());
        }

        private int GetColumnCount()
        {
            int availableWidth = Math.Max(1, ClientSize.Width - (OuterPadding * 2));
            if (_forceHorizontalScroll)
            {
                return 3;
            }

            return Math.Max(1, (availableWidth + CellSpacing) / (_cellSize + CellSpacing));
        }

        private void UpdateScrollSize()
        {
            int columns = GetColumnCount();
            int rows = _items.Count == 0 ? 1 : (int)Math.Ceiling((double)_items.Count / columns);
            int width = OuterPadding + (columns * (_cellSize + CellSpacing));
            int height = OuterPadding + (rows * (GetCellHeight() + CellSpacing));
            AutoScrollMinSize = new Size(width, height);
        }

        private void SetThumbnailSizeFromUser(int requestedSize)
        {
            int clamped = Math.Max(80, Math.Min(360, requestedSize));
            if (_cellSize == clamped)
            {
                return;
            }

            _cellSize = clamped;
            UpdateHorizontalScrollMode();
            UpdateScrollSize();
            Invalidate();
            if (ThumbnailSizeChanged != null)
            {
                ThumbnailSizeChanged(this, EventArgs.Empty);
            }
        }

        private bool IsOnResizeEdge(int index, Point point)
        {
            Rectangle bounds = GetCellBounds(index, GetColumnCount(), AutoScrollPosition);
            Rectangle rightEdge = new Rectangle(bounds.Right - ResizeGrip, bounds.Top, ResizeGrip, bounds.Height);
            Rectangle bottomEdge = new Rectangle(bounds.Left, bounds.Bottom - ResizeGrip, bounds.Width, ResizeGrip);
            return rightEdge.Contains(point) || bottomEdge.Contains(point);
        }

        private int GetCellHeight()
        {
            return _cellSize + (_showFileName ? TextAreaHeight : 0);
        }

        private Rectangle GetImageBounds(Rectangle bounds)
        {
            int imageHeight = bounds.Height - (_showFileName ? TextAreaHeight : 0);
            Rectangle imageArea = new Rectangle(bounds.Left, bounds.Top, bounds.Width, imageHeight);
            return Rectangle.Inflate(imageArea, -10, -10);
        }

        private Rectangle GetTextBounds(Rectangle bounds)
        {
            return new Rectangle(bounds.Left + 4, bounds.Bottom - TextAreaHeight, bounds.Width - 8, TextAreaHeight - 2);
        }

        private void RaiseSelectionChanged()
        {
            if (SelectionChanged != null)
            {
                SelectionChanged(this, EventArgs.Empty);
            }
        }

        private void UpdateHorizontalScrollMode()
        {
            int minimumThreeWidth = (OuterPadding * 2) + (3 * _cellSize) + (2 * CellSpacing);
            _forceHorizontalScroll = ClientSize.Width < minimumThreeWidth;
        }

        private bool TryMoveSelection(int offset)
        {
            if (_items.Count == 0 || SelectedCount > 1)
            {
                return false;
            }

            int index = GetSingleSelectedIndex();
            if (index < 0)
            {
                SelectSingle(_items[0].FilePath);
                EnsureIndexVisible(0);
                return true;
            }

            int targetIndex = Math.Max(0, Math.Min(_items.Count - 1, index + offset));
            if (targetIndex == index)
            {
                return true;
            }

            SelectSingle(_items[targetIndex].FilePath);
            EnsureIndexVisible(targetIndex);
            return true;
        }

        private bool TrySelectBoundaryItem(bool first)
        {
            if (_items.Count == 0 || SelectedCount > 1)
            {
                return false;
            }

            int targetIndex = first ? 0 : _items.Count - 1;
            SelectSingle(_items[targetIndex].FilePath);
            EnsureIndexVisible(targetIndex);
            return true;
        }

        private int GetSingleSelectedIndex()
        {
            if (SelectedCount != 1)
            {
                return -1;
            }

            string selectedPath = _selectedPaths.FirstOrDefault();
            return selectedPath == null ? -1 : IndexOf(selectedPath);
        }

        private void EnsureIndexVisible(int index)
        {
            if (index < 0 || index >= _items.Count)
            {
                return;
            }

            Rectangle bounds = GetCellBounds(index, GetColumnCount(), Point.Empty);
            int horizontal = HorizontalScroll.Visible ? HorizontalScroll.Value : 0;
            int vertical = VerticalScroll.Visible ? VerticalScroll.Value : 0;

            if (bounds.Left < horizontal + OuterPadding)
            {
                horizontal = Math.Max(0, bounds.Left - OuterPadding);
            }
            else if (bounds.Right > horizontal + ClientSize.Width - OuterPadding)
            {
                horizontal = Math.Max(0, bounds.Right - ClientSize.Width + OuterPadding);
            }

            if (bounds.Top < vertical + OuterPadding)
            {
                vertical = Math.Max(0, bounds.Top - OuterPadding);
            }
            else if (bounds.Bottom > vertical + ClientSize.Height - OuterPadding)
            {
                vertical = Math.Max(0, bounds.Bottom - ClientSize.Height + OuterPadding);
            }

            SetScrollOffset(horizontal, vertical);
        }

        private void ScrollByPage(int direction)
        {
            int rowHeight = GetCellHeight() + CellSpacing;
            int visibleRows = Math.Max(1, Math.Max(1, ClientSize.Height - (OuterPadding * 2)) / Math.Max(1, rowHeight));
            int horizontal = HorizontalScroll.Visible ? HorizontalScroll.Value : 0;
            int vertical = VerticalScroll.Visible ? VerticalScroll.Value : 0;
            int newVertical = Math.Max(0, vertical + (direction * visibleRows * rowHeight));
            SetScrollOffset(horizontal, newVertical);
        }

        private void SetScrollOffset(int horizontal, int vertical)
        {
            int maxHorizontal = Math.Max(0, HorizontalScroll.Maximum - HorizontalScroll.LargeChange + 1);
            int maxVertical = Math.Max(0, VerticalScroll.Maximum - VerticalScroll.LargeChange + 1);
            AutoScrollPosition = new Point(
                Math.Max(0, Math.Min(maxHorizontal, horizontal)),
                Math.Max(0, Math.Min(maxVertical, vertical)));
        }

        private static void DisposeItems(IEnumerable<PhotoItem> items)
        {
            foreach (PhotoItem item in items)
            {
                item.Dispose();
            }
        }

        private static Size GetScaledSize(Size source, Size target)
        {
            double scale = Math.Min((double)target.Width / Math.Max(1, source.Width), (double)target.Height / Math.Max(1, source.Height));
            scale = Math.Min(scale, 1.0);
            return new Size(
                Math.Max(1, (int)Math.Round(source.Width * scale)),
                Math.Max(1, (int)Math.Round(source.Height * scale)));
        }
    }

    internal sealed class ItemContextMenuEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string FilePath { get; private set; }
        public Point Location { get; private set; }

        public ItemContextMenuEventArgs(int index, string filePath, Point location)
        {
            Index = index;
            FilePath = filePath;
            Location = location;
        }
    }

    internal sealed class ItemDoubleClickEventArgs : EventArgs
    {
        public int Index { get; private set; }
        public string FilePath { get; private set; }

        public ItemDoubleClickEventArgs(int index, string filePath)
        {
            Index = index;
            FilePath = filePath;
        }
    }
}
