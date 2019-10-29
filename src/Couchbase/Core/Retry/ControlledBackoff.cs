using System;
using System.Threading.Tasks;

namespace Couchbase.Core.Retry
{
    public struct ControlledBackoff : IBackoffCalculator
    {
        public Task Delay(IRequest request)
        {
            return Task.Delay(CalculateBackoff(request), request.Token);
        }

        public TimeSpan CalculateBackoff(IRequest request)
        {
            switch (request.Attempts)
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
