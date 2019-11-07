using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Errors;
using Newtonsoft.Json;

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Represents an error that occurs while performing a Key/Value operation while using the Memcached Protocol and/or related services.
    /// </summary>
    public class KeyValueException : CouchbaseException
    {
        public KeyValueException()
        {
        }

        public KeyValueException(string message)
            : base(message)
        {
        }

        public KeyValueException(string message, Exception innerException)
            : base(message, innerException)
        {
        }

        public ResponseStatus Status { get; internal set; }

        public string Key { get; internal set; }

        public string Context { get; internal set; }

        //TODO possibly remove - redundent with Context
        public ErrorCode ErrorCode { get; internal set; }

        public static KeyValueException Create(ResponseStatus status, Exception innerException = null, string message = null, ErrorCode errorCode = null, string Key = null) => new KeyValueException(message, innerException)
        {
            ErrorCode = errorCode,
            Status = status,
            Key = Key,
            Context = errorCode == null ? null : JsonConvert.SerializeObject(errorCode)
        };

        public override string ToString()
        {
            return Context;
        }
    }
}
