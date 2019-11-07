using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations;


namespace Couchbase.Core.Retry
{
    public struct ControlledBackoff : IBackoffCalculator
    {
        public Task Delay(IOperation operation)
        {
            return Task.Delay(CalculateBackoff(operation));
        }

        public TimeSpan CalculateBackoff(IOperation operation)
        {
            switch (operation.Attempts)
            {
                case 0:
                    return TimeSpan.FromMilliseconds(1);
                case 1:
                    return TimeSpan.FromMilliseconds(10);
                case 2:
                    return TimeSpan.FromMilliseconds(50);
                case 3:
                    return TimeSpan.FromMilliseconds(100);
                case 4:
                    return TimeSpan.FromMilliseconds(500);
                default:
                    return TimeSpan.FromMilliseconds(1000);
            }
        }

        public static ControlledBackoff Create()
        {
            return new ControlledBackoff();
        }
    }
}
