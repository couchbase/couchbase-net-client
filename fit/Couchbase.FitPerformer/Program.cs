#nullable enable
using System;
using System.Linq;
using System.Threading.Tasks;
using Couchbase.Core.Exceptions;
using Couchbase.Grpc.Protocol;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Serilog;
using System.Threading;
using System.Diagnostics;
using Couchbase.FitPerformer.Utils;
using Serilog.Extensions.Logging;
using Couchbase.Client.Transactions;
namespace Couchbase.FitPerformer
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var port = 8060;
            var hostname = "0.0.0.0";
            // ReSharper disable once StringLiteralTypo
            var logLevel = Environment.GetEnvironmentVariable("FIT_LOG_LEVEL")?.Parse() ?? LogLevel.Information;

            string? logFile = null;
            var includeClientLogs = false;
            var disableConsoleRead = false;

            // ReSharper disable once CommentTypo
            //Need to send parameters in format : port=8060 version=1.1.0 loglevel=all:Info
            foreach (var arg in args)
            {
                var parameter = arg.Split("=");
                if (parameter.Length != 2) throw new InvalidArgumentException($"Malformed argument key/value pair: {arg}");
                switch (parameter[0])
                {
                    // ReSharper disable once StringLiteralTypo
                    case "loglevel":
                        logLevel = parameter[1].Split(':')[1].Parse() ?? LogLevel.Information;
                        break;
                    case "port":
                        port = Convert.ToInt32(parameter[1]);
                        break;
                    case "hostname":
                        hostname = parameter[1];
                        break;
                    case "logfile":
                        logFile = parameter[1];
                        break;
                    case "includeClientLogs":
                        includeClientLogs = bool.Parse(parameter[1]);
                        break;
                    case "disableConsoleRead":
                        disableConsoleRead = bool.Parse(parameter[1]);
                        break;
                    default:
                        Console.Out.WriteLine("Unrecognized option: " + parameter[0]);
                        break;
                }
            }

            var outputTemplate = "[{Timestamp:HH:mm:ss.fff} {Level:u3}] [{SourceContext:l}] {Message:lj} {Properties:j}{NewLine}{Exception}";
            var loggerConfig = new LoggerConfiguration()
                .Enrich.FromLogContext()
                .WriteTo.Console(outputTemplate: outputTemplate);

            if (logFile != null)
            {
                loggerConfig.WriteTo.File(logFile, outputTemplate: outputTemplate);
            }

            switch (logLevel)
            {
                case LogLevel.Trace:
                    includeClientLogs = true;
                    loggerConfig = loggerConfig.MinimumLevel.Verbose();
                    break;
                case LogLevel.Debug:
                    loggerConfig = loggerConfig.MinimumLevel.Debug();
                    break;
                case LogLevel.Information:
                    loggerConfig = loggerConfig.MinimumLevel.Information();
                    break;
                case LogLevel.Warning:
                    loggerConfig = loggerConfig.MinimumLevel.Warning();
                    break;
                case LogLevel.Error:
                case LogLevel.Critical:
                    loggerConfig = loggerConfig.MinimumLevel.Error();
                    break;
                case LogLevel.None:
                    break;
                default:
                    loggerConfig = loggerConfig.MinimumLevel.Debug();
                    break;
            }

            if (logLevel != LogLevel.None)
            {
                Serilog.Log.Logger = loggerConfig.CreateLogger();
            }

            Serilog.Log.Information("Using Properties Port: {Port}, LogLevel: {LogLevel}", port, logLevel);
            Serilog.Log.Information("Transactions Protocol Version: {V}", ProtocolVersion.SupportedVersion);
            Serilog.Log.Information(".NET SDK Version: {Version}", typeof(Cluster).Assembly.GetName().Version?.ToString() ?? "Unknown");

            // Accuracy of timing info is crucial to the performance testing
            var delayInTicks = TimeSpan.FromSeconds(0.1).Ticks;
            {
                var sw = Stopwatch.StartNew();
                var instantTicks = sw.ElapsedTicks;
                while (sw.ElapsedTicks < delayInTicks) { }
                sw.Stop();
                var afterDelayTicks = sw.ElapsedTicks;
                Serilog.Log.Information("High Precision Timer (Stopwatch.ElapsedTicks): after instant = {X}ns after 0.1 s = {Y}ns ({Z}ns)",
                    TimeExtensions.TicksToNanos(instantTicks),
                    TimeExtensions.TicksToNanos(afterDelayTicks),
                    sw.Elapsed.CalculateNanos());
            }

            foreach (var extension in ProtocolVersion.ExtensionsSupported().OrderBy(s => s.PascalCase))
            {
                if (Enum.TryParse<Grpc.Protocol.Transactions.Caps>(extension.PascalCase, out var cap))
                {
                    Serilog.Log.Information("Extension: {Ext}", cap);
                }
                else
                {
                    Serilog.Log.Warning("Unknown extension: {Ext}", cap);
                }
            }

            //start the gRPC fit-performer service
            ILoggerFactory? loggerFactory = includeClientLogs ? new SerilogLoggerFactory(Log.Logger, dispose: false) : null;
            var server = new Server
            {
                Services =
                {
                    PerformerService.BindService(
                        new PerformerServiceImpl(loggerFactory)),
                },
                Ports = { new ServerPort(hostname, port, ServerCredentials.Insecure) }
            };

            server.Start();
            Serilog.Log.Information("🚀.NET FIT-Performer started and ready to roll 🚀 (Ctrl-L to clear console. To stop press any other key)");

            //Sanity check, to easily see if the Rider run config auto-set to Debug for some reason.
#if DEBUG
            Serilog.Log.Information("Running in DEBUG mode");
#else
            Serilog.Log.Information("Running in RELEASE mode");
#endif

            do
            {
                if (!disableConsoleRead)
                {
                    var key = Console.ReadKey();
                    if (key.Modifiers == ConsoleModifiers.Control)
                    {
                        if (key.Key == ConsoleKey.L)
                        {
                            Console.Clear();
                            continue;
                        }
                    }

                    break;
                }

                await Task.Delay(10);
            } while (true);


            await server.ShutdownAsync().ConfigureAwait(false);

            Serilog.Log.Information(".NET FIT-Performer shutting down");
        }
    }
}
