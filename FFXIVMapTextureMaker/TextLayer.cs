using System.Drawing;
using System.Drawing.Imaging;

namespace FFXIVMapTextureMaker;

public class TextLayer
{
    private readonly GameResourceReader _reader;

    public TextLayer()
    {
        _reader = new GameResourceReader();
    }

    public unsafe Bitmap DrawText(string s, int size)
    {
        var fdt = _reader.Fdts[size];
        var texture = new TextTextureGenerator(_reader.FontTextureData, fdt)
            .WithText(s)
            .WithItalicness(4)
            .WithBoldness(4)
            .WithBorderWidth(6)
            .WithBorderStrength(1)
            .WithMaxWidth(int.MaxValue)
            .WithBorderColor(Color.Black)
            .WithFillColor(Color.White)
            .WithHorizontalAlignment(Fdt.LayoutBuilder.HorizontalAlignment.Left);
        var ret = texture.Build();
        var fill = texture.WithBorderWidth(0).Build();
        Bitmap retBitmap;
        fixed (byte* p = ret.Buffer!.Chunk(4).SelectMany(t => new B8G8R8A8(t[2], t[1], t[0], t[3]).GetBytes()).ToArray())
        {
            using var bmp = new Bitmap(ret.Width, ret.Height, ret.Width * 4, PixelFormat.Format32bppArgb, (IntPtr)p);
            retBitmap = new Bitmap(bmp);
        }
        fixed (byte* p = fill.Buffer!.Chunk(4).SelectMany(t => new B8G8R8A8(t[2], t[1], t[0], t[3]).GetBytes()).ToArray())
        {
            using var bmp = new Bitmap(fill.Width, fill.Height, fill.Width * 4, PixelFormat.Format32bppArgb, (IntPtr)p);
            using var bmp2 = new Bitmap(bmp);
            using var g = Graphics.FromImage(retBitmap);
            g.DrawImage(bmp2, 6, 6);
        }

        return retBitmap;
    }
}