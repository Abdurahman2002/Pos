using System.Runtime.Versioning;
using System.Text;
using DrawingBitmap = System.Drawing.Bitmap;
using DrawingBrushes = System.Drawing.Brushes;
using DrawingColor = System.Drawing.Color;
using DrawingFont = System.Drawing.Font;
using DrawingFontFamily = System.Drawing.FontFamily;
using DrawingFontStyle = System.Drawing.FontStyle;
using DrawingGraphics = System.Drawing.Graphics;
using DrawingGraphicsUnit = System.Drawing.GraphicsUnit;
using DrawingRectangleF = System.Drawing.RectangleF;
using DrawingSmoothingMode = System.Drawing.Drawing2D.SmoothingMode;
using DrawingStringAlignment = System.Drawing.StringAlignment;
using DrawingStringFormat = System.Drawing.StringFormat;
using DrawingStringFormatFlags = System.Drawing.StringFormatFlags;
using DrawingTextRenderingHint = System.Drawing.Text.TextRenderingHint;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace NewsApp2.Classes.Helpers
{
    [SupportedOSPlatform("windows")]
    public sealed class EscPosLabelBuilder
    {
        private const int DefaultDpi = 203;
        private const int DefaultLabelWidthMm = 38;
        private const int DefaultLabelHeightMm = 25;
        private const int DefaultLogoSizeDots = 32;
        private const int RasterThreshold = 160;
        private const byte DefaultLineSpacingDots = 18;
        private const byte RasterLineGapDots = 4;
        private const byte LineFeed = 0x0A;
        private const string DefaultFontName = "Tahoma";

        private readonly Encoding _textEncoding;
        private readonly int _labelWidthDots;
        private readonly int _labelHeightDots;
        private readonly int _dpi;

        public EscPosLabelBuilder(
            int dpi = DefaultDpi,
            int labelWidthMm = DefaultLabelWidthMm,
            int labelHeightMm = DefaultLabelHeightMm,
            Encoding? textEncoding = null)
        {
            _dpi = dpi;
            _labelWidthDots = MmToDots(labelWidthMm, dpi);
            _labelHeightDots = MmToDots(labelHeightMm, dpi);
            _textEncoding = textEncoding ?? Encoding.ASCII;
        }

        public byte[] BuildLabel(
            string productName,
            string barcode,
            string price,
            string shopName,
            byte[]? logoRaster,
            string? priceLabel = null)
        {
            using var ms = new MemoryStream();

            Write(ms, 0x1B, 0x40); // Initialize
            WriteLineSpacing(ms, DefaultLineSpacingDots);

            if (logoRaster != null && logoRaster.Length > 0)
            {
                WriteAlign(ms, TextAlign.Center);
                ms.Write(logoRaster, 0, logoRaster.Length);
                WriteFeedDots(ms, RasterLineGapDots);
            }

            WriteTextLine(ms, shopName, TextAlign.Center, bold: true, FontSize.Normal, 9f);
            WriteTextLine(ms, productName, TextAlign.Center, bold: true, FontSize.DoubleHeight, 10.5f);

            WriteAlign(ms, TextAlign.Center);
            WriteBarcode(ms, barcode);
            WriteFeedDots(ms, RasterLineGapDots);

            WriteTextLine(ms, barcode, TextAlign.Center, bold: false, FontSize.Normal, 8f);

            var priceLine = BuildPriceLine(priceLabel, price);
            if (!string.IsNullOrWhiteSpace(priceLine))
            {
                WriteTextLine(ms, priceLine, TextAlign.Right, bold: true, FontSize.Normal, 9f);
            }

            WriteFeedDots(ms, 48);
            return ms.ToArray();
        }

        public byte[]? BuildLogoRaster(byte[]? logoBytes, int targetWidthDots = DefaultLogoSizeDots, int targetHeightDots = DefaultLogoSizeDots)
        {
            if (logoBytes == null || logoBytes.Length == 0)
                return null;

            try
            {
                using var image = Image.Load<Rgba32>(logoBytes);
                var maxWidth = Math.Min(targetWidthDots, _labelWidthDots);
                var maxHeight = Math.Min(targetHeightDots, _labelHeightDots);

                using var resized = image.Clone(ctx => ctx.Resize(new ResizeOptions
                {
                    Size = new SixLabors.ImageSharp.Size(maxWidth, maxHeight),
                    Mode = ResizeMode.Max
                }));

                using var canvas = new Image<Rgba32>(SixLabors.ImageSharp.Configuration.Default, _labelWidthDots, resized.Height, new Rgba32(255, 255, 255));
                var offsetX = Math.Max(0, (_labelWidthDots - resized.Width) / 2);
                canvas.Mutate(ctx => ctx.DrawImage(resized, new SixLabors.ImageSharp.Point(offsetX, 0), 1f));

                return BuildRasterImage(canvas);
            }
            catch
            {
                return null;
            }
        }

        private void WriteTextLine(MemoryStream ms, string? text, TextAlign align, bool bold, FontSize fontSize, float rasterFontSize)
        {
            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized))
                return;

            if (NeedsRaster(sanitized))
            {
                var raster = BuildTextRasterLine(sanitized, rasterFontSize, align, bold);
                if (raster != null)
                {
                    ms.Write(raster, 0, raster.Length);
                    WriteFeedDots(ms, RasterLineGapDots);
                    return;
                }
            }

            WriteAlign(ms, align);
            WriteBold(ms, bold);
            WriteFontSize(ms, fontSize);
            WriteText(ms, sanitized);
            WriteBold(ms, false);
            WriteFontSize(ms, FontSize.Normal);
            Write(ms, LineFeed);
        }

        private byte[]? BuildTextRasterLine(string text, float baseFontSize, TextAlign align, bool bold)
        {
            if (!OperatingSystem.IsWindows())
                return null;

            var fontFamily = ResolveFontFamily();
            var style = bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular;

            using var font = CreateFittedFont(text, fontFamily, baseFontSize, style);
            var height = Math.Max(16, (int)Math.Ceiling(font.GetHeight(_dpi)) + 6);

            using var bitmap = new DrawingBitmap(_labelWidthDots, height);
            bitmap.SetResolution(_dpi, _dpi);

            using (var graphics = DrawingGraphics.FromImage(bitmap))
            {
                graphics.Clear(DrawingColor.White);
                graphics.SmoothingMode = DrawingSmoothingMode.AntiAlias;
                graphics.TextRenderingHint = DrawingTextRenderingHint.AntiAlias;

                using var format = BuildStringFormat(align, IsRightToLeft(text));
                var rect = new DrawingRectangleF(0, 0, bitmap.Width, bitmap.Height);
                graphics.DrawString(text, font, DrawingBrushes.Black, rect, format);
            }

            return ConvertBitmapToRaster(bitmap);
        }

        private byte[] BuildRasterImage(Image<Rgba32> image)
        {
            var width = image.Width;
            var height = image.Height;
            var widthBytes = (width + 7) / 8;
            var data = new byte[widthBytes * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = image[x, y];
                    var luma = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    var isBlack = luma < RasterThreshold && pixel.A > 0;

                    if (!isBlack)
                        continue;

                    var index = y * widthBytes + (x / 8);
                    data[index] |= (byte)(0x80 >> (x % 8));
                }
            }

            using var ms = new MemoryStream();
            Write(ms, 0x1D, 0x76, 0x30, 0x00,
                (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
                (byte)(height & 0xFF), (byte)((height >> 8) & 0xFF));
            ms.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private static byte[] ConvertBitmapToRaster(DrawingBitmap bitmap)
        {
            var width = bitmap.Width;
            var height = bitmap.Height;
            var widthBytes = (width + 7) / 8;
            var data = new byte[widthBytes * height];

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var pixel = bitmap.GetPixel(x, y);
                    if (pixel.A == 0)
                        continue;

                    var luma = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    if (luma >= RasterThreshold)
                        continue;

                    var index = y * widthBytes + (x / 8);
                    data[index] |= (byte)(0x80 >> (x % 8));
                }
            }

            using var ms = new MemoryStream();
            Write(ms, 0x1D, 0x76, 0x30, 0x00,
                (byte)(widthBytes & 0xFF), (byte)((widthBytes >> 8) & 0xFF),
                (byte)(height & 0xFF), (byte)((height >> 8) & 0xFF));
            ms.Write(data, 0, data.Length);
            return ms.ToArray();
        }

        private void WriteBarcode(MemoryStream ms, string barcode)
        {
            var sanitized = SanitizeBarcode(barcode);
            if (string.IsNullOrWhiteSpace(sanitized))
                return;

            Write(ms, 0x1D, 0x48, 0x00); // HRI off
            Write(ms, 0x1D, 0x68, 50);   // Height
            Write(ms, 0x1D, 0x77, 2);    // Module width

            var payload = Encoding.ASCII.GetBytes("{B" + sanitized);
            var length = Math.Min(payload.Length, 255);
            Write(ms, 0x1D, 0x6B, 0x49, (byte)length);
            ms.Write(payload, 0, length);
        }

        private void WriteText(MemoryStream ms, string text)
        {
            if (string.IsNullOrEmpty(text))
                return;

            var data = _textEncoding.GetBytes(text);
            ms.Write(data, 0, data.Length);
        }

        private static void WriteLineSpacing(MemoryStream ms, byte dots)
            => Write(ms, 0x1B, 0x33, dots);

        private static void WriteFeedDots(MemoryStream ms, byte dots)
            => Write(ms, 0x1B, 0x4A, dots);

        private static void WriteAlign(MemoryStream ms, TextAlign align)
            => Write(ms, 0x1B, 0x61, (byte)align);

        private static void WriteBold(MemoryStream ms, bool enabled)
            => Write(ms, 0x1B, 0x45, (byte)(enabled ? 1 : 0));

        private static void WriteFontSize(MemoryStream ms, FontSize size)
            => Write(ms, 0x1D, 0x21, (byte)size);

        private static void Write(MemoryStream ms, params byte[] bytes)
            => ms.Write(bytes, 0, bytes.Length);

        private static int MmToDots(int mm, int dpi)
            => (int)Math.Round(mm / 25.4 * dpi);

        private static string BuildPriceLine(string? label, string price)
        {
            var sanitizedPrice = SanitizeText(price);
            if (string.IsNullOrWhiteSpace(sanitizedPrice))
                return string.Empty;

            var sanitizedLabel = SanitizeText(label);
            if (string.IsNullOrWhiteSpace(sanitizedLabel))
                return sanitizedPrice;

            return $"{sanitizedLabel}: {sanitizedPrice}";
        }

        private static string SanitizeText(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value)
            {
                if (!char.IsControl(c))
                    sb.Append(c);
            }

            return sb.ToString().Trim();
        }

        private static string SanitizeBarcode(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            var sb = new StringBuilder(value.Length);
            foreach (var c in value.Trim())
            {
                if (c >= 32 && c <= 126)
                    sb.Append(c);
            }

            return sb.ToString();
        }

        private static string Truncate(string value, int maxLength)
            => value.Length > maxLength ? value.Substring(0, maxLength) : value;

        private static bool NeedsRaster(string text)
        {
            foreach (var c in text)
            {
                if (c > 127)
                    return true;
            }

            return false;
        }

        private static bool IsRightToLeft(string text)
        {
            foreach (var c in text)
            {
                if ((c >= '\u0590' && c <= '\u08FF') || (c >= '\uFB50' && c <= '\uFEFF'))
                    return true;
            }

            return false;
        }

        private static DrawingFontFamily ResolveFontFamily()
            => TryResolveFont(DefaultFontName) ?? TryResolveFont("Segoe UI") ?? DrawingFontFamily.GenericSansSerif;

        private static DrawingFontFamily? TryResolveFont(string name)
        {
            try
            {
                return new DrawingFontFamily(name);
            }
            catch
            {
                return null;
            }
        }

        private DrawingFont CreateFittedFont(string text, DrawingFontFamily family, float baseSize, DrawingFontStyle style)
        {
            using var scratch = new DrawingBitmap(1, 1);
            using var graphics = DrawingGraphics.FromImage(scratch);

            var size = baseSize;
            var maxWidth = Math.Max(12, _labelWidthDots - 6);

            while (size > 6f)
            {
                using var testFont = new DrawingFont(family, size, style, DrawingGraphicsUnit.Point);
                if (graphics.MeasureString(text, testFont).Width <= maxWidth)
                    break;

                size -= 0.5f;
            }

            return new DrawingFont(family, size, style, DrawingGraphicsUnit.Point);
        }

        private static DrawingStringFormat BuildStringFormat(TextAlign align, bool rightToLeft)
        {
            var format = new DrawingStringFormat(DrawingStringFormatFlags.NoWrap)
            {
                Alignment = align switch
                {
                    TextAlign.Left => DrawingStringAlignment.Near,
                    TextAlign.Right => DrawingStringAlignment.Far,
                    _ => DrawingStringAlignment.Center
                },
                LineAlignment = DrawingStringAlignment.Center
            };

            if (rightToLeft)
                format.FormatFlags |= DrawingStringFormatFlags.DirectionRightToLeft;

            return format;
        }

        private enum TextAlign : byte
        {
            Left = 0,
            Center = 1,
            Right = 2
        }

        private enum FontSize : byte
        {
            Normal = 0x00,
            DoubleHeight = 0x01,
            DoubleWidth = 0x10,
            DoubleBoth = 0x11
        }
    }
}
