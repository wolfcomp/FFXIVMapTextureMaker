// Copyright - https://github.com/Soreepeong/QuoteOfTheLobby/blob/main/LICENSE

using System.Drawing;

namespace FFXIVMapTextureMaker;

internal class TextTextureGenerator
{
    public static readonly int[] TextureChannelOrder = { 2, 1, 0, 3 };
    private readonly List<byte[]> _textureData;
    private readonly Fdt _fdt;

    private string _text = "";
    public Fdt.LayoutBuilder.HorizontalAlignment _horizontalAlignment;
    private float _borderWidth;
    private float _borderStrength;
    private float _boldness = 1;
    private float _italicness = 4;
    private int _maxWidth;
    private Color _borderColor;
    private Color _fillColor;

    public TextTextureGenerator(List<byte[]> textureData, Fdt fdt)
    {
        _textureData = textureData;
        _fdt = fdt;
    }

    public TextTextureGenerator WithText(string text)
    {
        _text = text;
        return this;
    }

    public TextTextureGenerator WithBoldness(float boldness)
    {
        _boldness = boldness;
        return this;
    }

    public TextTextureGenerator WithItalicness(float italicness)
    {
        _italicness = italicness;
        return this;
    }

    public TextTextureGenerator WithBorderWidth(float borderWidth)
    {
        _borderWidth = borderWidth;
        return this;
    }

    public TextTextureGenerator WithBorderStrength(float borderStrength)
    {
        _borderStrength = borderStrength;
        return this;
    }

    public TextTextureGenerator WithMaxWidth(int maxWidth)
    {
        _maxWidth = maxWidth;
        return this;
    }

    public TextTextureGenerator WithBorderColor(Color borderColor)
    {
        _borderColor = borderColor;
        return this;
    }

    public TextTextureGenerator WithFillColor(Color fillColor)
    {
        _fillColor = fillColor;
        return this;
    }

    public TextTextureGenerator WithHorizontalAlignment(Fdt.LayoutBuilder.HorizontalAlignment horizontalAlignment)
    {
        _horizontalAlignment = horizontalAlignment;
        return this;
    }

    public class Result
    {
        public byte[]? Buffer { get; internal set; }
        public int Width { get; internal set; }
        public int Height { get; internal set; }
    }

