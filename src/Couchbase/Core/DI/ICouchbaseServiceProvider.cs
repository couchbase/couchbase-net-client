using System;

namespace Couchbase.Core.DI
{
    /// <summary>
    /// Extends <see cref="IServiceProvider"/> with a method to test for service registration.
    /// </summary>
    internal interface ICouchbaseServiceProvider : IServiceProvider
    {
        /// <summary>
        /// Determines if the specified service type is available from the <see cref="ICouchbaseServiceProvider"/>.
        /// </summary>
        /// <param name="serviceType">An object that specifies the type of service object to test.</param>
        /// <returns>true if the specified service is a available, false if it is not.</returns>
        bool IsService(Type serviceType);
    }
}
