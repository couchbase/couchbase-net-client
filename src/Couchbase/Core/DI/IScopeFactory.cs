using System;
using System.Collections.Generic;
using Couchbase.Core.Configuration.Server;
using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Creates new <see cref="IScope"/> implementations.
    /// </summary>
    internal interface IScopeFactory
    {
        /// <summary>
        /// Creates a <see cref="Scope"/> given a name and identifier.
        /// </summary>
        /// <param name="name">The name of the scope from the application.</param>
        /// <param name="bucket">The owning <see cref="BucketBase"/>.</param>
        /// <returns></returns>
        IScope CreateScope(string name, BucketBase bucket);
    }
}
