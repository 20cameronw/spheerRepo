using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Frontier.Universe.Runner
{
    /// <summary>Minimal PNG encoder (8-bit RGB) plus a tiny image canvas used for the visual checks.</summary>
    public sealed class Canvas
    {
        public readonly int Width, Height;
        public readonly byte[] Rgb;

        public Canvas(int width, int height)
        {
            Width = width;
            Height = height;
            Rgb = new byte[width * height * 3];
        }

        public void Set(int x, int y, byte r, byte g, byte b)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 3;
            Rgb[i] = r; Rgb[i + 1] = g; Rgb[i + 2] = b;
        }

        public void Blend(int x, int y, float r, float g, float b, float alpha)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 3;
            Rgb[i] = ToByte(Rgb[i] + (r * 255f - Rgb[i]) * alpha);
            Rgb[i + 1] = ToByte(Rgb[i + 1] + (g * 255f - Rgb[i + 1]) * alpha);
            Rgb[i + 2] = ToByte(Rgb[i + 2] + (b * 255f - Rgb[i + 2]) * alpha);
        }

        public void Add(int x, int y, float r, float g, float b)
        {
            if (x < 0 || y < 0 || x >= Width || y >= Height) return;
            int i = (y * Width + x) * 3;
            Rgb[i] = ToByte(Rgb[i] + r * 255f);
            Rgb[i + 1] = ToByte(Rgb[i + 1] + g * 255f);
            Rgb[i + 2] = ToByte(Rgb[i + 2] + b * 255f);
        }

        public static byte ToByte(float v)
        {
            if (v <= 0f) return 0;
            if (v >= 255f) return 255;
            return (byte)(v + 0.5f);
        }

        public void FillCircle(double cx, double cy, double radius, float r, float g, float b, float alpha)
        {
            int x0 = (int)Math.Floor(cx - radius - 1), x1 = (int)Math.Ceiling(cx + radius + 1);
            int y0 = (int)Math.Floor(cy - radius - 1), y1 = (int)Math.Ceiling(cy + radius + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                    double cover = Math.Max(0.0, Math.Min(1.0, radius - d + 0.5));
                    if (cover > 0.0) Blend(x, y, r, g, b, alpha * (float)cover);
                }
            }
        }

        public void Ring(double cx, double cy, double radius, double thickness, float r, float g, float b, float alpha)
        {
            int x0 = (int)Math.Floor(cx - radius - thickness - 1), x1 = (int)Math.Ceiling(cx + radius + thickness + 1);
            int y0 = (int)Math.Floor(cy - radius - thickness - 1), y1 = (int)Math.Ceiling(cy + radius + thickness + 1);
            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    double d = Math.Sqrt((x + 0.5 - cx) * (x + 0.5 - cx) + (y + 0.5 - cy) * (y + 0.5 - cy));
                    double cover = Math.Max(0.0, Math.Min(1.0, thickness * 0.5 - Math.Abs(d - radius) + 0.5));
                    if (cover > 0.0) Blend(x, y, r, g, b, alpha * (float)cover);
                }
            }
        }

        // 5x7 bitmap font (rows top to bottom, 5 columns each).
        static readonly Dictionary<char, string> Font = MakeFont();

        static Dictionary<char, string> MakeFont()
        {
            var f = new Dictionary<char, string>();
            string[] digits =
            {
                "01110100011001110101110011000101110", "00100011000010000100001000010001110", "01110100010000100010001000100011111",
                "11110000010000101110000010000111110", "00010001100101010010111110001000010", "11111100001111000001000011000101110",
                "00110010001000011110100011000101110", "11111000010001000100010000100001000", "01110100011000101110100011000101110",
                "01110100011000101111000010001001100"
            };
            for (int i = 0; i < 10; i++) f[(char)('0' + i)] = digits[i];
            string[] letters =
            {
                "01110100011000111111100011000110001", "11110100011000111110100011000111110", "01110100011000010000100001000101110",
                "11110100011000110001100011000111110", "11111100001000011110100001000011111", "11111100001000011110100001000010000",
                "01110100011000010111100011000101111", "10001100011000111111100011000110001", "01110001000010000100001000010001110",
                "00111000100001000010000101001001100", "10001100101010011000101001001010001", "10000100001000010000100001000011111",
                "10001110111010110101100011000110001", "10001110011010110011100011000110001", "01110100011000110001100011000101110",
                "11110100011000111110100001000010000", "01110100011000110001101011001001101", "11110100011000111110101001001010001",
                "01111100001000001110000010000111110", "11111001000010000100001000010000100", "10001100011000110001100011000101110",
                "10001100011000110001100010101000100", "10001100011000110101101011101110001", "10001100010101000100010101000110001",
                "10001100010101000100001000010000100", "11111000010001000100010001000011111"
            };
            for (int i = 0; i < 26; i++) f[(char)('A' + i)] = letters[i];
            f['-'] = string.Concat("00000", "00000", "00000", "11111", "00000", "00000", "00000");
            f['.'] = string.Concat("00000", "00000", "00000", "00000", "00000", "01100", "01100");
            f[':'] = string.Concat("00000", "01100", "01100", "00000", "01100", "01100", "00000");
            f[','] = string.Concat("00000", "00000", "00000", "00000", "01100", "00100", "01000");
            f['/'] = string.Concat("00001", "00010", "00010", "00100", "01000", "01000", "10000");
            f['+'] = string.Concat("00000", "00100", "00100", "11111", "00100", "00100", "00000");
            f['('] = string.Concat("00010", "00100", "01000", "01000", "01000", "00100", "00010");
            f[')'] = string.Concat("01000", "00100", "00010", "00010", "00010", "00100", "01000");
            f['>'] = string.Concat("01000", "00100", "00010", "00001", "00010", "00100", "01000");
            f['='] = string.Concat("00000", "00000", "11111", "00000", "11111", "00000", "00000");
            f['*'] = string.Concat("00000", "10101", "01110", "11111", "01110", "10101", "00000");
            f['%'] = string.Concat("11001", "11010", "00010", "00100", "01000", "01011", "10011");
            f[' '] = string.Concat("00000", "00000", "00000", "00000", "00000", "00000", "00000");
            return f;
        }

        public void Glyph(int x, int y, char c, int scale, float r, float g, float b, float alpha = 1f)
        {
            string rows;
            if (!Font.TryGetValue(char.ToUpperInvariant(c), out rows)) return;
            for (int gy = 0; gy < 7; gy++)
                for (int gx = 0; gx < 5; gx++)
                    if (rows[gy * 5 + gx] == '1')
                        for (int sy = 0; sy < scale; sy++)
                            for (int sx = 0; sx < scale; sx++)
                                Blend(x + gx * scale + sx, y + gy * scale + sy, r, g, b, alpha);
        }

        /// <summary>Draws text with its top-left corner at (x, y); returns the pixel width.</summary>
        public int Text(int x, int y, string text, int scale, float r, float g, float b, float alpha = 1f)
        {
            for (int i = 0; i < text.Length; i++) Glyph(x + i * 6 * scale, y, text[i], scale, r, g, b, alpha);
            return text.Length * 6 * scale;
        }

        public void Number(int x, int y, int value, int scale, float r, float g, float b)
        {
            Text(x, y, value.ToString(), scale, r, g, b);
        }

        /// <summary>Text with a dark backing box for legibility on busy maps.</summary>
        public void Label(int x, int y, string text, int scale, float r, float g, float b)
        {
            int w = text.Length * 6 * scale;
            for (int yy = y - 2; yy < y + 7 * scale + 2; yy++)
                for (int xx = x - 2; xx < x + w + 1; xx++) Blend(xx, yy, 0f, 0f, 0.02f, 0.55f);
            Text(x, y, text, scale, r, g, b);
        }

        public void Save(string path)
        {
            PngWriter.Write(path, Width, Height, Rgb);
        }
    }

    public static class PngWriter
    {
        static readonly uint[] CrcTable = MakeTable();

        static uint[] MakeTable()
        {
            var t = new uint[256];
            for (uint n = 0; n < 256; n++)
            {
                uint c = n;
                for (int k = 0; k < 8; k++) c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
                t[n] = c;
            }
            return t;
        }

        static uint Crc(byte[] type, byte[] data, int length)
        {
            uint c = 0xFFFFFFFFu;
            for (int i = 0; i < 4; i++) c = CrcTable[(c ^ type[i]) & 0xFF] ^ (c >> 8);
            for (int i = 0; i < length; i++) c = CrcTable[(c ^ data[i]) & 0xFF] ^ (c >> 8);
            return c ^ 0xFFFFFFFFu;
        }

        static void WriteChunk(Stream s, string type, byte[] data, int length)
        {
            var t = new[] { (byte)type[0], (byte)type[1], (byte)type[2], (byte)type[3] };
            WriteUInt(s, (uint)length);
            s.Write(t, 0, 4);
            s.Write(data, 0, length);
            WriteUInt(s, Crc(t, data, length));
        }

        static void WriteUInt(Stream s, uint v)
        {
            s.WriteByte((byte)(v >> 24)); s.WriteByte((byte)(v >> 16)); s.WriteByte((byte)(v >> 8)); s.WriteByte((byte)v);
        }

        public static void Write(string path, int width, int height, byte[] rgb)
        {
            var raw = new byte[(width * 3 + 1) * height];
            for (int y = 0; y < height; y++)
            {
                raw[y * (width * 3 + 1)] = 0;
                Buffer.BlockCopy(rgb, y * width * 3, raw, y * (width * 3 + 1) + 1, width * 3);
            }
            byte[] compressed;
            using (var ms = new MemoryStream())
            {
                using (var z = new ZLibStream(ms, CompressionLevel.Optimal, true)) z.Write(raw, 0, raw.Length);
                compressed = ms.ToArray();
            }
            using (var fs = File.Create(path))
            {
                fs.Write(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }, 0, 8);
                var ihdr = new byte[13];
                ihdr[0] = (byte)(width >> 24); ihdr[1] = (byte)(width >> 16); ihdr[2] = (byte)(width >> 8); ihdr[3] = (byte)width;
                ihdr[4] = (byte)(height >> 24); ihdr[5] = (byte)(height >> 16); ihdr[6] = (byte)(height >> 8); ihdr[7] = (byte)height;
                ihdr[8] = 8; ihdr[9] = 2;
                WriteChunk(fs, "IHDR", ihdr, 13);
                WriteChunk(fs, "IDAT", compressed, compressed.Length);
                WriteChunk(fs, "IEND", new byte[0], 0);
            }
        }
    }
}
