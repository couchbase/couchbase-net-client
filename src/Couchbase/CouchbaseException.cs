using System;
using System.Text;
using Couchbase.Core;
using Newtonsoft.Json;

namespace Couchbase
{
    /// <summary>
    /// Base exception for all exceptions generated or handled by the Couchbase SDK.
    /// </summary>
    public class CouchbaseException : Exception
    {
        public CouchbaseException() { }

        public CouchbaseException(IErrorContext context)
        {
            Context = context;
        }

        public CouchbaseException(string message) : base(message) {}

        public CouchbaseException(string message, Exception innerException) : base(message, innerException) {}

        public IErrorContext Context { get; set; }

        internal bool IsReadOnly { get; set; }

        public override string ToString()
        {
            var sb = new StringBuilder();
            sb.AppendLine(base.ToString());
            sb.AppendLine("-----------------------Context Info---------------------------");
            sb.AppendLine(JsonConvert.SerializeObject(Context));
            return sb.ToString();
        }
    }
}
