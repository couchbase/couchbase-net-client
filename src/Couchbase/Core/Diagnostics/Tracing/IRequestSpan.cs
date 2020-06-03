using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A generic wrapper around a span.  Finish() and Dispose() should be equivalent.
    /// </summary>
    /// <remarks>Volatile.  (This interface may change in breaking ways during minor releases)</remarks>
    public interface IRequestSpan : IDisposable
    {
        void Finish();
    }
}
