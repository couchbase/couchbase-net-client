using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.Retry
{
    public struct ExponentialBackoff : IBackoffCalculator
    {
        private readonly int _maxRetries;
        private readonly int _delayMillis;
        private readonly int _maxDelayMillis;
        private int _power;

        public ExponentialBackoff(int maxRetries, int delayMillis, int maxDelayMillis)
        {
            _maxRetries = maxRetries;
            _delayMillis = delayMillis;
            _maxDelayMillis = maxDelayMillis;
            _power = 2;
        }

        public Task Delay(IOperation op)
        {
            return Task.Delay(CalculateBackoff(op));
        }

        public TimeSpan CalculateBackoff(IOperation op)
        {
            if (op.Attempts == _maxRetries)
            {
                throw new OperationCanceledException();
            }

            if (op.Attempts < 31)
            {
                _power <<= 1;
            }

            return TimeSpan.FromMilliseconds(Math.Min(_delayMillis * (_power - 1) / 2, _maxDelayMillis));
        }

        public static ExponentialBackoff Create(int maxRetries, int delayMillis, int maxDelayMillis)
        {
            return new ExponentialBackoff(maxRetries, delayMillis, maxDelayMillis);
        }
    }
}
