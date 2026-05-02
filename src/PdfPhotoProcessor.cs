using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Data.Pdf;

using Windows.Storage;
using Windows.Storage.Streams;

namespace PictureOrganizer
{
    internal static class PdfPhotoProcessor
    {
        private const long JpegQuality = 100L;
        private const string EditedPrefix = "Ralph-";

        public static void ProcessFolder(string sourceFolder, IProgress<string> progress)
        {
            string[] pdfFiles = Directory.GetFiles(sourceFolder, "*.pdf", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (pdfFiles.Length == 0)
            {
                throw new InvalidOperationException("No PDF files were found in the selected folder.");
            }

            string conversionFolder = CreateConversionFolder(sourceFolder);

            foreach (string sourcePdfPath in pdfFiles)
            {
                string fileName = Path.GetFileName(sourcePdfPath);
                progress.Report("Processing " + fileName + "...");

                try
                {
                    ProcessSingleFile(sourcePdfPath, conversionFolder);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException("File: " + fileName + Environment.NewLine + "Error: " + ex.Message, ex);
                }
            }
        }

        private static void ProcessSingleFile(string sourcePdfPath, string conversionFolder)
        {
            string copiedPdfPath = Path.Combine(conversionFolder, Path.GetFileName(sourcePdfPath));
            File.Copy(sourcePdfPath, copiedPdfPath, false);

            string jpgPath = Path.ChangeExtension(copiedPdfPath, ".JPG");
            string editedBaseName = Path.GetFileNameWithoutExtension(copiedPdfPath) + "-EDIT";
            if (!editedBaseName.StartsWith(EditedPrefix, StringComparison.OrdinalIgnoreCase))
            {
                editedBaseName = EditedPrefix + editedBaseName;
            }

            string editedPath = Path.Combine(conversionFolder, editedBaseName + ".JPG");

            using (Bitmap renderedBitmap = RenderPdfPageToBitmap(copiedPdfPath))
            {
                SaveJpeg(renderedBitmap, jpgPath, JpegQuality);

                using (Bitmap editedBitmap = AutoCropBitmap(renderedBitmap))
                {
                    SaveJpeg(editedBitmap, editedPath, JpegQuality);
                }
            }
        }

        private static string CreateConversionFolder(string sourceFolder)
        {
            int suffix = 1;
            while (true)
            {
                string folderName = suffix == 1 ? "PDF2JPEG" : "PDF2JPEG (" + suffix + ")";
                string candidate = Path.Combine(sourceFolder, folderName);
                if (!Directory.Exists(candidate))
                {
                    Directory.CreateDirectory(candidate);
                    return candidate;
                }

                suffix++;
            }
        }

        public static Bitmap RenderPdfPageToBitmap(string pdfPath)
        {
            StorageFile storageFile = Await(StorageFile.GetFileFromPathAsync(pdfPath));
            PdfDocument document = Await(PdfDocument.LoadFromFileAsync(storageFile));
            if (document.PageCount == 0)
            {
                throw new InvalidOperationException("The PDF does not contain any pages.");
            }

            PdfPage page = document.GetPage(0);
            try
            {
                using (var stream = new InMemoryRandomAccessStream())
                {
                    double scale = 600.0 / 96.0;
                    uint width = Math.Max(1u, (uint)Math.Round(page.Size.Width * scale));
                    uint height = Math.Max(1u, (uint)Math.Round(page.Size.Height * scale));

                    var renderOptions = new PdfPageRenderOptions();
                    renderOptions.DestinationWidth = width;
                    renderOptions.DestinationHeight = height;

                    Await(page.RenderToStreamAsync(stream, renderOptions));
                    stream.Seek(0);

                    using (Stream managedStream = stream.AsStreamForRead())
                    using (var memoryStream = new MemoryStream())
                    {
                        managedStream.CopyTo(memoryStream);
                        memoryStream.Position = 0;
                        using (Image image = Image.FromStream(memoryStream))
                        {
                            return new Bitmap(image);
                        }
                    }
                }
            }
            finally
            {
                if (page != null)
                {
                    page.Dispose();
                }
            }
        }

        public static int GetPdfPageCount(string pdfPath)
        {
            StorageFile storageFile = Await(StorageFile.GetFileFromPathAsync(pdfPath));
            PdfDocument document = Await(PdfDocument.LoadFromFileAsync(storageFile));
            return (int)document.PageCount;
        }

        private static T Await<T>(Windows.Foundation.IAsyncOperation<T> operation)
        {
            return System.WindowsRuntimeSystemExtensions.AsTask(operation).GetAwaiter().GetResult();
        }

        private static void Await(Windows.Foundation.IAsyncAction action)
        {
            System.WindowsRuntimeSystemExtensions.AsTask(action).GetAwaiter().GetResult();
        }

        public static Bitmap AutoCropBitmap(Bitmap original)
        {
            using (Bitmap preview = ResizeForAnalysis(original, 900))
            {
                bool[,] mask = BuildForegroundMask(preview);
                List<PointF> points = ExtractForegroundPoints(mask);

                if (points.Count < 100)
                {
                    return new Bitmap(original);
                }

                double angle = FindBestRotation(points, mask.GetLength(1), mask.GetLength(0));

                using (Bitmap rotatedPreview = RotateBitmap(preview, (float)angle))
                {
                    bool[,] rotatedMask = BuildForegroundMask(rotatedPreview);
                    Rectangle maskCrop = FindCropRectangle(rotatedMask);
                    Rectangle textureCrop = FindTextureCrop(rotatedPreview);
                    Rectangle projectionCrop = FindProjectionCrop(rotatedPreview);
                    Rectangle previewCrop = ChooseBestCrop(rotatedPreview.Size, maskCrop, textureCrop, projectionCrop);

                    if (previewCrop.Width < 10 || previewCrop.Height < 10)
                    {
                        return new Bitmap(original);
                    }

                    using (Bitmap rotatedOriginal = RotateBitmap(original, (float)angle))
                    {
                        Rectangle scaledCrop = ScaleRectangle(previewCrop, rotatedPreview.Size, rotatedOriginal.Size);
                        Rectangle boundedCrop = Rectangle.Intersect(
                            new Rectangle(Point.Empty, rotatedOriginal.Size),
                            scaledCrop);

                        if (boundedCrop.Width < 10 || boundedCrop.Height < 10)
                        {
                            return new Bitmap(rotatedOriginal);
                        }

                        return CropBitmap(rotatedOriginal, boundedCrop);
                    }
                }
            }
        }

        private static Bitmap ResizeForAnalysis(Bitmap source, int maxDimension)
        {
            int largest = Math.Max(source.Width, source.Height);
            if (largest <= maxDimension)
            {
                return new Bitmap(source);
            }

            double scale = (double)maxDimension / (double)largest;
            int width = Math.Max(1, (int)Math.Round(source.Width * scale));
            int height = Math.Max(1, (int)Math.Round(source.Height * scale));
            return ResizeBitmap(source, width, height);
        }

        private static Bitmap ResizeBitmap(Bitmap source, int width, int height)
        {
            var resized = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(resized))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.DrawImage(source, new Rectangle(0, 0, width, height));
            }

