using Lumina.Data.Files;

namespace FFXIVMapTextureMaker;

internal class GameResourceReader
{
    public readonly List<Fdt> Fdts = new();
    public readonly List<byte[]> FontTextureData = new();

    public static readonly string[] FontNames = {
        "AXIS_96", "AXIS_12", "AXIS_14", "AXIS_18", "AXIS_36",
        "Jupiter_16", "Jupiter_20", "Jupiter_23", "Jupiter_46",
        "MiedingerMid_10", "MiedingerMid_12", "MiedingerMid_14", "MiedingerMid_18", "MiedingerMid_36",
        "TrumpGothic_184", "TrumpGothic_23", "TrumpGothic_34", "TrumpGothic_68",
    };

    public GameResourceReader()
    {
        foreach (var fontName in FontNames)
            Fdts.Add(new Fdt(Program.GameData.GetFile($"common/font/{fontName}.fdt")!.Data));
        foreach (var i in Enumerable.Range(1, 100)) {
            var tf = Program.GameData.GetFile<TexFile>($"common/font/font{i}.tex");
            if (tf == null)
                break;

            Console.WriteLine($"Read common/font/font{i}.tex ({tf.Header.Width} x {tf.Header.Height})");
            if (tf.ImageData.Length != tf.Header.Width * tf.Header.Height * 4)
                throw new Exception("Texture data error; corrupted game resource files?");

            FontTextureData.Add(tf.ImageData);
        }
    }
}