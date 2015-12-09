using System;

namespace Couchbase.Core.Diagnostics
{
    public interface IOperationTimer : IDisposable
    {
        ITimingStore Store { get; set; }
    }
}
