using System.CommandLine;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Lumina;
using Lumina.Excel.GeneratedSheets;

namespace FFXIVMapTextureMaker;

public class Program
{
    public static GameData GameData = null!;
    public static TextLayer TextLayer = null!;
    private int _page;
    private List<Map> maps = null!;
    private Map _selectedMap = null!;
    private Bitmap _baseTexture = null!;
    private RootCommand _rootCommand = null!;
    private bool _handleCommands;
    private bool _hideSpoilers;

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
        var gamePath = new DirectoryInfo(@"C:\Program Files (x86)\SquareEnix\FINAL FANTASY XIV - A Realm Reborn\game\sqpack");
        var xivLauncherPath = new FileInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "XIVLauncher", "launcherConfigV3.json"));
        if (!gamePath.Exists && xivLauncherPath.Exists)
        {
            Console.WriteLine("Could not find pre programmed path checking with XIVLauncher location");
            JsonDocument.Parse(xivLauncherPath.OpenText().ReadToEnd()).RootElement.TryGetProperty("GamePath", out var gamePathElement);
            gamePath = new DirectoryInfo(Path.Combine(gamePathElement.GetString()!, "game", "sqpack"));
        }
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
            gamePath = new DirectoryInfo(path!);
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
        TextLayer = new TextLayer();
        await Task.Delay(TimeSpan.FromSeconds(1));
        await SelectMap();
    }

    private async Task SelectMap()
    {
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
                    _selectedMap = maps.Skip(_page * 9).Take(9).Skip(i).First();
                    Console.WriteLine($"Generating base texture for {_selectedMap.PlaceName.Value?.Name} - {_selectedMap.PlaceNameSub.Value?.Name}");
                    _baseTexture = MapTextureGenerator.GenerateBaseTexture(_selectedMap, _hideSpoilers);
                    await ProcessFurtherCommands();
                    PrintMaps();
                    break;
                case ConsoleKey.H:
                    _hideSpoilers = !_hideSpoilers;
                    PrintMaps();
                    break;
                default:
                    break;
            }

            input = Console.ReadKey();
        }
    }

    private void SetupCommandArgs()
    {
        _rootCommand = new RootCommand();
        var generateOption = new Option<string>("--output", "The output file to save the texture to.");
        var generateMinimal = new Option<bool>("--minimal", "Generates just the texture without exporting texts and icons files.");
        var generateBase = new Option<bool>("--base", "Generates just the base texture without texts and icons files.");
        var generate = new Command("generate")
        {
            generateOption,
            generateMinimal,
            generateBase
        };
        generate.AddAlias("save");
        generate.SetHandler(GenerateTexture, generateOption, generateMinimal, generateBase);
        _rootCommand.AddCommand(generate);
        var addId = new Option<int>("--id", "The id of the icon.");
        var scale = new Option<float>("--scale", "Scale the icon.");
        var x = new Option<float?>("--x", "The x position of the icon in map coords.");
        var y = new Option<float?>("--y", "The y position of the icon in map coords.");
        var worldX = new Option<float?>("--worldX", "The x position of the icon in world coords.");
        var worldY = new Option<float?>("--worldY", "The y position of the icon in world coords.");
        var add = new Command("add") { addId, scale, x, y, worldX, worldY };
        add.SetHandler(AddIcon, addId, scale, x, y, worldX, worldY);
        _rootCommand.AddCommand(add);
        var uid = new Option<int>("--uid", "The id of the icon to remove.");
        var remove = new Command("remove")
        {
            uid
        };
        remove.SetHandler((uid) =>
        {
            MapTextureGenerator.Icons.RemoveAll(t => t.Uid == uid);
        }, uid);
        _rootCommand.AddCommand(remove);
        var list = new Command("list");
        list.SetHandler(() =>
        {
            foreach (var icon in MapTextureGenerator.Icons)
            {
                Console.WriteLine($"Uid: {icon.Uid} Id: {icon.Id} Scale: {icon.Scale} X: {icon.MapX(_baseTexture.Width, _selectedMap)} Y: {icon.MapY(_baseTexture.Height, _selectedMap)} UseWorld: {icon.UseWorld}");
            }
        });
        _rootCommand.AddCommand(list);
        var loadOption = new Option<string>("--file", "The file to load the icons from.");
        var load = new Command("load", "Loads .icons.csv and .texts.csv file to place all icons and text into memory")
        {
            loadOption
        };
        load.SetHandler((filePath) =>
        {
            if (!File.Exists(filePath + ".icons.csv") && !File.Exists(filePath + ".texts.csv"))
            {
                Console.WriteLine("File does not exist.");
                return;
            }
            Console.WriteLine("Loading...");
            MapTextureGenerator.Icons = File.ReadAllLines(filePath + ".icons.csv").Skip(1).Select(t =>
            {
                //Id,X,Y,UseWorld,Scale
                var args = t.Split(',');
                var color = args[5].Split(' ').Select(s => byte.Parse(s, NumberStyles.AllowHexSpecifier)).ToArray();
                return new Icon
                {
                    Id = int.Parse(args[0]),
                    X = float.Parse(args[1]),
                    Y = float.Parse(args[2]),
                    UseWorld = bool.Parse(args[3]),
                    Scale = float.Parse(args[4]),
                    OverlayColor = Color.FromArgb(color[0], color[1], color[2]),
                    IsMapIcon = bool.Parse(args[6])
                };
            }).ToList();
            MapTextureGenerator.Texts = File.ReadAllLines(filePath + ".texts.csv").Skip(1).Select(t =>
            {
                var args = t.Split(',');
                var color = args[5].Split(' ').Select(s => byte.Parse(s, NumberStyles.AllowHexSpecifier)).ToArray();
                return Tuple.Create(float.Parse(args[0]), float.Parse(args[1]), MapTextureGenerator.ProcessString(args[2]), byte.Parse(args[3]), int.Parse(args[4]), Color.FromArgb(color[0], color[1], color[2]));
            }).ToList();
            Console.WriteLine("Loaded!");
        }, loadOption);
        _rootCommand.AddCommand(load);
        var exit = new Command("exit");
        exit.AddAlias("quit");
        exit.SetHandler(() => Environment.Exit(0));
        _rootCommand.AddCommand(exit);
        var newCommand = new Command("new");
        newCommand.SetHandler(() =>
        {
            _handleCommands = false;
        });
        _rootCommand.AddCommand(newCommand);
    }

    private static void AddIcon(int id, float scale, float? x, float? y, float? worldX, float? worldY)
    {
        switch (x)
        {
            case null when y is null && worldX is null && worldY is null:
                Console.WriteLine("You must specify either x and y or worldX and worldY");
                return;
            case null when y is null && (worldX is null || worldY is null):
                Console.WriteLine("You must specify both x and y or worldX and worldY");
                return;
            case not null when y is not null && (worldX is not null || worldY is not null):
                Console.WriteLine("You must specify either x and y or worldX and worldY");
                return;
            default:
                var worldCoords = x is null;
                MapTextureGenerator.Icons.Add(new Icon
                {
                    Id = id,
                    Scale = scale,
                    UseWorld = worldCoords,
                    X = worldCoords ? worldX!.Value : x!.Value,
                    Y = worldCoords ? worldY!.Value : y!.Value
                });
                break;
        }
    }

    private void GenerateTexture(string? output, bool minimal, bool @base)
    {
        if (output == null)
        {
            Console.WriteLine("No output file specified.");
            return;
        }

        if (output.Where(t => t is not ('.' or '\\' or ':')).Any(t => Path.GetInvalidFileNameChars().Contains(t)))
        {
            Console.WriteLine("Invalid output file name.");
            return;
        }

        output = output.Split(".")[0];
        var bitmap = new Bitmap(_baseTexture);
        if (!@base)
        {
            using var g = Graphics.FromImage(bitmap);
            bitmap = MapTextureGenerator.Icons.Where(t => t.IsMapIcon).Aggregate(bitmap, (current, icon) =>
                MapTextureGenerator.AddIconToMap(current, icon.Id, icon.MapX(_baseTexture.Width, _selectedMap), icon.MapY(_baseTexture.Height, _selectedMap), icon.Scale, icon.OverlayColor));
            var p = MapTextureGenerator.Texts.Select(text => MapTextureGenerator.AddTextToMap(text.Item3, (int)(text.Item1 / ScaleMap * _baseTexture.Width), (int)(text.Item2 / ScaleMap * _baseTexture.Height), text.Item4, text.Item5, text.Item6)).Where(t => t != null).Cast<Tuple<Bitmap, PointF>>().ToArray();
            g.InterpolationMode = InterpolationMode.High;
            g.SmoothingMode = SmoothingMode.HighQuality;
            g.TextRenderingHint = TextRenderingHint.AntiAliasGridFit;
            g.CompositingQuality = CompositingQuality.HighQuality;
            foreach (var (b, point) in p)
            {
                g.DrawImage(b, point);
            }
            bitmap = MapTextureGenerator.Icons.Where(t => !t.IsMapIcon).Aggregate(bitmap, (current, icon) => MapTextureGenerator.AddIconToMap(current, icon.Id, icon.MapX(_baseTexture.Width, _selectedMap), icon.MapY(_baseTexture.Height, _selectedMap), icon.Scale, icon.OverlayColor));
        }
        bitmap.Save(output + ".png");
        if (minimal) return;
        File.WriteAllLines(output + ".icons.csv", MapTextureGenerator.Icons.Select(t => t.ToString()).Prepend("Id,X,Y,UseWorld,Scale,OverlayColor,MapIcon"));
        File.WriteAllLines(output + ".texts.csv", MapTextureGenerator.Texts.Select(t => $"{t.Item1},{t.Item2},{MapTextureGenerator.UnprocessString(t.Item3)},{t.Item4},{t.Item5},{t.Item6.R:X} {t.Item6.G:X} {t.Item6.B:X}").Prepend("X,Y,Text,Orientation,FontSize,Color"));
    }

    private float ScaleMap => 4200.0f / _selectedMap.SizeFactor + 0.012f * (_selectedMap.SizeFactor - 100);

    private async Task ProcessFurtherCommands()
    {
        SetupCommandArgs();
        Console.WriteLine("Generated base map in memory.");
        await _rootCommand.InvokeAsync("-h");
        _handleCommands = true;
        while (_handleCommands)
        {
            var line = Console.ReadLine()?.Split(" ");
            if (line == null)
                continue;
            await _rootCommand.InvokeAsync(line);
        }
        MapTextureGenerator.Icons.Clear();
        MapTextureGenerator.Texts.Clear();
        GC.Collect();
        GC.WaitForFullGCComplete();
    }

    private void PrintMaps()
    {
        Console.Clear();
        Console.WriteLine("Please select a map to generate a texture for.");

        var k = 0;
        foreach (var map in maps.Skip(_page * 9).Take(9))
        {
            Console.WriteLine($"{++k}: {map.PlaceName.Value?.Name} - {map.PlaceNameSub.Value?.Name}");
        }

        Console.WriteLine("0: Exit");
        Console.WriteLine("N: Next Page");
        Console.WriteLine("P: Previous Page");
        Console.WriteLine($"H: {(_hideSpoilers ? "Show" : "Hide")} Spoilers");
    }
}