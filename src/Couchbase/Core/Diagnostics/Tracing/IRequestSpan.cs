#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A generic wrapper around an <see cref="Activity" /> to enable the <c>using</c> pattern.  Finish() and Dispose() should be equivalent.
    /// </summary>
    /// <remarks>Volatile.  (This interface may change in breaking ways during minor releases)</remarks>
    public interface IRequestSpan : IDisposable
    {
        Activity? Activity { get; }
    }
}
