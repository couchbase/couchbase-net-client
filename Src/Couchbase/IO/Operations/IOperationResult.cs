
using Couchbase.Configuration.Server.Serialization;

namespace Couchbase.IO.Operations
{
    /// <summary>
    /// The result of an operation.
    /// </summary>
    /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
    /// <typeparam name="T"></typeparam>
    public interface IOperationResult<out T>
    {
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        bool Success { get; }

        /// <summary>
        /// The value of the key retrieved from Couchbase Server.
        /// </summary>
        T Value { get; }

        /// <summary>
        /// If Success is false, the reasom why the operation failed.
        /// </summary>
        string Message { get; }

        /// <summary>
        /// The status returned from the Couchbase Server after an operation.
        /// </summary>
        /// <remarks><see cref="ResponseStatus.Success"/> will be returned if <see cref="Success"/> 
        /// is true, otherwise <see cref="Success"/> will be false. If <see cref="ResponseStatus.ClientFailure"/> is
        /// returned, then the operation failed before being sent to the Couchbase Server.</remarks>
        ResponseStatus Status { get; }

        /// <summary>
        /// The Check-and-swap value for a given key or document.
        /// </summary>
        ulong Cas { get; }
    }
}
