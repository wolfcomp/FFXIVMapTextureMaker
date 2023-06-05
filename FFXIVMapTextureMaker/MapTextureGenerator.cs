using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.Runtime.InteropServices;
using System.Text;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex.Buffers;
using Lumina.Excel.GeneratedSheets;
using Lumina.Text;
using Lumina.Text.Expressions;
using Lumina.Text.Payloads;

namespace FFXIVMapTextureMaker;

internal class MapTextureGenerator
{
    private const string _mapFileFormat = "ui/map/{0}/{1}{2}_{3}.tex";
    private const string _icoFileFormat = "ui/icon/{0:D3}000/{1:D6}_hr1.tex";
    public static List<Icon> Icons = new();
    public static List<Tuple<float, float, string, byte, int, Color>> Texts = new();

    public static int IconUidNext;

    public static unsafe Bitmap GenerateBaseTexture(Map map, string size, bool hideSpoilers)
    {
        var fileName = map.Id.ToString().Replace("/", "");
        var filePath = string.Format(_mapFileFormat, map.Id, fileName, string.Empty, size);
        var file = Program.GameData.GetFile<TexFile>(filePath);
        var tex = TextureBuffer.FromStream(file!.Header, file.Reader);
        var data = tex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData.Chunk(4).Select(t => new B8G8R8A8(t[0], t[1], t[2], t[3])).ToArray();

        var maskPath = string.Format(_mapFileFormat, map.Id, fileName, "m", size);
        var maskFile = Program.GameData.GetFile<TexFile>(maskPath);
        if (maskFile != null && maskFile.Header.Height == tex.Height && maskFile.Header.Width == tex.Width)
        {
            var maskTex = TextureBuffer.FromStream(maskFile.Header, maskFile.Reader);
            var maskData = maskTex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData.Chunk(4).Select(t => new B8G8R8A8(t[0], t[1], t[2], t[3])).ToArray();
            for (var i = 0; i < data.Length; i++)
            {
                data[i] *= maskData[i];
            }
        }

        var mapMarkers = Program.GameData.GetExcelSheet<MapMarker>()!.Where(t => t.RowId == map.MapMarkerRange).ToArray();
        if (hideSpoilers)
            mapMarkers = mapMarkers.Where(t => t.Unknown10 == 0).ToArray();
        foreach (var mapMarker in mapMarkers)
        {
            var x = mapMarker.X / 2048f * (4200f / map.SizeFactor + 0.012f * (map.SizeFactor - 100f));
            var y = mapMarker.Y / 2048f * (4200f / map.SizeFactor + 0.012f * (map.SizeFactor - 100f));
            if (mapMarker.Icon != 0)
                Icons.Add(new Icon
                {
                    UseWorld = false,
                    Id = mapMarker.Icon,
                    Scale = .5f,
                    X = x,
                    Y = y,
                    IsMapIcon = true
                });
            var placeName = mapMarker.PlaceNameSubtext.Value;
            if (placeName != null)
            {
                var t = GetStringFromSeString(placeName.Name);
                if (!string.IsNullOrWhiteSpace(t))
                    Texts.Add(new Tuple<float, float, string, byte, int, Color>(x, y, t, mapMarker.SubtextOrientation, 14, Color.White));
            }
        }

        Bitmap img;
        fixed (byte* p = data.SelectMany(t => t.GetBytes()).ToArray())
        {
            var ptr = (nint)p;
            using var tmpImage = new Bitmap(tex.Width, tex.Height, tex.Width * 4, PixelFormat.Format32bppArgb, ptr);
            img = new Bitmap(tmpImage);
        }
        return img;
    }

    public static Bitmap GenerateBaseTexture(Map map, bool hideSpoilers)
    {
        return GenerateBaseTexture(map, "m", hideSpoilers);
    }

    public static unsafe Bitmap AddIconToMap(Bitmap bitmap, int icon, int x, int y, float scale, Color overlay)
    {
        var icoPath = string.Format(_icoFileFormat, icon / 1000, icon);
        var icoFile = Program.GameData.GetFile<TexFile>(icoPath);
        var icoTex = TextureBuffer.FromStream(icoFile!.Header, icoFile.Reader);
        var icoData = icoTex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData.Chunk(4).Select(t => new B8G8R8A8(t[0], t[1], t[2], t[3])).ToArray();
        for (var i = 0; i < icoData.Length; i++)
        {
            icoData[i] *= overlay;
        }
        fixed (byte* p = icoData.SelectMany(t => t.GetBytes()).ToArray())
        {
            var ptr = (nint)p;
            using var tmpImage = new Bitmap(icoTex.Width, icoTex.Height, icoTex.Width * 4, PixelFormat.Format32bppArgb, ptr);
            using var scaledImg = new Bitmap(tmpImage, (int)(tmpImage.Width * scale), (int)(tmpImage.Height * scale));
            using var g = Graphics.FromImage(bitmap);
            g.InterpolationMode = InterpolationMode.High;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.DrawImage(scaledImg, x - scaledImg.Width / 2, y - scaledImg.Height / 2);
        }
        return bitmap;
    }

    public static Tuple<Bitmap, PointF>? AddTextToMap(string text, int x, int y, byte orientation, int emSize, Color color)
    {
        if (orientation == 0)
            return null;
        var size = emSize switch
        {
            96 => 0,
            36 => 4,
            18 => 3,
            13 => 2,
            _ => 1
        };
        var bitmap = Program.TextLayer.DrawText(text, size, color);
        var point = orientation switch
        {
            2 => new PointF(x + emSize, y - bitmap.Height / 2),
            4 => new PointF(x - bitmap.Width / 2, y - bitmap.Height),
            3 => new PointF(x - bitmap.Width / 2, y + bitmap.Height / 2),
            1 => new PointF(x - bitmap.Width + emSize, y - bitmap.Height / 2),
            _ => new PointF(x - bitmap.Width / 2, y - bitmap.Height / 2)
        };
        return Tuple.Create(bitmap, point);
    }

