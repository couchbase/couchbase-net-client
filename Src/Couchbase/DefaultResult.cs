using System;

namespace Couchbase
{
    /// <summary>
    /// Basic operation return value
    /// </summary>
    public class DefaultResult : IResult
    {
        public DefaultResult()
        {
        }
        public DefaultResult(bool success, string message, Exception exception)
        {
            Success = success;
            Message = message;
            Exception = exception;
        }

        /// <summary>
        /// Returns true if the operation was succesful.
        /// </summary>
        /// <remarks>If Success is false, use the Message property to help determine the reason.</remarks>
        public bool Success { get; internal set; }

        /// <summary>
        /// If the operation wasn't succesful, a message indicating why it was not succesful.
        /// </summary>
        public string Message { get; internal set; }

        /// <summary>
        /// If Success is false and an exception has been caught internally, this field will contain the exception.
        /// </summary>
        public Exception Exception { get; internal set; }

        public bool ShouldRetry()
        {
            throw new NotImplementedException();
        }
    }
}
