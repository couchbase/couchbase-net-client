using System;


namespace Couchbase
{
    /// <summary>
    /// Basic operation return value
    /// </summary>
    public class DefaultResult<T> : DefaultResult, IResult<T>
    {
        public DefaultResult()
        {
        }

        public DefaultResult(bool success, string message, Exception exception)
            : base(success, message, exception)
        {
        }

        public T Value{ get; internal set; }
    }
}
