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

namespace NewsApp2.Classes.Helpers
{
    [SupportedOSPlatform("windows")]
    public sealed class TsplLabelBuilder
    {
        private const int DefaultDpi = 203;
        private const int DefaultLabelWidthMm = 38;
        private const int DefaultLabelHeightMm = 25;
        private const int DefaultLabelGapMm = 2;
        private const int DefaultSpeed = 4;
        private const int DefaultDensity = 10;
        private const int RasterThreshold = 160;
        private const string DefaultFontName = "Arial";

        private readonly int _dpi;
        private readonly int _labelWidthMm;
        private readonly int _labelHeightMm;
        private readonly int _labelGapMm;
        private readonly int _speed;
        private readonly int _density;
        private readonly int _labelWidthDots;
        private readonly int _labelHeightDots;

        public TsplLabelBuilder(
            int dpi = DefaultDpi,
            int labelWidthMm = DefaultLabelWidthMm,
            int labelHeightMm = DefaultLabelHeightMm,
            int labelGapMm = DefaultLabelGapMm,
            int speed = DefaultSpeed,
            int density = DefaultDensity)
        {
            _dpi = dpi;
            _labelWidthMm = labelWidthMm;
            _labelHeightMm = labelHeightMm;
            _labelGapMm = labelGapMm;
            _speed = speed;
            _density = density;
            _labelWidthDots = MmToDots(labelWidthMm, dpi);
            _labelHeightDots = MmToDots(labelHeightMm, dpi);
        }

        public byte[] BuildLabel(
            string productName,
            string barcode,
            string price,
            string shopName,
            byte[]? logoBytes,
            string? priceLabel = null,
            int copies = 1)
        {
            using var ms = new MemoryStream(2048);
            WriteAscii(ms, $"SIZE {_labelWidthMm} mm,{_labelHeightMm} mm\r\n");
            WriteAscii(ms, $"GAP {_labelGapMm} mm,0 mm\r\n");
            WriteAscii(ms, $"SPEED {_speed}\r\n");
            WriteAscii(ms, $"DENSITY {_density}\r\n");
            WriteAscii(ms, "DIRECTION 0\r\n");
            WriteAscii(ms, "CLS\r\n");

            const int marginV    = 3;
            const int gapSmall   = 2;
            const int gapMedium  = 4;
            var       padH       = MmToDots(1, _dpi);
            var       contentW   = Math.Max(40, _labelWidthDots - padH * 2);
            var       cleanCode  = SanitizeBarcode(barcode);
            var       priceLine  = BuildPriceLine(priceLabel, price);

            // ── render text bitmaps (all centred) ──────────────────────────────
            using var shopBmp  = BuildTextBitmap(shopName,    8f,   bold: true,  TextAlign.Center, contentW);
            using var nameBmp  = BuildTextBitmap(productName, 11f,  bold: true,  TextAlign.Center, contentW);
            using var codeBmp  = BuildTextBitmap(cleanCode,   10f,  bold: false, TextAlign.Center, contentW);
            using var priceBmp = BuildTextBitmap(priceLine,   12f,  bold: false, TextAlign.Center, contentW);

            int shopH  = shopBmp?.Height  ?? 0;
            int nameH  = nameBmp?.Height  ?? 0;
            int codeH  = codeBmp?.Height  ?? 0;
            int priceH = priceBmp?.Height ?? 0;

            // ── decide barcode height from remaining vertical space ───────────
            int fixedH = (shopH  > 0 ? shopH  + gapSmall  : 0)
                       + (nameH  > 0 ? nameH  + gapMedium : 0)
                       + (codeH  > 0 ? codeH  + gapSmall  : 0)
                       + (priceH > 0 ? priceH + gapSmall  : 0);

            int available  = _labelHeightDots - marginV * 2 - fixedH - gapMedium;
            int barcodeH   = Math.Clamp(available, 22, 55);

            // ── centre the barcode horizontally ───────────────────────────────
            int estBarcodeW = EstimateBarcodeWidth128(cleanCode, narrowDots: 2);
            int barcodeX    = Math.Max(padH, (_labelWidthDots - estBarcodeW) / 2);

            // ── lay out top → bottom ──────────────────────────────────────────
            int y = marginV;

            if (shopBmp != null)
            {
                AppendBitmap(ms, padH, y, shopBmp);
                y += shopH + gapSmall;
            }

            if (nameBmp != null)
            {
                AppendBitmap(ms, padH, y, nameBmp);
                y += nameH + gapMedium;
            }

            if (!string.IsNullOrWhiteSpace(cleanCode) && barcodeH >= 22)
            {
                WriteAscii(ms, $"BARCODE {barcodeX},{y},\"128\",{barcodeH},0,0,2,3,\"{cleanCode}\"\r\n");
                y += barcodeH + gapSmall;
            }

            if (codeBmp != null)
            {
                AppendBitmap(ms, padH, y, codeBmp);
                y += codeH + gapSmall;
            }

            if (priceBmp != null)
            {
                AppendBitmap(ms, padH, y, priceBmp);
            }

            WriteAscii(ms, $"PRINT {Math.Max(1, copies)},1\r\n");
            return ms.ToArray();
        }

