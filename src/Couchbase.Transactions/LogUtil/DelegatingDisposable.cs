using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Transactions.LogUtil
{
    /// <summary>
    /// A utility class for chaining disposables together into a single `using` statement.
    /// </summary>
    /// <typeparam name="T">The type of the significant root item.</typeparam>
    internal class DelegatingDisposable<T> : IDisposable where T : IDisposable
    {
        private readonly IDisposable? _delegated;

        public T? Item { get; }

        public DelegatingDisposable(T? item, IDisposable delegated)
        {
            Item = item;
            _delegated = delegated;
        }

        public void Dispose()
        {
            try
            {
                Item?.Dispose();
            }
            finally
            {
                _delegated?.Dispose();
            }
        }
    }
}
