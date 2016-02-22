﻿using System;
using Couchbase.Core;

namespace Couchbase
{
    internal interface IRefCountable
    {
        /// <summary>
        /// Increments the reference counter for this <see cref="IBucket"/> instance.
        /// </summary>
        /// <returns>The current count of all <see cref="IBucket"/> references, or -1 if a reference could not be added because the bucket is disposed.</returns>
        int AddRef();

        /// <summary>
        /// Decrements the reference counter and calls <see cref="IDisposable.Dispose"/> if the count is zero.
        /// </summary>
        /// <returns></returns>
        int Release();
    }
}
