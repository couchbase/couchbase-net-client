using BenchmarkDotNet.Attributes;
using Couchbase.Core.Logging;

namespace Couchbase.LoadTests.Core.Logging
{
    [MemoryDiagnoser]
    [Config(typeof(StandardConfig))]
    // ReSharper disable once InconsistentNaming
    public class Redactor_Rendered
    {
        private TypedRedactor _redactor;

        [Params(RedactionLevel.None, RedactionLevel.Partial, RedactionLevel.Full)]
        public RedactionLevel Level { get; set; }

        [GlobalSetup(Target = nameof(Baseline))]
        public void BaselineSetup()
        {
            _redactor = new TypedRedactor(Level);
        }

        [Benchmark(Baseline = true)]
        public string Baseline()
        {
            return _redactor.UserData("test").ToString();
        }
    }
}
