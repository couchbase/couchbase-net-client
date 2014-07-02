using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Couchbase.IO;

namespace Couchbase
{
    public interface IResult
    {
        /// <summary>
        /// True if the operation succeeded.
        /// </summary>
        bool Success { get; }

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
    }
}