        /// <summary>
        /// Rough estimate of Code128 bar width in dots.
        /// Code128: (start + data + check + stop) = (n+3)*11 + 2 modules.
        /// </summary>
        private static int EstimateBarcodeWidth128(string data, int narrowDots)
        {
            if (string.IsNullOrEmpty(data)) return 0;
            int modules = (data.Length + 3) * 11 + 2;
            return modules * narrowDots;
        }

        private DrawingBitmap? BuildLogoBitmap(byte[]? logoBytes, int maxWidthDots, int maxHeightDots)
        {
            if (logoBytes == null || logoBytes.Length == 0)
                return null;

            try
            {
                using var ms = new MemoryStream(logoBytes);
                using var image = System.Drawing.Image.FromStream(ms);
                var ratio = Math.Min((double)maxWidthDots / image.Width, (double)maxHeightDots / image.Height);
                var width = Math.Max(1, (int)Math.Round(image.Width * ratio));
                var height = Math.Max(1, (int)Math.Round(image.Height * ratio));

                var bitmap = new DrawingBitmap(width, height);
                bitmap.SetResolution(_dpi, _dpi);
                using var graphics = DrawingGraphics.FromImage(bitmap);
                graphics.Clear(DrawingColor.White);
                graphics.SmoothingMode = DrawingSmoothingMode.None;
                graphics.DrawImage(image, 0, 0, width, height);
                return bitmap;
            }
            catch
            {
                return null;
            }
        }

        private DrawingBitmap? BuildTextBitmap(string? text, float baseFontSize, bool bold, TextAlign align, int? canvasWidthDots = null)
        {
            var sanitized = SanitizeText(text);
            if (string.IsNullOrWhiteSpace(sanitized))
                return null;

            var fontFamily = ResolveFontFamily();
            var style = bold ? DrawingFontStyle.Bold : DrawingFontStyle.Regular;
            var targetWidth = canvasWidthDots ?? _labelWidthDots;

            using var font = CreateFittedFont(sanitized, fontFamily, baseFontSize, style, targetWidth);
            var height = Math.Max(16, (int)Math.Ceiling(font.GetHeight(_dpi)) + 8);

            var bitmap = new DrawingBitmap(targetWidth, height);
            bitmap.SetResolution(_dpi, _dpi);

            using (var graphics = DrawingGraphics.FromImage(bitmap))
            {
                graphics.Clear(DrawingColor.White);
                graphics.SmoothingMode = DrawingSmoothingMode.AntiAlias;
                graphics.TextRenderingHint = DrawingTextRenderingHint.AntiAlias;

                using var format = BuildStringFormat(align, IsRightToLeft(sanitized));
                var rect = new DrawingRectangleF(0, 0, bitmap.Width, bitmap.Height);
                graphics.DrawString(sanitized, font, DrawingBrushes.Black, rect, format);
            }

            return bitmap;
        }

        private static void AppendBitmap(MemoryStream ms, int x, int y, DrawingBitmap bitmap)
        {
            var widthBytes = (bitmap.Width + 7) / 8;
            var height = bitmap.Height;
            var data = new byte[widthBytes * height];

            for (var row = 0; row < height; row++)
            {
                for (var col = 0; col < bitmap.Width; col++)
                {
                    var pixel = bitmap.GetPixel(col, row);
                    if (pixel.A == 0)
                        continue;

                    var luma = (pixel.R * 299 + pixel.G * 587 + pixel.B * 114) / 1000;
                    if (luma >= RasterThreshold)
                        continue;

                    var index = row * widthBytes + (col / 8);
                    data[index] |= (byte)(0x80 >> (col % 8));
                }
            }

            for (var i = 0; i < data.Length; i++)
            {
                data[i] = (byte)~data[i];
            }

            WriteAscii(ms, $"BITMAP {x},{y},{widthBytes},{height},0,");
            ms.Write(data, 0, data.Length);
            WriteAscii(ms, "\r\n");
        }

        private static void WriteAscii(MemoryStream ms, string value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            var bytes = Encoding.ASCII.GetBytes(value);
            ms.Write(bytes, 0, bytes.Length);
        }

        private DrawingFont CreateFittedFont(string text, DrawingFontFamily family, float baseSize, DrawingFontStyle style, int targetWidthDots)
        {
            using var scratch = new DrawingBitmap(1, 1);
            using var graphics = DrawingGraphics.FromImage(scratch);

            var size = baseSize;
            var maxWidth = Math.Max(12, targetWidthDots - 6);

            while (size > 6f)
            {
                using var testFont = new DrawingFont(family, size, style, DrawingGraphicsUnit.Point);
                if (graphics.MeasureString(text, testFont).Width <= maxWidth)
                    break;

                size -= 0.5f;
            }

            return new DrawingFont(family, size, style, DrawingGraphicsUnit.Point);
        }

        private static DrawingFontFamily ResolveFontFamily()
            => TryResolveFont("Arial") ?? TryResolveFont("Segoe UI") ?? TryResolveFont("Tahoma") ?? DrawingFontFamily.GenericSansSerif;

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

            // Format price line with clear separation for better readability
            return $"{sanitizedLabel}\u200B: {sanitizedPrice}";
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

        private static bool IsRightToLeft(string text)
        {
            foreach (var c in text)
            {
                if ((c >= '\u0590' && c <= '\u08FF') || (c >= '\uFB50' && c <= '\uFEFF'))
                    return true;
            }

            return false;
        }

        private enum TextAlign
        {
            Left,
            Center,
            Right
        }
    }
}
