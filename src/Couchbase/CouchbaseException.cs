using System;
using System.Collections;
using System.Text;
using Couchbase.Core;
using Newtonsoft.Json.Linq;

namespace Couchbase
{
    /// <summary>
    /// Base exception for all exceptions generated or handled by the Couchbase SDK.
    /// </summary>
    public class CouchbaseException : Exception
    {
        public CouchbaseException() {}

        public CouchbaseException(string message) : base(message) {}

        public CouchbaseException(string message, Exception innerException) : base(message, innerException) {}

        public override string Message
        {
            get
            {
                var message = base.Message;
                if (Data.Count > 0)
                {
                    var contextInfo = new JObject();
                    foreach (DictionaryEntry dictionaryEntry in Data)
                    {
                        contextInfo.Add(new JProperty(dictionaryEntry.Key.ToString(), dictionaryEntry.Value));
                    }

                    var json = contextInfo.ToString();
                    var sb = new StringBuilder(message.Length + json.Length + 3);
                    sb.AppendLine(message);
                    sb.Append(Environment.NewLine);
                    sb.AppendLine("Context Information");
                    sb.AppendLine("--------------------");
                    sb.AppendLine(json);
                    return sb.ToString();
                }

                return message;
            }
        }

        public IErrorContext Context { get; set; }
    }
}
