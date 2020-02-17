using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

#nullable enable

namespace Couchbase.Core.Bootstrapping
{
    /// <summary>
    /// Flags a resource for monitoring it's bootstrapped state.
    /// </summary>
    internal interface IBootstrappable
    {
        /// <summary>
        /// Starts the bootstrapping process if <see cref="IsBootstrapped"/> is false.
        /// </summary>
        /// <returns></returns>
        Task BootStrapAsync();

        /// <summary>
        /// True if bootstrapped; otherwise false.
        /// </summary>
        bool IsBootstrapped { get; }

        /// <summary>
        /// The last exception thrown by the bootstrapping process.
        /// </summary>
        List<Exception> DeferredExceptions { get; }
    }
}
