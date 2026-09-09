using BenchmarkDotNet.Attributes;
using Couchbase.Core.Logging;

namespace Couchbase.LoadTests.Core.Logging
{
    [MemoryDiagnoser]
    [Config(typeof(StandardConfig))]
    // ReSharper disable once InconsistentNaming
    public class Redactor_Unrendered
    {
        private Redactor _redactor;

        [Params(RedactionLevel.None, RedactionLevel.Partial, RedactionLevel.Full)]
        public RedactionLevel Level { get; set; }

        [GlobalSetup(Target = nameof(Baseline))]
        public void BaselineSetup()
        {
            _redactor = new Redactor(Level);
        }

        [Benchmark(Baseline = true)]
        public object Baseline()
        {
            return _redactor.UserData("test");
        }
    }
}
