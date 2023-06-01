using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Lumina.Data;
using Lumina.Data.Files;
using Lumina.Data.Parsing.Tex.Buffers;
using Lumina.Excel.GeneratedSheets;
using Lumina.Models.Materials;

namespace FFXIVMapTextureMaker;

internal class MapTextureGenerator
{
    private static string mapFileFormat = "ui/map/{0}/{1}{2}_{3}.tex";

    public static unsafe Bitmap GenerateBaseTexture(Map map, string size)
    {
        var fileName = map.Id.ToString().Replace("/", "");
        var filePath = string.Format(mapFileFormat, map.Id, fileName, string.Empty, size);
        var file = Program.GameData.GetFile<TexFile>(filePath);
        var tex = TextureBuffer.FromStream(file.Header, file.Reader);
        var data = tex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData;

        var maskPath = string.Format(mapFileFormat, map.Id, fileName, "m", size);
        var maskFile = Program.GameData.GetFile<TexFile>(maskPath);
        if (maskFile != null && maskFile.Header.Height == tex.Height && maskFile.Header.Width == tex.Width)
        {
            var maskTex = TextureBuffer.FromStream(maskFile.Header, maskFile.Reader);
            var maskData = maskTex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData;
            for (var i = 0; i < data.Length; i += 4)
            {
                if (maskData[i + 3] != 0) continue;
                data[i] *= maskData[i];
                data[i + 1] *= maskData[i + 1];
                data[i + 2] *= maskData[i + 2];
            }
        }

        Bitmap img;
        fixed (byte* p = data)
        {
            var ptr = (nint)p;
            using var tmpImage = new Bitmap(tex.Width, tex.Height, tex.Width * 4, PixelFormat.Format32bppArgb, ptr);
            img = new Bitmap(tmpImage);
        }
        return img;
    }

    public static Bitmap GenerateBaseTexture(Map map)
    {
        return GenerateBaseTexture(map, "m");
    }

    public static unsafe Bitmap AddIconToMap(Bitmap bitmap, string icoPath, int x, int y)
    {
        var icoFile = Program.GameData.GetFile<TexFile>(icoPath);
        var icoTex = TextureBuffer.FromStream(icoFile.Header, icoFile.Reader);
        var icoData = icoTex.Filter(0, 0, TexFile.TextureFormat.B8G8R8A8).RawData;
        fixed (byte* p = icoData)
        {
            var ptr = (nint)p;
            using var tmpImage = new Bitmap(icoTex.Width, icoTex.Height, icoTex.Width * 4, PixelFormat.Format32bppArgb, ptr);
            using var g = Graphics.FromImage(bitmap);
            g.DrawImage(tmpImage, x, y);
        }
        return bitmap;
    }
}