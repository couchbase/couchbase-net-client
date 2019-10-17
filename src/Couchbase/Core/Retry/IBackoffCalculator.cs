using System;
using System.Threading.Tasks;
using Couchbase.Core.IO.Operations.Legacy;

namespace Couchbase.Core.Retry
{
    public interface IBackoffCalculator
    {
        Task Delay(IOperation operation);

        TimeSpan CalculateBackoff(IOperation op);
    }
}
