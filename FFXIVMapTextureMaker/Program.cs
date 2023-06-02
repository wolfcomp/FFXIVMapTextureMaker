using System.CommandLine;
using System.CommandLine.Invocation;
using System.Drawing;
using System.Reflection;
using Lumina;
using Lumina.Excel.GeneratedSheets;

namespace FFXIVMapTextureMaker;

public class Program
{
    public static GameData GameData;
    private int _page;
    private List<Map> maps;
    private Bitmap _baseTexture;
    private RootCommand _rootCommand;
    private bool handleCommands;

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
                    var map = maps.Skip(_page * 9).Take(9).Skip(i).First();
                    Console.WriteLine($"Generating base texture for {map.PlaceName.Value?.Name}");
                    _baseTexture = MapTextureGenerator.GenerateBaseTexture(map);
                    await ProcessFurtherCommands();
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
        var generate = new Command("generate")
        {
            generateOption
        };
        generate.AddAlias("save");
        generate.SetHandler(GenerateTexture, generateOption);
        _rootCommand.AddCommand(generate);
        var add = new Command("add")
        {
            new Option<int>("--id", "The id of the icon."),
            new Option<float>("--scale", "Scale the icon."),
            new Option<float>("--x", "The x position of the icon in map coords."),
            new Option<float>("--y", "The y position of the icon in map coords.")
        };
        add.SetHandler(() =>
        {
            Console.WriteLine("Not implemented yet.");
        });
        _rootCommand.AddCommand(add);
        var remove = new Command("remove")
        {
            new Option<int>("--uid", "The id of the icon to remove.")
        };
        remove.SetHandler(() => 
        {
            Console.WriteLine("Not implemented yet.");
        });
        _rootCommand.AddCommand(remove);
        var list = new Command("list");
        list.SetHandler(() =>
        {
            Console.WriteLine("Not implemented yet.");
        });
        _rootCommand.AddCommand(list);
        var exit = new Command("exit");
        exit.AddAlias("quit");
        exit.SetHandler(() => Environment.Exit(0));
        _rootCommand.AddCommand(exit);
        var newCommand = new Command("new");
        newCommand.SetHandler(() =>
        {
            handleCommands = false;
        });
        _rootCommand.AddCommand(newCommand);
    }

    private void GenerateTexture(string? output)
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

        output = output.Split(".")[0] + ".png";
        var bitmap = new Bitmap(_baseTexture);
        bitmap = MapTextureGenerator.Icons.Aggregate(bitmap, (current, icon) => MapTextureGenerator.AddIconToMap(current, icon.Id, icon.MapX, icon.MapY, icon.Scale));
        bitmap.Save(output);
    }

    private async Task ProcessFurtherCommands()
    {
        SetupCommandArgs();
        handleCommands = true;
        while (handleCommands)
        {
            var line = Console.ReadLine()?.Split(" ");
            if (line == null)
                continue;
            await _rootCommand.InvokeAsync(line);
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