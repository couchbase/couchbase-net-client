using System;
using System.Threading;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

namespace Couchbase.LoadTests.Core.IO.Operations
{
    [MemoryDiagnoser]
    public class CalculationTokenPairSourceTests
    {
        private readonly CancellationTokenSource _externalToken = new();
        private readonly IOperation _operation = new Get<object>();

        private static readonly TimeSpan _timeout = TimeSpan.FromMilliseconds(2500);

        [Benchmark]
        public void RentRegisterReturn()
        {
            var source = CancellationTokenPairSourcePool.Shared.Rent(_timeout, _externalToken.Token);
            var tokenPair = source.TokenPair;

            try
            {
                using var _ = new OperationCancellationRegistration(_operation, tokenPair);
            }
            finally
            {
                CancellationTokenPairSourcePool.Shared.Return(source);
            }
        }
    }
}
