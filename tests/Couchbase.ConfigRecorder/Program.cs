using Couchbase.ConfigRecorder;

const string Usage = """
    Usage
      record [--config <path>]   Record until Ctrl+C. The default settings file is ./recorder.json.
      summarize <run folder>     Print a report of a recorded run.
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
