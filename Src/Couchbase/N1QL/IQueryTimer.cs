using System;
using Couchbase.Core.Diagnostics;

namespace Couchbase.N1QL
{
    public interface IQueryTimer : IDisposable
    {
        ITimingStore Store { get; }
    }
}