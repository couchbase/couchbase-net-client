using System;
using System.Threading.Tasks;

namespace Couchbase.Core.Retry
{
    public interface IBackoffCalculator
    {
        Task Delay(IRequest request);

        TimeSpan CalculateBackoff(IRequest request);
    }
}
