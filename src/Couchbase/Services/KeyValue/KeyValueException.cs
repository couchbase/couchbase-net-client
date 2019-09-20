using System;
using Couchbase.Core.IO.Operations;
using Couchbase.Core.IO.Operations.Legacy.Errors;
using Newtonsoft.Json;

namespace Couchbase.Services.KeyValue
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
        public ErrorMap ErrorMap { get; internal set; }

        public static KeyValueException Create(ResponseStatus status, Exception innerException = null, string message = null, ErrorMap errorMap = null, string Key = null) => new KeyValueException(message, innerException)
        {
            ErrorMap = errorMap,
            Status = status,
            Key = Key,
            Context = errorMap == null ? null : JsonConvert.SerializeObject(errorMap)
        };

        public override string ToString()
        {
            return Context;
        }
    }
}
