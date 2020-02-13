#nullable enable

namespace Couchbase.Core.IO.Connections
{
    /// <summary>
    /// Creates a new <see cref="IConnectionPoolScaleController"/>.
    /// </summary>
    internal interface IConnectionPoolScaleControllerFactory
    {
        /// <summary>
        /// Creates a new <see cref="IConnectionPoolScaleController"/>.
        /// </summary>
        /// <returns>A new <see cref="IConnectionPoolScaleController"/>.</returns>
        IConnectionPoolScaleController Create();
    }
}
