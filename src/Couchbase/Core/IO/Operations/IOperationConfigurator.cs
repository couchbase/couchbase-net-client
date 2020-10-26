using Couchbase.KeyValue;

#nullable enable

namespace Couchbase.Core.IO.Operations
{
    /// <summary>
    /// Configures an operation with services it requires before sending.
    /// </summary>
    internal interface IOperationConfigurator
    {
        /// <summary>
        /// Apply configuration to a key/value operation.
        /// </summary>
        /// <param name="operation">The operation to configure.</param>
        /// <param name="options">Options for the key/value operation.</param>
        void Configure(OperationBase operation, IKeyValueOptions? options = null);
    }
}
