using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommandLine;
using Serilog.Events;

namespace Couchbase.ConcurrencyTests
{
    internal class CommandLineOptions
    {
        [Option('w', longName: "wait",
            Default = false,
            HelpText = "Wait for keypress after startup, to allow `dotnet counters` to monitor the process.")]
        public bool WaitForCounters { get; set; } = false;

        [Option('s', longName: "scenario",
            Default = new string[] { "ping,50" },
            HelpText = "Scenario to include, in the form of \"scenarioName,n\".  Can be specified multiple times. (e.g. \"ping,50\", \"getDocument,500\", \"crud,100\")")]
        public IEnumerable<string> Scenarios { get; set; } = new string[] { "ping,50" };

        [Option('l', longName: "logLevel",
            Default = LogEventLevel.Warning,
            HelpText = "The logging level.")]
        public LogEventLevel LogLevel { get; set; } = LogEventLevel.Warning;

        [Option('t', longName: "time",
            Default = null,
            HelpText = "Length of time to run in TimeSpan format.")]
        public TimeSpan? Time { get; set; } = null;

        public override string ToString() => $"{nameof(WaitForCounters)}={WaitForCounters}, {nameof(Scenarios)}={Scenarios}, {nameof(LogLevel)}={LogLevel}, {nameof(Time)}={Time}";
    }
}
