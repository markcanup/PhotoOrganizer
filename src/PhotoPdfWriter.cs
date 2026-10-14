using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;

namespace PictureOrganizer
{
    internal static class PhotoPdfWriter
    {
        public static void Save(Bitmap photo, string path)
        {
            // Reuse the PNG encoder's lossless RGB/zlib stream, including its row predictors.
            // Flatten transparency onto white, as it would appear on printed paper.
            byte[] pixels;
            using (Bitmap rgb = new Bitmap(photo.Width, photo.Height, PixelFormat.Format24bppRgb))
            using (Graphics graphics = Graphics.FromImage(rgb))
            using (MemoryStream png = new MemoryStream())
            {
                graphics.Clear(Color.White);
                graphics.DrawImageUnscaled(photo, 0, 0);
                rgb.Save(png, ImageFormat.Png);
                pixels = ReadRgbImageData(png.ToArray());
            }

            double dpi = photo.HorizontalResolution;
            if (double.IsNaN(dpi) || double.IsInfinity(dpi) || dpi <= 0) dpi = 96;
            double width = photo.Width * 72.0 / dpi, height = photo.Height * 72.0 / dpi;
            // Bound the physical page to 11 inches while retaining every image pixel.
            double scale = Math.Min(1.0, 792.0 / Math.Max(width, height));
            width *= scale; height *= scale;
            string w = width.ToString("0.######", CultureInfo.InvariantCulture);
            string h = height.ToString("0.######", CultureInfo.InvariantCulture);
            byte[] drawing = Encoding.ASCII.GetBytes("q\n" + w + " 0 0 " + h + " 0 0 cm\n/Photo Do\nQ\n");

            using (FileStream output = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                Write(output, "%PDF-1.4\n");
                List<long> offsets = new List<long> { 0 };
                Object(output, offsets, "<< /Type /Catalog /Pages 2 0 R >>");
                Object(output, offsets, "<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
                Object(output, offsets, "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 " + w + " " + h + "] /Resources << /XObject << /Photo 4 0 R >> >> /Contents 5 0 R >>");
                StreamObject(output, offsets, "/Type /XObject /Subtype /Image /Width " + photo.Width + " /Height " + photo.Height
                    + " /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /FlateDecode /DecodeParms << /Predictor 15 /Colors 3 /BitsPerComponent 8 /Columns " + photo.Width + " >>", pixels);
                StreamObject(output, offsets, string.Empty, drawing);
                long xref = output.Position;
                Write(output, "xref\n0 6\n0000000000 65535 f \n");
                for (int i = 1; i < offsets.Count; i++) Write(output, offsets[i].ToString("0000000000", CultureInfo.InvariantCulture) + " 00000 n \n");
                Write(output, "trailer\n<< /Size 6 /Root 1 0 R >>\nstartxref\n" + xref.ToString(CultureInfo.InvariantCulture) + "\n%%EOF\n");
                output.Flush(true);
            }
        }

        private static byte[] ReadRgbImageData(byte[] png)
        {
            using (MemoryStream input = new MemoryStream(png))
            using (BinaryReader reader = new BinaryReader(input))
            using (MemoryStream data = new MemoryStream())
            {
                byte[] signature = reader.ReadBytes(8);
                if (signature.Length != 8 || signature[0] != 137 || signature[1] != 80 || signature[2] != 78 || signature[3] != 71) throw new InvalidDataException("The image encoder did not produce a PNG.");
                bool rgbHeader = false, ended = false;
                while (input.Position < input.Length)
                {
                    byte[] lengthBytes = reader.ReadBytes(4);
                    if (lengthBytes.Length != 4) throw new InvalidDataException("Incomplete PNG chunk.");
                    uint length = ((uint)lengthBytes[0] << 24) | ((uint)lengthBytes[1] << 16) | ((uint)lengthBytes[2] << 8) | lengthBytes[3];
                    string type = Encoding.ASCII.GetString(reader.ReadBytes(4));
                    if (length > int.MaxValue || length + 4L > input.Length - input.Position) throw new InvalidDataException("Invalid PNG chunk length.");
                    byte[] chunk = reader.ReadBytes((int)length);
                    reader.ReadBytes(4); // CRC was produced by the platform encoder.
                    if (type == "IHDR") rgbHeader = chunk.Length == 13 && chunk[8] == 8 && chunk[9] == 2 && chunk[12] == 0;
                    if (type == "IDAT") data.Write(chunk, 0, chunk.Length);
                    if (type == "IEND") { ended = true; break; }
                }
                if (!rgbHeader || !ended || data.Length == 0) throw new InvalidDataException("The PNG encoder did not produce a supported RGB image.");
                return data.ToArray();
            }
        }

        private static void Object(Stream output, List<long> offsets, string value)
        {
            offsets.Add(output.Position);
            Write(output, (offsets.Count - 1) + " 0 obj\n" + value + "\nendobj\n");
        }
        private static void StreamObject(Stream output, List<long> offsets, string dictionary, byte[] bytes)
        {
            offsets.Add(output.Position);
            Write(output, (offsets.Count - 1) + " 0 obj\n<< " + dictionary + " /Length " + bytes.Length + " >>\nstream\n");
            output.Write(bytes, 0, bytes.Length);
            Write(output, "\nendstream\nendobj\n");
        }
        private static void Write(Stream output, string value)
        {
            byte[] bytes = Encoding.ASCII.GetBytes(value);
            output.Write(bytes, 0, bytes.Length);
        }
    }
}
