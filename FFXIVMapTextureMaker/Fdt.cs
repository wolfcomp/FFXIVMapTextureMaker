using Lumina.Text;
using System.Runtime.InteropServices;

namespace FFXIVMapTextureMaker;

internal class Fdt
{
    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FdtHeader
    {
        public fixed byte Signature[8];
        public int FontTableHeaderOffset;
        public int KerningTableHeaderOffset;
        public fixed byte Padding[0x10];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FontTableHeader
    {
        public fixed byte Signature[4];
        public int FontTableEntryCount;
        public int KerningTableEntryCount;
        public fixed byte Padding[0x04];
        public ushort TextureWidth;
        public ushort TextureHeight;
        public float Points;
        public int LineHeight;
        public int Ascent;
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct FontTableEntry : IComparable<FontTableEntry>
    {
        public int CharUtf8;
        public ushort CharSjis;
        public ushort TextureIndex;
        public ushort TextureOffsetX;
        public ushort TextureOffsetY;
        public byte BoundingWidth;
        public byte BoundingHeight;
        public sbyte NextOffsetX;
        public sbyte CurrentOffsetY;

        public int CompareTo(FontTableEntry other)
        {
            return CharUtf8 - other.CharUtf8;
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct KerningTableHeader
    {
        public fixed byte Signature[4];
        public int Count;
        public fixed byte Padding[0x08];
    }

    [StructLayout(LayoutKind.Sequential)]
    public unsafe struct KerningTableEntry : IComparable<KerningTableEntry>
    {
        public int LeftUtf8;
        public int RightUtf8;
        public ushort LeftSjis;
        public ushort RightSjis;
        public int RightOffset;

        public int CompareTo(KerningTableEntry other)
        {
            if (LeftUtf8 == other.LeftUtf8)
                return RightUtf8 - other.RightUtf8;
            else
                return LeftUtf8 - other.LeftUtf8;
        }
    }

    public FdtHeader Fcsv { get; init; } = new();
    public FontTableHeader Fthd { get; init; } = new();
    public KerningTableHeader Knhd { get; init; } = new();
    public List<FontTableEntry> Glyphs { get; init; } = new();
    public List<KerningTableEntry> Distances { get; init; } = new();

    unsafe public Fdt(byte[] data)
    {
        fixed (byte* ptr = data)
        {
            Fcsv = Marshal.PtrToStructure<FdtHeader>((IntPtr)ptr);
            Fthd = Marshal.PtrToStructure<FontTableHeader>(IntPtr.Add((IntPtr)ptr, Fcsv.FontTableHeaderOffset));
            Knhd = Marshal.PtrToStructure<KerningTableHeader>(IntPtr.Add((IntPtr)ptr, Fcsv.KerningTableHeaderOffset));
            for (int p = Fcsv.FontTableHeaderOffset, p_ = Fcsv.FontTableHeaderOffset + Fthd.FontTableEntryCount * Marshal.SizeOf<FontTableEntry>(); p < p_; p += Marshal.SizeOf<FontTableEntry>())
                Glyphs.Add(Marshal.PtrToStructure<FontTableEntry>(IntPtr.Add((IntPtr)ptr, p)));
            for (int p = Fcsv.KerningTableHeaderOffset, p_ = Fcsv.KerningTableHeaderOffset + Knhd.Count * Marshal.SizeOf<KerningTableEntry>(); p < p_; p += Marshal.SizeOf<KerningTableEntry>())
                Distances.Add(Marshal.PtrToStructure<KerningTableEntry>(IntPtr.Add((IntPtr)ptr, p)));
        }
    }

    static int CodePointToUtf8int32(int codepoint)
    {
        if (codepoint <= 0x7F)
        {
            return codepoint;
        }
        else if (codepoint <= 0x7FF)
        {
            return ((0xC0 | (codepoint >> 6)) << 8)
                | ((0x80 | ((codepoint >> 0) & 0x3F)) << 0);
        }
        else if (codepoint <= 0xFFFF)
        {
            return ((0xE0 | (codepoint >> 12)) << 16)
                | ((0x80 | ((codepoint >> 6) & 0x3F)) << 8)
                | ((0x80 | ((codepoint >> 0) & 0x3F)) << 0);
        }
        else if (codepoint <= 0x10FFFF)
        {
            return ((0xF0 | (codepoint >> 18)) << 24)
                | ((0x80 | ((codepoint >> 12) & 0x3F)) << 16)
                | ((0x80 | ((codepoint >> 6) & 0x3F)) << 8)
                | ((0x80 | ((codepoint >> 0) & 0x3F)) << 0);
        }
        else
        {
            return 0xFFFE;
        }
    }

    public FontTableEntry? FindGlyph(int codepoint)
    {
        var i = Glyphs.BinarySearch(new FontTableEntry { CharUtf8 = CodePointToUtf8int32(codepoint) });
        if (i < 0 || i == Glyphs.Count)
            return null;
        return Glyphs[i];
    }

    public FontTableEntry Glyph(int codepoint)
    {
        FontTableEntry? glyph;
        if ((glyph = FindGlyph(codepoint)) == null)
            if ((glyph = FindGlyph('＝')) == null)
                if ((glyph = FindGlyph('=')) == null)
                    glyph = FindGlyph('!');
        return (FontTableEntry)glyph!;
    }

    public int Distance(int codepoint1, int codepoint2)
    {
        var i = Distances.BinarySearch(new KerningTableEntry { LeftUtf8 = CodePointToUtf8int32(codepoint1), RightUtf8 = CodePointToUtf8int32(codepoint2) });
        if (i < 0 || i == Distances.Count)
            return 0;
        return Distances[i].RightOffset;
    }

    public class LayoutPlan
    {
        public class Element
        {
            public int Codepoint { get; init; }
            public float Italic { get; init; }
            public float Bold { get; init; }

            public int X { get; internal set; }
            public int Y { get; internal set; }

            public FontTableEntry Glyph { get; internal set; }

            public bool IsControl => Codepoint < 0x10000 && char.IsControl((char)Codepoint);

            public bool IsSpace => Codepoint < 0x10000 && char.IsWhiteSpace((char)Codepoint);

            public bool IsLineBreak => Codepoint == '\n' || Codepoint == '\r';

            public bool IsChineseCharacter =>
                Codepoint switch
                {
                    // CJK Symbols and Punctuation(〇)
                    >= 0x3007 and <= 0x3007 => true,
                    // CJK Unified Ideographs Extension A
                    >= 0x3400 and <= 0x4DBF => true,
                    // CJK Unified Ideographs
                    >= 0x4E00 and <= 0x9FFF => true,
                    // CJK Unified Ideographs Extension B
                    >= 0x20000 and <= 0x2A6DF => true,
                    // CJK Unified Ideographs Extension C
                    >= 0x2A700 and <= 0x2B73F => true,
                    // CJK Unified Ideographs Extension D
                    >= 0x2B740 and <= 0x2B81F => true,
                    // CJK Unified Ideographs Extension E
                    >= 0x2B820 and <= 0x2CEAF => true,
                    // CJK Unified Ideographs Extension F
                    >= 0x2CEB0 and <= 0x2EBEF => true,
                    _ => false
                };

            public bool IsWordBreakPoint
            {
                get
                {
                    if (IsChineseCharacter)
                        return true;

                    if (Codepoint >= 0x10000)
                        return false;

                    // TODO: Whatever
                    switch (char.GetUnicodeCategory((char)Codepoint))
                    {
                        case System.Globalization.UnicodeCategory.SpaceSeparator:
                        case System.Globalization.UnicodeCategory.LineSeparator:
                        case System.Globalization.UnicodeCategory.ParagraphSeparator:
                        case System.Globalization.UnicodeCategory.Control:
                        case System.Globalization.UnicodeCategory.Format:
                        case System.Globalization.UnicodeCategory.Surrogate:
                        case System.Globalization.UnicodeCategory.PrivateUse:
                        case System.Globalization.UnicodeCategory.ConnectorPunctuation:
                        case System.Globalization.UnicodeCategory.DashPunctuation:
                        case System.Globalization.UnicodeCategory.OpenPunctuation:
                        case System.Globalization.UnicodeCategory.ClosePunctuation:
                        case System.Globalization.UnicodeCategory.InitialQuotePunctuation:
                        case System.Globalization.UnicodeCategory.FinalQuotePunctuation:
                        case System.Globalization.UnicodeCategory.OtherPunctuation:
                        case System.Globalization.UnicodeCategory.MathSymbol:
                        case System.Globalization.UnicodeCategory.ModifierSymbol:
                        case System.Globalization.UnicodeCategory.OtherSymbol:
                        case System.Globalization.UnicodeCategory.OtherNotAssigned:
                            return true;
                    }
                    return false;
                }
            }
        }

        public int Left;
        public int Width;
        public int Height;
        public List<Element> Elements = new();
    }

    public class LayoutBuilder
    {
        public enum HorizontalAlignment
        {
            Left,
            Center,
            Right,
        }

        private readonly Fdt _fdt;
        private readonly string _text;
        private int _maxWidth = int.MaxValue;
        private int _translateX, _translateY;
        private float _boldness = 1;
        private float _italicness = 4;
        private HorizontalAlignment _horizontalAlignment = HorizontalAlignment.Left;

        internal LayoutBuilder(Fdt fdt, string text)
        {
            _fdt = fdt;
            _text = text;
        }

        public LayoutBuilder WithBoldness(float boldness)
        {
            _boldness = boldness;
            return this;
        }

        public LayoutBuilder WithItalicness(float italicness)
        {
            _italicness = italicness;
            return this;
        }

        public LayoutBuilder WithMaxWidth(int maxWidth)
        {
            _maxWidth = maxWidth;
            return this;
        }

        public LayoutBuilder WithTranslate(int dx, int dy)
        {
            _translateX = dx;
            _translateY = dy;
            return this;
        }

        public LayoutBuilder WithHorizontalAlignment(HorizontalAlignment horizontalAlignment)
        {
            _horizontalAlignment = horizontalAlignment;
            return this;
        }

        private void Build_LoadCodepointAndItalics(LayoutPlan plan)
        {
            float italic = 0;
            float bold = 0;
            foreach (var payload in _text)
            {
                if (payload == '*')
                    italic = italic == 0 ? _italicness : 0;
                else if (payload == '_')
                    bold = bold == 0 ? _boldness : 0;
                else
                    plan.Elements.Add(new LayoutPlan.Element() { Codepoint = payload, Bold = bold, Italic = italic });
            }

            for (var i = 0; i < plan.Elements.Count; i++)
                plan.Elements[i].Glyph = _fdt.Glyph(plan.Elements[i].Codepoint);
        }


        public LayoutPlan Build()
        {
            var plan = new LayoutPlan();
            Build_LoadCodepointAndItalics(plan);

            int lastBreakIndex = 0;
            List<int> lineBreakIndices = new() { 0 };
            for (var i = 1; i < plan.Elements.Count; i++)
            {
                var prev = plan.Elements[i - 1];
                var curr = plan.Elements[i];

                if (prev.IsLineBreak)
                {
                    curr.X = 0;
                    curr.Y = prev.Y + _fdt.Fthd.LineHeight;
                    lineBreakIndices.Add(i);
                }
                else
                {
                    curr.X = prev.X + prev.Glyph.NextOffsetX + prev.Glyph.BoundingWidth + _fdt.Distance(prev.Codepoint, curr.Codepoint);
                    curr.Y = prev.Y;
                }

                if (prev.IsWordBreakPoint)
                    lastBreakIndex = i;

                if (curr.IsSpace)
                    continue;

                if (curr.X + curr.Glyph.BoundingWidth < _maxWidth)
                    continue;

                if (!prev.IsSpace && plan.Elements[lastBreakIndex].X > 0)
                {
                    prev = plan.Elements[lastBreakIndex - 1];
                    curr = plan.Elements[lastBreakIndex];
                    i = lastBreakIndex;
                }
                else
                {
                    lastBreakIndex = i;
                }
                curr.X = 0;
                curr.Y = prev.Y + _fdt.Fthd.LineHeight;
                lineBreakIndices.Add(i);
            }

            for (var i = 0; i < plan.Elements.Count; i++)
            {
                plan.Elements[i].X += _translateX;
                plan.Elements[i].Y += _translateY;
            }

            lineBreakIndices.Add(plan.Elements.Count);
            for (var i = 1; i < lineBreakIndices.Count; i++)
            {
                var from = lineBreakIndices[i - 1];
                var to = lineBreakIndices[i];
                while (to > from && plan.Elements[to - 1].IsSpace)
                {
                    to--;
                }
                if (from >= to)
                    continue;
                var right = 0;
                for (var j = from; j < to; j++)
                {
                    var e = plan.Elements[j];
                    right = Math.Max(right, e.X + e.Glyph.BoundingWidth + (int)Math.Ceiling(e.Bold + Math.Abs(e.Italic)));
                    plan.Height = Math.Max(plan.Height, e.Y + e.Glyph.BoundingHeight);
                }
                plan.Width = Math.Max(plan.Width, right - plan.Elements[from].X);
                int offsetX;
                if (_horizontalAlignment == HorizontalAlignment.Center)
                    offsetX = (_maxWidth - right) / 2;
                else if (_horizontalAlignment == HorizontalAlignment.Right)
                    offsetX = _maxWidth - right;
                else if (_horizontalAlignment == HorizontalAlignment.Left)
                    offsetX = 0;
                else
                    throw new ArgumentException("Invalid horizontal alignment");
                for (var j = from; j < to; j++)
                    plan.Elements[j].X += offsetX;
                plan.Left = i == 1 ? plan.Elements[from].X : Math.Min(plan.Left, plan.Elements[from].X);
            }
            plan.Height = Math.Max(plan.Height, _fdt.Fthd.LineHeight * (lineBreakIndices.Count - 1));

            return plan;
        }
    }

    public LayoutBuilder BuildLayout(string text)
    {
        return new LayoutBuilder(this, text);
    }
}