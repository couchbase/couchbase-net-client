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
            AddColumnProvider(DefaultColumnProviders.Instance);
            AddColumn(RankColumn.Arabic);

            AddExporter(DefaultExporters.CsvMeasurements);
            AddExporter(DefaultExporters.Csv);
            AddExporter(DefaultExporters.RPlot);
            AddExporter(DefaultExporters.Markdown);
            AddExporter(DefaultExporters.Html);

            AddLogger(ConsoleLogger.Default);
        }
    }
}