            return resized;
        }

        private static bool[,] BuildForegroundMask(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            bool[,] colorMask = new bool[height, width];
            bool[,] edgeMask = new bool[height, width];

            Color background = EstimateBackground(bitmap);
            int backgroundThreshold = 28;
            int edgeThreshold = 36;
            int mixedThreshold = 20;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int backgroundDiff = ColorDifference(pixel, background);
                    if (backgroundDiff > backgroundThreshold)
                    {
                        colorMask[y, x] = true;
                    }

                    int neighborContrast = 0;
                    if (x + 1 < width)
                    {
                        neighborContrast = Math.Max(neighborContrast, ColorDifference(pixel, bitmap.GetPixel(x + 1, y)));
                    }

                    if (y + 1 < height)
                    {
                        neighborContrast = Math.Max(neighborContrast, ColorDifference(pixel, bitmap.GetPixel(x, y + 1)));
                    }

                    if (neighborContrast > edgeThreshold || (neighborContrast > mixedThreshold && backgroundDiff > mixedThreshold))
                    {
                        edgeMask[y, x] = true;
                    }
                }
            }

            colorMask = Dilate(colorMask, 1);
            edgeMask = Dilate(edgeMask, 1);

            bool[,] mask = OrMasks(colorMask, edgeMask);
            mask = Dilate(mask, 1);
            mask = Erode(mask, 1);
            mask = KeepLargestComponent(mask);
            mask = Dilate(mask, 2);
            return mask;
        }

        private static Rectangle FindTextureCrop(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            double[] columnScores = new double[width];
            double[] rowScores = new double[height];

            for (int y = 0; y < height - 1; y += 2)
            {
                for (int x = 0; x < width - 1; x += 2)
                {
                    int current = Luminance(bitmap.GetPixel(x, y));
                    int rightValue = Luminance(bitmap.GetPixel(x + 1, y));
                    int down = Luminance(bitmap.GetPixel(x, y + 1));
                    int diagonal = Luminance(bitmap.GetPixel(x + 1, y + 1));

                    double energy = Math.Abs(current - rightValue)
                        + Math.Abs(current - down)
                        + (Math.Abs(current - diagonal) * 0.5);

                    columnScores[x] += energy;
                    rowScores[y] += energy;
                }
            }

            double[] smoothColumns = SmoothScores(columnScores, Math.Max(5, width / 60));
            double[] smoothRows = SmoothScores(rowScores, Math.Max(5, height / 60));

            int left = FindAdaptiveSignalStart(smoothColumns, 6);
            int right = FindAdaptiveSignalEnd(smoothColumns, 6);
            int top = FindAdaptiveSignalStart(smoothRows, 6);
            int bottom = FindAdaptiveSignalEnd(smoothRows, 6);

            if (left < 0 || top < 0 || right <= left || bottom <= top)
            {
                return Rectangle.Empty;
            }

            int horizontalMargin = Math.Max(8, width / 70);
            int verticalMargin = Math.Max(8, height / 70);
            left = Math.Max(0, left - horizontalMargin);
            top = Math.Max(0, top - verticalMargin);
            right = Math.Min(width - 1, right + horizontalMargin);
            bottom = Math.Min(height - 1, bottom + verticalMargin);

            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static Rectangle FindProjectionCrop(Bitmap bitmap)
        {
            int width = bitmap.Width;
            int height = bitmap.Height;
            Color background = EstimateBackground(bitmap);
            double[] columnScores = new double[width];
            double[] rowScores = new double[height];

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    Color pixel = bitmap.GetPixel(x, y);
                    int score = 0;

                    if (ColorDifference(pixel, background) > 18)
                    {
                        score += 2;
                    }

                    if (x + 2 < width)
                    {
                        score += ColorDifference(pixel, bitmap.GetPixel(x + 2, y)) > 28 ? 1 : 0;
                    }

                    if (y + 2 < height)
                    {
                        score += ColorDifference(pixel, bitmap.GetPixel(x, y + 2)) > 28 ? 1 : 0;
                    }

                    if (score > 0)
                    {
                        columnScores[x] += score;
                        rowScores[y] += score;
                    }
                }
            }

            double[] smoothColumns = SmoothScores(columnScores, Math.Max(5, width / 80));
            double[] smoothRows = SmoothScores(rowScores, Math.Max(5, height / 80));
            int left = FindAdaptiveSignalStart(smoothColumns, 4);
            int right = FindAdaptiveSignalEnd(smoothColumns, 4);
            int top = FindAdaptiveSignalStart(smoothRows, 4);
            int bottom = FindAdaptiveSignalEnd(smoothRows, 4);

            if (left < 0 || top < 0 || right <= left || bottom <= top)
            {
                return Rectangle.Empty;
            }

            int horizontalMargin = Math.Max(6, width / 80);
            int verticalMargin = Math.Max(6, height / 80);
            left = Math.Max(0, left - horizontalMargin);
            top = Math.Max(0, top - verticalMargin);
            right = Math.Min(width - 1, right + horizontalMargin);
            bottom = Math.Min(height - 1, bottom + verticalMargin);

            return Rectangle.FromLTRB(left, top, right + 1, bottom + 1);
        }

        private static Rectangle ChooseBestCrop(Size canvasSize, Rectangle maskCrop, Rectangle textureCrop, Rectangle projectionCrop)
        {
            Rectangle canvas = new Rectangle(Point.Empty, canvasSize);
            Rectangle boundedMask = Rectangle.Intersect(canvas, maskCrop);
            Rectangle boundedTexture = Rectangle.Intersect(canvas, textureCrop);
            Rectangle boundedProjection = Rectangle.Intersect(canvas, projectionCrop);
            bool maskValid = IsUsableCrop(canvasSize, boundedMask);
            bool textureValid = IsUsableCrop(canvasSize, boundedTexture);
            bool projectionValid = IsUsableCrop(canvasSize, boundedProjection);

            if (textureValid)
            {
                if (!maskValid)
                {
                    return boundedTexture;
                }

                double maskArea = (double)boundedMask.Width * (double)boundedMask.Height;
                double textureArea = (double)boundedTexture.Width * (double)boundedTexture.Height;
                double canvasArea = (double)canvasSize.Width * (double)canvasSize.Height;

                if (maskArea > canvasArea * 0.93 && textureArea < maskArea * 0.92)
                {
                    return boundedTexture;
                }

                Rectangle overlap = Rectangle.Intersect(boundedMask, boundedTexture);
                double overlapArea = Math.Max(0.0, (double)overlap.Width * (double)overlap.Height);
                double smallerArea = Math.Max(1.0, Math.Min(maskArea, textureArea));
                if (overlapArea / smallerArea >= 0.70)
                {
                    return textureArea <= maskArea ? boundedTexture : boundedMask;
                }

                if (textureArea < maskArea * 0.88)
                {
                    return boundedTexture;
                }
            }

            if (maskValid)
            {
                return boundedMask;
            }

            if (projectionValid)
            {
                return boundedProjection;
            }

            return textureValid ? boundedTexture : Rectangle.Empty;
        }

        private static bool IsUsableCrop(Size canvasSize, Rectangle crop)
        {
            if (crop.Width < 10 || crop.Height < 10)
            {
                return false;
            }

            double cropArea = (double)crop.Width * (double)crop.Height;
            double canvasArea = (double)canvasSize.Width * (double)canvasSize.Height;
            return cropArea >= canvasArea * 0.05 && cropArea <= canvasArea * 0.995;
        }

        private static int FindAdaptiveSignalStart(double[] values, int runLength)
        {
            if (values.Length == 0)
            {
                return -1;
            }

            double average = values.Average();
            double max = values.Max();
            double min = values.Min();
            double threshold = Math.Max(average * 1.15, min + ((max - min) * 0.22));
            int run = 0;
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] >= threshold)
                {
                    run++;
                    if (run >= runLength)
                    {
                        return i - runLength + 1;
                    }
                }
                else
                {
                    run = 0;
                }
            }

            return -1;
        }

        private static int FindAdaptiveSignalEnd(double[] values, int runLength)
        {
            if (values.Length == 0)
            {
                return -1;
            }

            double average = values.Average();
            double max = values.Max();
            double min = values.Min();
            double threshold = Math.Max(average * 1.15, min + ((max - min) * 0.22));
            int run = 0;
            for (int i = values.Length - 1; i >= 0; i--)
            {
                if (values[i] >= threshold)
                {
                    run++;
                    if (run >= runLength)
                    {
                        return i + runLength - 1;
                    }
                }
                else
                {
                    run = 0;
                }
            }

            return -1;
        }

        private static bool[,] OrMasks(bool[,] first, bool[,] second)
        {
            int height = first.GetLength(0);
            int width = first.GetLength(1);
            bool[,] result = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    result[y, x] = first[y, x] || second[y, x];
                }
            }

            return result;
        }

        private static int ColorDifference(Color first, Color second)
        {
            return Math.Abs(first.R - second.R) + Math.Abs(first.G - second.G) + Math.Abs(first.B - second.B);
        }

        private static int Luminance(Color color)
        {
            return (int)Math.Round((color.R * 0.299) + (color.G * 0.587) + (color.B * 0.114));
        }

        private static double[] SmoothScores(double[] values, int radius)
        {
            double[] result = new double[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                int start = Math.Max(0, i - radius);
                int end = Math.Min(values.Length - 1, i + radius);
                double sum = 0;
                int count = 0;
                for (int j = start; j <= end; j++)
                {
                    sum += values[j];
                    count++;
                }

                result[i] = count == 0 ? 0 : sum / count;
            }

            return result;
        }

        private static Color EstimateBackground(Bitmap bitmap)
        {
            var samples = new List<Color>();
            int block = Math.Max(8, Math.Min(bitmap.Width, bitmap.Height) / 25);
            SampleBlock(bitmap, 0, 0, block, block, samples);
            SampleBlock(bitmap, bitmap.Width - block, 0, block, block, samples);
            SampleBlock(bitmap, 0, bitmap.Height - block, block, block, samples);
            SampleBlock(bitmap, bitmap.Width - block, bitmap.Height - block, block, block, samples);

            int r = (int)samples.Average(c => c.R);
            int g = (int)samples.Average(c => c.G);
            int b = (int)samples.Average(c => c.B);
            return Color.FromArgb(r, g, b);
        }

        private static void SampleBlock(Bitmap bitmap, int startX, int startY, int width, int height, List<Color> samples)
        {
            int endX = Math.Min(bitmap.Width, startX + width);
            int endY = Math.Min(bitmap.Height, startY + height);
            for (int y = Math.Max(0, startY); y < endY; y += 2)
            {
                for (int x = Math.Max(0, startX); x < endX; x += 2)
                {
                    samples.Add(bitmap.GetPixel(x, y));
                }
            }
        }

        private static bool[,] Dilate(bool[,] mask, int radius)
        {
            int height = mask.GetLength(0);
            int width = mask.GetLength(1);
            bool[,] result = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool value = false;
                    for (int dy = -radius; dy <= radius && !value; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= height)
                        {
                            continue;
                        }

                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= width)
                            {
                                continue;
                            }

                            if (mask[yy, xx])
                            {
                                value = true;
                                break;
                            }
                        }
                    }

                    result[y, x] = value;
                }
            }

            return result;
        }

        private static bool[,] Erode(bool[,] mask, int radius)
        {
            int height = mask.GetLength(0);
            int width = mask.GetLength(1);
            bool[,] result = new bool[height, width];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    bool value = true;
                    for (int dy = -radius; dy <= radius && value; dy++)
                    {
                        int yy = y + dy;
                        if (yy < 0 || yy >= height)
                        {
                            value = false;
                            break;
                        }

                        for (int dx = -radius; dx <= radius; dx++)
                        {
                            int xx = x + dx;
                            if (xx < 0 || xx >= width || !mask[yy, xx])
                            {
                                value = false;
                                break;
                            }
                        }
                    }

                    result[y, x] = value;
                }
            }

            return result;
        }

        private static bool[,] KeepLargestComponent(bool[,] mask)
        {
            int height = mask.GetLength(0);
            int width = mask.GetLength(1);
            bool[,] visited = new bool[height, width];
            bool[,] best = new bool[height, width];
            int bestCount = 0;
            int[] xOffsets = { -1, 0, 1, -1, 1, -1, 0, 1 };
            int[] yOffsets = { -1, -1, -1, 0, 0, 1, 1, 1 };

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[y, x] || visited[y, x])
                    {
                        continue;
                    }

                    var queue = new Queue<Point>();
                    var component = new List<Point>();
                    queue.Enqueue(new Point(x, y));
                    visited[y, x] = true;

                    while (queue.Count > 0)
                    {
                        Point point = queue.Dequeue();
                        component.Add(point);

                        for (int i = 0; i < xOffsets.Length; i++)
                        {
                            int xx = point.X + xOffsets[i];
                            int yy = point.Y + yOffsets[i];
                            if (xx < 0 || yy < 0 || xx >= width || yy >= height)
                            {
                                continue;
                            }

                            if (visited[yy, xx] || !mask[yy, xx])
                            {
                                continue;
                            }

                            visited[yy, xx] = true;
                            queue.Enqueue(new Point(xx, yy));
                        }
                    }

                    if (component.Count > bestCount)
                    {
                        bestCount = component.Count;
                        best = new bool[height, width];
                        foreach (Point point in component)
                        {
                            best[point.Y, point.X] = true;
                        }
                    }
                }
            }

            return bestCount == 0 ? mask : best;
        }

        private static List<PointF> ExtractForegroundPoints(bool[,] mask)
        {
            int height = mask.GetLength(0);
            int width = mask.GetLength(1);
            var points = new List<PointF>();

            for (int y = 0; y < height; y += 2)
            {
                for (int x = 0; x < width; x += 2)
                {
                    if (mask[y, x])
                    {
                        points.Add(new PointF(x, y));
                    }
                }
            }

            return points;
        }

        private static double FindBestRotation(List<PointF> points, int width, int height)
        {
            double centerX = width / 2.0;
            double centerY = height / 2.0;
            double bestAngle = 0.0;
            double bestScore = double.MaxValue;

            for (double angle = -12.0; angle <= 12.0001; angle += 0.25)
            {
                double radians = angle * Math.PI / 180.0;
                double cos = Math.Cos(radians);
                double sin = Math.Sin(radians);
                double minX = double.MaxValue;
                double minY = double.MaxValue;
                double maxX = double.MinValue;
                double maxY = double.MinValue;

                foreach (PointF point in points)
                {
                    double dx = point.X - centerX;
                    double dy = point.Y - centerY;
                    double rx = (dx * cos) - (dy * sin);
                    double ry = (dx * sin) + (dy * cos);

                    if (rx < minX) minX = rx;
                    if (ry < minY) minY = ry;
                    if (rx > maxX) maxX = rx;
                    if (ry > maxY) maxY = ry;
                }

                double area = (maxX - minX + 1.0) * (maxY - minY + 1.0);
                if (area < bestScore)
                {
                    bestScore = area;
                    bestAngle = angle;
                }
            }

            return bestAngle;
        }

        private static Bitmap RotateBitmap(Bitmap source, float angle)
        {
            if (Math.Abs(angle) < 0.01f)
            {
                return new Bitmap(source);
            }

            double radians = angle * Math.PI / 180.0;
            double cos = Math.Abs(Math.Cos(radians));
            double sin = Math.Abs(Math.Sin(radians));
            int newWidth = Math.Max(1, (int)Math.Round((source.Width * cos) + (source.Height * sin)));
            int newHeight = Math.Max(1, (int)Math.Round((source.Width * sin) + (source.Height * cos)));

            var rotated = new Bitmap(newWidth, newHeight, PixelFormat.Format24bppRgb);
            rotated.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(rotated))
            {
                graphics.Clear(Color.White);
                graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
                graphics.SmoothingMode = SmoothingMode.HighQuality;
                graphics.TranslateTransform(newWidth / 2f, newHeight / 2f);
                graphics.RotateTransform(angle);
                graphics.TranslateTransform(-source.Width / 2f, -source.Height / 2f);
                graphics.DrawImage(source, new Rectangle(0, 0, source.Width, source.Height));
            }

            return rotated;
        }

        private static Rectangle FindCropRectangle(bool[,] mask)
        {
            int height = mask.GetLength(0);
            int width = mask.GetLength(1);
            int minX = width;
            int minY = height;
            int maxX = -1;
            int maxY = -1;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (!mask[y, x])
                    {
                        continue;
                    }

                    if (x < minX) minX = x;
                    if (y < minY) minY = y;
                    if (x > maxX) maxX = x;
                    if (y > maxY) maxY = y;
                }
            }

            if (maxX < minX || maxY < minY)
            {
                return Rectangle.Empty;
            }

            int margin = Math.Max(4, (int)Math.Round(Math.Max(maxX - minX, maxY - minY) * 0.015));
            minX = Math.Max(0, minX - margin);
            minY = Math.Max(0, minY - margin);
            maxX = Math.Min(width - 1, maxX + margin);
            maxY = Math.Min(height - 1, maxY + margin);

            return Rectangle.FromLTRB(minX, minY, maxX + 1, maxY + 1);
        }

        private static Rectangle ScaleRectangle(Rectangle sourceRect, Size sourceSize, Size targetSize)
        {
            double scaleX = (double)targetSize.Width / (double)sourceSize.Width;
            double scaleY = (double)targetSize.Height / (double)sourceSize.Height;

            int left = (int)Math.Floor(sourceRect.Left * scaleX);
            int top = (int)Math.Floor(sourceRect.Top * scaleY);
            int right = (int)Math.Ceiling(sourceRect.Right * scaleX);
            int bottom = (int)Math.Ceiling(sourceRect.Bottom * scaleY);

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private static Bitmap CropBitmap(Bitmap source, Rectangle cropRect)
        {
            var target = new Bitmap(cropRect.Width, cropRect.Height, PixelFormat.Format24bppRgb);
            target.SetResolution(source.HorizontalResolution, source.VerticalResolution);

            using (Graphics graphics = Graphics.FromImage(target))
            {
                graphics.Clear(Color.White);
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, cropRect.Width, cropRect.Height),
                    cropRect,
                    GraphicsUnit.Pixel);
            }

            return target;
        }

        public static void SaveBitmap(Bitmap bitmap, string path, ImageFormat format)
        {
            if (format.Guid == ImageFormat.Jpeg.Guid)
            {
                SaveJpeg(bitmap, path, JpegQuality);
                return;
            }

            bitmap.Save(path, format);
        }

        private static void SaveJpeg(Bitmap bitmap, string path, long quality)
        {
            ImageCodecInfo encoder = ImageCodecInfo.GetImageEncoders()
                .First(codec => codec.FormatID == ImageFormat.Jpeg.Guid);

            using (var parameters = new EncoderParameters(1))
            {
                parameters.Param[0] = new EncoderParameter(Encoder.Quality, quality);
                bitmap.Save(path, encoder, parameters);
            }
        }
    }
}








