using Couchbase.ConfigRecorder;
using Couchbase.ConfigRecorder.Reduce;

const string Usage = """
    Usage
      record [--config <path>]   Record until Ctrl+C. The default settings file is ./recorder.json.
      summarize <run folder>     Print a report of a recorded run.
      reduce --name <profile> --out <dir> [--server-version <version>] <run folder>...
                                 Write an anonymized profile of the runs to <dir>/<profile>.
    """;

if (args.Length == 0 || args[0] is "-h" or "--help" or "help")
{
    Console.WriteLine(Usage);
    return args.Length == 0 ? 2 : 0;
}

try
{
    switch (args[0])
    {
        case "record":
            var configPath = "recorder.json";
            for (var i = 1; i < args.Length; i++)
            {
                if (args[i] == "--config" && i + 1 < args.Length)
                {
                    configPath = args[++i];
                }
                else
                {
                    Console.Error.WriteLine($"Unknown argument '{args[i]}'.");
                    Console.Error.WriteLine(Usage);
                    return 2;
                }
            }

            return await RecordCommand.RunAsync(RecorderSettings.FromFile(configPath));
        case "summarize" when args.Length == 2:
            return SummarizeCommand.Run(args[1], Console.Out);
        case "reduce":
            string? name = null, outFolder = null, serverVersion = null;
            var runFolders = new List<string>();
            for (var i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--name" when i + 1 < args.Length:
                        name = args[++i];
                        break;
                    case "--out" when i + 1 < args.Length:
                        outFolder = args[++i];
                        break;
                    case "--server-version" when i + 1 < args.Length:
                        serverVersion = args[++i];
                        break;
                    case var flag when flag.StartsWith("--", StringComparison.Ordinal):
                        Console.Error.WriteLine($"Unknown argument '{flag}'.");
                        Console.Error.WriteLine(Usage);
                        return 2;
                    default:
                        runFolders.Add(args[i]);
                        break;
                }
            }

            if (name is null || outFolder is null || runFolders.Count == 0)
            {
                Console.Error.WriteLine(Usage);
                return 2;
            }

            return ReduceCommand.Run(name, outFolder, serverVersion, runFolders, Console.Out);
        default:
            Console.Error.WriteLine(Usage);
            return 2;
    }
}
catch (SettingsException e)
{
    Console.Error.WriteLine(e.Message);
    return 2;
}
