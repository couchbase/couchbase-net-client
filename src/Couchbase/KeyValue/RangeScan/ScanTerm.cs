using System.Text;
using Couchbase.Core.Compatibility;
using Couchbase.Utils;

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    ///  A single <see cref="ScanTerm"/>identifying either the point to scan from or to scan to
    ///  when performing a Range Scan.
    /// </summary>
    [InterfaceStability(Level.Volatile)]
    public sealed class ScanTerm
    {
        internal static readonly ScanTerm Minimum = new(CouchbaseStrings.MinimumPattern, false);
        internal static readonly ScanTerm Maximum = new(CouchbaseStrings.MaximumPattern, false);

        private ScanTerm(string id, bool exclusive)
        {
            Id = id;
            IsExclusive = exclusive;
        }

        internal byte[] ByteId => Encoding.UTF8.GetBytes(Id);

        /// <summary>
        /// The term to scan.
        /// </summary>
        public string Id { get; private set; }

        /// <summary>
        /// Controls whether the scan is inclusive or exclusive.
        /// </summary>
        public bool IsExclusive { get; private set; }

        /// <summary>
        /// Creates an exclusive scan for a term.
        /// </summary>
        /// <param name="id">The term to scan.</param>
        /// <returns>A <see cref="ScanTerm"/> instance for an exclusive scan.</returns>
        public static ScanTerm Exclusive(string id)
        {
            return new ScanTerm(id, true);
        }

        /// <summary>
        /// Creates an Inclusive scan for a term.
        /// </summary>
        /// <param name="id">The term to scan.</param>
        /// <returns>A <see cref="ScanTerm"/> instance for an inclusive scan.</returns>
        public static ScanTerm Inclusive(string id)
        {
            return new ScanTerm(id, false);
        }
    }
}
