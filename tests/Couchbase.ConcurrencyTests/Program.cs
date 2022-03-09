using CommandLine;
using Couchbase;
using Couchbase.ConcurrencyTests;
using Couchbase.ConcurrencyTests.Connections;
using Serilog;
using Serilog.Extensions.Logging;
using System.Diagnostics;

var mainOpts = new CommandLineOptions();
var parserResult = CommandLine.Parser.Default.ParseArguments<CommandLineOptions>(args)
    .WithNotParsed(errs =>
    {
        Environment.Exit(1);
    })
    .WithParsed( opts =>
    {
        mainOpts = opts;
    });

Console.Out.WriteLine(mainOpts);
using var cancelRunTokenSource = mainOpts.Time.HasValue ? new CancellationTokenSource(mainOpts.Time.Value) : new CancellationTokenSource();
void HandleCancel(object? sender, ConsoleCancelEventArgs args)
{
    Console.Out.WriteLine("Cancellation requested");
    cancelRunTokenSource!.Cancel();
    args.Cancel = true;
}

Console.CancelKeyPress += new ConsoleCancelEventHandler(HandleCancel);

// TODO: make these part of the config/options
var clusterOptions = new ClusterOptions()
{
    UserName = "Administrator",
    Password = "password",
    ConnectionString = "couchbase://localhost"
};

Serilog.Log.Logger = new Serilog.LoggerConfiguration()
        .Enrich.FromLogContext()
        .MinimumLevel.Is(mainOpts.LogLevel)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss.fff} {Level:u3}] |{Properties:j}| [{SourceContext:l}] {Message:lj}{NewLine}{Exception}").CreateLogger();
clusterOptions.WithLogging(new SerilogLoggerFactory());

Console.Out.WriteLine($"PID = {Environment.ProcessId}, Name = {Process.GetCurrentProcess().ProcessName}");

await using var mainCluster = await Cluster.ConnectAsync(clusterOptions);
ConnectionManager.AddConnection("main", mainCluster);

var allScenarios = Scenarios.GetScenarios("main", mainOpts.Scenarios);
foreach (var scenario in allScenarios)
{
    await scenario.Warmup(cancelRunTokenSource.Token);
    Console.Out.WriteLine($"[{scenario.Name}, {scenario.ActorCount}]");
}

if (mainOpts.WaitForCounters)
{
    Console.Out.WriteLine("Waiting for start to give time to attach performance monitors.");
    Console.Out.WriteLine(" dotnet-counters.exe monitor --counters CouchbaseNetClient --maxTimeSeries 10000 --maxHistograms 1000 --name Couchbase.ConcurrencyTests");
    Console.Out.WriteLine("Press any key to continue.");
    Console.ReadKey();
}

var allTasks = allScenarios.Select(scenario => scenario.Run(cancelRunTokenSource.Token));

await Task.WhenAll(allTasks);
