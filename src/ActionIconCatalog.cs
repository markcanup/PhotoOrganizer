using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace PictureOrganizer
{
    internal static class ActionIconCatalog
    {
        private static readonly Dictionary<SessionActionType, Image> Cache = new Dictionary<SessionActionType, Image>();

        public static Image GetIcon(SessionActionType actionType)
        {
            if (!Cache.ContainsKey(actionType))
            {
                Cache[actionType] = BuildIcon(actionType);
            }

            return Cache[actionType];
        }

        private static Image BuildIcon(SessionActionType actionType)
        {
            Color backColor;
            string glyph;
            switch (actionType)
            {
                case SessionActionType.View:
                case SessionActionType.Fullscreen:
                case SessionActionType.Compare:
                    backColor = Color.SteelBlue;
                    glyph = "V";
                    break;
                case SessionActionType.Copy:
                    backColor = Color.SeaGreen;
                    glyph = "C";
                    break;
                case SessionActionType.Move:
                    backColor = Color.DarkOrange;
                    glyph = "M";
                    break;
                case SessionActionType.DateUpdate:
                    backColor = Color.MediumSlateBlue;
                    glyph = "D";
                    break;
                case SessionActionType.Rename:
                    backColor = Color.Teal;
                    glyph = "R";
                    break;
                case SessionActionType.Convert:
                    backColor = Color.MediumVioletRed;
                    glyph = "F";
                    break;
                case SessionActionType.Autocrop:
                    backColor = Color.SaddleBrown;
                    glyph = "A";
                    break;
                case SessionActionType.Rotate:
                    backColor = Color.CadetBlue;
                    glyph = "O";
                    break;
                case SessionActionType.Delete:
                    backColor = Color.Firebrick;
                    glyph = "X";
                    break;
                case SessionActionType.Rating:
                    backColor = Color.Goldenrod;
                    glyph = "*";
                    break;
                case SessionActionType.Edit:
                    backColor = Color.DimGray;
                    glyph = "E";
                    break;
                default:
                    backColor = Color.Gray;
                    glyph = "?";
                    break;
            }

            Bitmap bitmap = new Bitmap(16, 16);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (SolidBrush brush = new SolidBrush(backColor))
            using (Pen pen = new Pen(Color.FromArgb(80, 0, 0, 0)))
            using (Font font = new Font(SystemFonts.MenuFont.FontFamily, 8f, FontStyle.Bold, GraphicsUnit.Point))
            {
                graphics.Clear(Color.Transparent);
                graphics.FillRectangle(brush, 0, 0, 15, 15);
                graphics.DrawRectangle(pen, 0, 0, 15, 15);
                TextRenderer.DrawText(graphics, glyph, font, new Rectangle(0, 0, 16, 16), Color.White, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine);
            }

            return bitmap;
        }
    }
}
