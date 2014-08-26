using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase
{
    /// <summary>
    /// Provides an interface for interacting with documents within Couchbase Server
    /// </summary>
    /// <typeparam name="T">The type of document.</typeparam>
    public class Document<T> : IDocument<T>
    {
        /// <summary>
        /// The unique identifier for the document
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// The "Check and Set" value for enforcing optimistic concurrency
        /// </summary>
        public ulong Cas { get; set; }

        /// <summary>
        /// The time-to-live or TTL for the document before it's evicated from disk
        /// </summary>
        /// <remarks>Setting this to zero or less will give the document infinite lifetime</remarks>
        public uint Expiry { get; set; }

        /// <summary>
        /// The value representing the document itself
        /// </summary>
        public T Value { get; set; }
    }
}
