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
        /// Creates <see cref="IScope"/> populated with <see cref="ICollection"/> based
        /// on a <see cref="Manifest"/>.
        /// </summary>
        /// <param name="bucket">The owning <see cref="BucketBase"/>.</param>
        /// <param name="manifest">The <see cref="Manifest"/>.</param>
        /// <returns>Scopes created from the manifest.</returns>
        IEnumerable<IScope> CreateScopes(BucketBase bucket, Manifest manifest);

        /// <summary>
        /// Creates the default scope and collection.
        /// </summary>
        /// <param name="bucket">The owning <see cref="BucketBase"/>.</param>
        /// <returns>The defaults scope and collection.</returns>
        IScope CreateDefaultScope(BucketBase bucket);
    }
}