    private static string GetStringFromSeString(SeString s)
    {
        var sb = new StringBuilder();
        XmlRepr(sb, s);
        return ProcessString(sb.ToString());
    }

    public static string ProcessString(string s) => s.Replace("<NewLine />", "\n").Replace("<Indent />", "\t").Replace("<Hyphen />", "-").Replace("<Italics />", "*");

    public static string UnprocessString(string s) => s.Replace("\n", "<NewLine />").Replace("\t", "<Indent />").Replace("-", "<Hyphen />").Replace("*", "<Italics />");

    private static void XmlRepr(StringBuilder sb, SeString s)
    {
        foreach (var basePayload in s.Payloads)
        {
            if (basePayload is TextPayload t)
                sb.Append(t.RawString);
            else if (basePayload.PayloadType == PayloadType.Italics)
                sb.Append("<Italics />");
            else if (!basePayload.Expressions.Any())
                sb.Append($"<{basePayload.PayloadType} />");
            else
            {
                sb.Append($"<{basePayload.PayloadType}>");
                foreach (var baseExpression in basePayload.Expressions)
                    XmlRepr(sb, baseExpression);
                sb.Append($"</{basePayload.PayloadType}>");
            }
        }
    }

    private static void XmlRepr(StringBuilder sb, BaseExpression expr)
    {
        switch (expr)
        {
            case PlaceholderExpression ple:
                sb.Append('<').Append(ple.ExpressionType).Append(" />");
                break;
            case IntegerExpression ie:
                sb.Append('<').Append(ie.ExpressionType).Append('>');
                sb.Append(ie.Value);
                sb.Append("</").Append(ie.ExpressionType).Append('>');
                break;
            case StringExpression se:
                sb.Append('<').Append(se.ExpressionType).Append('>');
                XmlRepr(sb, se.Value);
                sb.Append("</").Append(se.ExpressionType).Append('>');
                break;
            case ParameterExpression pae:
                sb.Append('<').Append(pae.ExpressionType).Append('>');
                sb.Append("<operand>");
                XmlRepr(sb, pae.Operand);
                sb.Append("</operand>");
                sb.Append("</").Append(pae.ExpressionType).Append('>');
                break;
            case BinaryExpression pae:
                sb.Append('<').Append(pae.ExpressionType).Append('>');
                sb.Append("<operand1>");
                XmlRepr(sb, pae.Operand1);
                sb.Append("</operand1>");
                sb.Append("<operand2>");
                XmlRepr(sb, pae.Operand2);
                sb.Append("</operand2>");
                sb.Append("</").Append(pae.ExpressionType).Append('>');
                break;
        }
    }
}

public record Icon
{
    private readonly int _uid = MapTextureGenerator.IconUidNext++;
    public int Uid => _uid;
    public int Id { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool UseWorld { get; set; }
    public float Scale { get; set; }
    public Color OverlayColor { get; set; } = Color.White;
    public bool IsMapIcon { get; set; }

    public int MapX(int mapWidth, Map map) =>
        UseWorld switch
        {
            false => (int)(WorldToMap(MapToWorld(X, map.SizeFactor, map.OffsetX), map.SizeFactor, map.OffsetX) / ScaleMap(map) * mapWidth),
            true => (int)(WorldToMap(X, map.SizeFactor, map.OffsetX) / 42 * mapWidth)
        };

    public int MapY(int mapHeight, Map map) =>
        UseWorld switch
        {
            false => (int)(WorldToMap(MapToWorld(Y, map.SizeFactor, map.OffsetY), map.SizeFactor, map.OffsetY) / ScaleMap(map) * mapHeight),
            true => (int)(WorldToMap(Y, map.SizeFactor, map.OffsetY) / 42 * mapHeight)
        };

    private static float ScaleMap(Map map) => 4200.0f / map.SizeFactor + 0.012f * (map.SizeFactor - 100);

    private static float WorldToMap(float value, uint scale, int offset) => 0.02f * offset + 2048f / scale + 0.02f * value + .5f;

    private static float MapToWorld(float value, uint scale, int offset) => 50 * value - offset - 102400f / scale - 25;

    public override string ToString()
    {
        return $"{Id},{X},{Y},{UseWorld},{Scale},{OverlayColor.R:X} {OverlayColor.G:X} {OverlayColor.B:X},{IsMapIcon}";
    }
}

[StructLayout(LayoutKind.Sequential)]
public struct B8G8R8A8
{
    public byte B;
    public byte G;
    public byte R;
    public byte A;

    public B8G8R8A8(byte b, byte g, byte r, byte a)
    {
        R = r;
        G = g;
        B = b;
        A = a;
    }

    public static B8G8R8A8 operator *(B8G8R8A8 a, B8G8R8A8 b)
    {
        return b.A == 0 ? a : new B8G8R8A8((byte)(a.B * b.B / 255), (byte)(a.G * b.G / 255), (byte)(a.R * b.R / 255), a.A);
    }

    public static B8G8R8A8 operator *(B8G8R8A8 a, Color b)
    {
        return a.A == 0 ? a : new B8G8R8A8((byte)(a.B * b.B / 255), (byte)(a.G * b.G / 255), (byte)(a.R * b.R / 255), a.A);
    }

    public byte[] GetBytes() => new[] { B, G, R, A };
}