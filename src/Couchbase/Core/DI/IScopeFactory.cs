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
        /// Creates <see cref="IScope"/> populated with <see cref="ICouchbaseCollection"/> based
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

        /// <summary>
        /// Creates a <see cref="Scope"/> given a name and identifier.
        /// </summary>
        /// <param name="name">The name of the scope from the application.</param>
        /// <param name="scopeIdentifier">The scope unique identifier from the server.</param>
        /// <param name="bucket">The owning <see cref="BucketBase"/>.</param>
        /// <returns></returns>
        IScope CreateScope(string name, string scopeIdentifier, BucketBase bucket);
    }
}