    public Result Build()
    {
        var r = new Result();
        var pad = (int)Math.Ceiling(_borderWidth);

        var plan = _fdt
            .BuildLayout(_text)
            .WithItalicness(_italicness)
            .WithBoldness(_boldness)
            .WithMaxWidth(_maxWidth - 2 * pad)
            .WithHorizontalAlignment(_horizontalAlignment)
            .Build();

        var width = plan.Width + 2 * pad;
        var height = plan.Height + 2 * pad;

        var distanceMap = new float[2 * pad + 1, 2 * pad + 1];
        var strength = Math.Pow(2, -_borderStrength);
        for (var x = 0; x <= 2 * pad; x++)
        {
            for (var y = 0; y <= 2 * pad; y++)
            {
                distanceMap[x, y] = (float)Math.Pow(1 - Math.Min(1, Math.Sqrt(Math.Pow(x - pad, 2) + Math.Pow(y - pad, 2)) / _borderWidth), strength);
            }
        }

        var fillBuffer = new byte[width * height * 4];
        foreach (var p in plan.Elements)
        {
            if (p.IsControl || p.IsSpace)
                continue;
            var sourceBuffer = _textureData[p.Glyph.TextureIndex / 4];
            var sourceBufferDelta = TextureChannelOrder[p.Glyph.TextureIndex % 4];
            for (var xbold = 0; xbold < p.Bold + 1; xbold++)
            {
                float boldStrength = Math.Min(1f, p.Bold + 1 - xbold);
                for (var j = 0; j < p.Glyph.BoundingHeight; j++)
                {
                    float xDelta = xbold + pad + p.X - plan.Left;
                    if (p.Italic > 0)
                        xDelta += 1f * p.Italic * (_fdt.Fthd.LineHeight - p.Glyph.CurrentOffsetY - j) / _fdt.Fthd.LineHeight;
                    else if (p.Italic < 0)
                        xDelta -= 1f * p.Italic * (p.Glyph.CurrentOffsetY + j) / _fdt.Fthd.LineHeight;
                    var xDeltaInt = (int)Math.Floor(xDelta);
                    var xness = xDelta - xDeltaInt;
                    for (var i = 0; i < p.Glyph.BoundingWidth; i++)
                    {
                        var pos = 4 * (i + xDeltaInt + width * (j + pad + p.Y));
                        var n1 = sourceBuffer[sourceBufferDelta + 4 * (
                                p.Glyph.TextureOffsetX + i +
                                (p.Glyph.TextureOffsetY + j) * _fdt.Fthd.TextureWidth
                                )];
                        var n2 = i == p.Glyph.BoundingWidth - 1 ? 0 : sourceBuffer[sourceBufferDelta + 4 * (
                                p.Glyph.TextureOffsetX + i + 1 +
                                (p.Glyph.TextureOffsetY + j) * _fdt.Fthd.TextureWidth
                                )];
                        var n = n1 * xness + n2 * (1 - xness);
                        fillBuffer[pos + 3] = Math.Max(fillBuffer[pos + 3], (byte)(boldStrength * n));
                    }
                }
            }
        }

        if (pad > 0)
        {
            var borderBuffer = new byte[width * height * 4];
            foreach (var p in plan.Elements)
            {
                if (p.IsControl || p.IsSpace)
                    continue;
                var sourceBuffer = _textureData[p.Glyph.TextureIndex / 4];
                var sourceBufferDelta = TextureChannelOrder[p.Glyph.TextureIndex % 4];
                for (var xbold = 0; xbold < p.Bold + 1; xbold++)
                {
                    float boldStrength = Math.Min(1f, p.Bold + 1 - xbold);
                    for (var x = 0; x <= 2 * pad; x++)
                    {
                        for (var y = 0; y <= 2 * pad; y++)
                        {
                            for (var j = 0; j < p.Glyph.BoundingHeight; j++)
                            {
                                float xDelta = x + xbold + p.X - plan.Left;
                                if (p.Italic > 0)
                                    xDelta += 1f * p.Italic * (_fdt.Fthd.LineHeight - p.Glyph.CurrentOffsetY - j) / _fdt.Fthd.LineHeight;
                                else if (p.Italic < 0)
                                    xDelta -= 1f * p.Italic * (p.Glyph.CurrentOffsetY + j) / _fdt.Fthd.LineHeight;
                                var xDeltaInt = (int)Math.Floor(xDelta);
                                var xness = xDelta - xDeltaInt;
                                for (var i = 0; i < p.Glyph.BoundingWidth; i++)
                                {
                                    var pos = 4 * (i + xDeltaInt + width * (j + y + p.Y));
                                    var n1 = sourceBuffer[sourceBufferDelta + 4 * (
                                            p.Glyph.TextureOffsetX + i +
                                            (p.Glyph.TextureOffsetY + j) * _fdt.Fthd.TextureWidth
                                            )];
                                    var n2 = i == p.Glyph.BoundingWidth - 1 ? 0 : sourceBuffer[sourceBufferDelta + 4 * (
                                            p.Glyph.TextureOffsetX + i + 1 +
                                            (p.Glyph.TextureOffsetY + j) * _fdt.Fthd.TextureWidth
                                            )];
                                    var n = n1 * xness + n2 * (1 - xness);
                                    borderBuffer[pos + 3] = Math.Max(borderBuffer[pos + 3], (byte)(distanceMap[x, y] * boldStrength * n));
                                }
                            }
                        }
                    }
                }
            }

            for (var i = 0; i < borderBuffer.Length; i += 4)
            {
                float fillAlpha = fillBuffer[i + 3] / 255f;
                float borderAlpha = Math.Min(borderBuffer[i + 3], (byte)(255 - fillBuffer[i + 3])) / 255f;
                fillBuffer[i + 0] = (byte)(_fillColor.R * fillAlpha + _borderColor.R * (1 - fillAlpha));
                fillBuffer[i + 1] = (byte)(_fillColor.G * fillAlpha + _borderColor.G * (1 - fillAlpha));
                fillBuffer[i + 2] = (byte)(_fillColor.B * fillAlpha + _borderColor.B * (1 - fillAlpha));
                fillBuffer[i + 3] = (byte)(fillAlpha * _fillColor.A + (1 - fillAlpha * _fillColor.A) * borderAlpha * _borderColor.A);
            }
        }
        else
        {
            for (var i = 0; i < fillBuffer.Length; i += 4)
            {
                fillBuffer[i + 0] = _fillColor.R;
                fillBuffer[i + 1] = _fillColor.G;
                fillBuffer[i + 2] = _fillColor.B;
                fillBuffer[i + 3] = (byte)(fillBuffer[i + 3] * _fillColor.A / 255);
            }
        }

        r.Buffer = fillBuffer;
        r.Width = width;
        r.Height = height;

        return r;
    }
}