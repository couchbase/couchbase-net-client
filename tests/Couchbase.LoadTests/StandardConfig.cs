using System;
using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Exporters;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Loggers;
using BenchmarkDotNet.Toolchains.CsProj;

namespace Couchbase.LoadTests
{
    public class StandardConfig : ManualConfig
    {
        public StandardConfig()
        {
            Add(DefaultColumnProviders.Instance);
            Add(RankColumn.Arabic);

            Add(DefaultExporters.CsvMeasurements);
            Add(DefaultExporters.Csv);
            Add(DefaultExporters.RPlot);
            Add(DefaultExporters.Markdown);
            Add(DefaultExporters.Html);

            Add(ConsoleLogger.Default);
        }
    }
}
