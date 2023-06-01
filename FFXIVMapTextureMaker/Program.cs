
using System.Reflection;
using Lumina;
using Lumina.Excel.GeneratedSheets;

namespace FFXIVMapTextureMaker;

public class Program
{
    public static GameData GameData;
    private int _page;
    private List<Map> maps;

    public static void Main(string[] args)
    {
        new Program().MainAsync(args).GetAwaiter().GetResult();
    }

    public async Task MainAsync(string[] args)
    {
        Console.WriteLine("FFXIV Map Texture Maker");
        Console.WriteLine("By: Wolfcomp");
        Console.WriteLine($"Version: {Assembly.GetExecutingAssembly().GetName().Version}");
        Console.WriteLine("");
        DirectoryInfo gamePath = new DirectoryInfo(@"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack");
        while (!gamePath.Exists)
        {
            Console.WriteLine("Could not find the path of the game location of sqpack please input it:");
        inputGameDir:
            var path = Console.ReadLine();
            if (path != null)
            {
                Console.WriteLine("Not a valid input try again.");
                goto inputGameDir;
            }
            gamePath = new DirectoryInfo(path);
        }
        Console.WriteLine("Loading Game Data...");
        GameData = new GameData(gamePath.FullName, new LuminaOptions
        {
            PanicOnSheetChecksumMismatch = false
        });
        Console.WriteLine("Game Data Loaded! Found...");
        foreach (var (key, repo) in GameData.Repositories)
        {
            Console.WriteLine($"{key}: {repo.Version}");
        }

        maps = GameData.GetExcelSheet<Map>()!.Where(t => !string.IsNullOrWhiteSpace(t.Id) && t.RowId != 10).ToList();
        await Task.Delay(TimeSpan.FromSeconds(1));
        PrintMaps();
        var input = Console.ReadKey();
        while (input.Key != ConsoleKey.D0)
        {
            switch (input.Key)
            {
                case ConsoleKey.N:
                    _page++;
                    PrintMaps();
                    break;
                case ConsoleKey.P:
                    if (_page > 0)
                        _page--;
                    PrintMaps();
                    break;
                case ConsoleKey.D1:
                case ConsoleKey.D2:
                case ConsoleKey.D3:
                case ConsoleKey.D4:
                case ConsoleKey.D5:
                case ConsoleKey.D6:
                case ConsoleKey.D7:
                case ConsoleKey.D8:
                case ConsoleKey.D9:
                    var i = (int)input.Key - 49;
                    var map = maps.Skip(_page * 9).Take(9).Skip(i).First();
                    Console.WriteLine($"Generating base texture for {map.PlaceName.Value?.Name}");
                    var baseTexture = MapTextureGenerator.GenerateBaseTexture(map);
                    baseTexture.Save($"D:\\{map.PlaceName.Value?.Name}.png");
                    return;
                default:
                    break;
            }

            input = Console.ReadKey();
        }
    }

    private void PrintMaps()
    {
        Console.Clear();
        Console.WriteLine("Please select a map to generate a texture for.");

        var k = 0;
        foreach (var map in maps.Skip(_page * 9).Take(9))
        {
            Console.WriteLine($"{++k}: {map.PlaceName.Value?.Name}");
        }

        Console.WriteLine("0: Exit");
        Console.WriteLine("N: Next Page");
        Console.WriteLine("P: Previous Page");
    }
}