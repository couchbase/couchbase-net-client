using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Couchbase.Utils
{
    /// <summary>
    /// Temporarily removes the <see cref="SynchronizationContext"/> from the current thread, replacing it once
    /// the object is disposed.
    /// </summary>
    /// <remarks>
    /// This is designed to help prevent deadlocks when synchronously waiting on an asynchronous task,
    /// as in http://blogs.msdn.com/b/pfxteam/archive/2012/04/13/10293638.aspx.  This class is designed
    /// to be used with a "using" clause for simplicity and to guarantee that the context is replaced even
    /// if there is an exception.
    /// </remarks>
    internal class SynchronizationContextExclusion : IDisposable
    {
        private bool _disposed = false;
        private readonly SynchronizationContext _cachedContext;

        public SynchronizationContextExclusion()
        {
            _cachedContext = SynchronizationContext.Current;
            SynchronizationContext.SetSynchronizationContext(null);
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                if (_cachedContext != null)
                {
                    SynchronizationContext.SetSynchronizationContext(_cachedContext);
                }

                _disposed = true;
            }
        }
    }
}
