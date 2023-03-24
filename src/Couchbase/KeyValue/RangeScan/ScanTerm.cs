using System.Text;

namespace Couchbase.KeyValue.RangeScan
{
    /// <summary>
    ///  A single <see cref="ScanTerm"/>identifying either the point to scan from or to scan to
    ///  when performing a Range Scan.
    /// </summary>
    public sealed class ScanTerm
    {
        private static readonly byte[] MinimumPattern = { 0x00 };
        private static readonly byte[] MaximumPattern = { 0xFF };

        private ScanTerm(byte[] id, bool exclusive)
        {
            Id = id;
            IsExclusive = exclusive;
        }

        /// <summary>
        /// The term to scan.
        /// </summary>
        public byte[] Id { get; private set; }

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
            return Exclusive(Encoding.UTF8.GetBytes(id));
        }

        /// <summary>
        /// Creates an exclusive scan for a term.
        /// </summary>
        /// <param name="id">The term to scan.</param>
        /// <returns>A <see cref="ScanTerm"/> instance for an exclusive scan.</returns>
        public static ScanTerm Exclusive(byte[] id)
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
            return new ScanTerm(Encoding.UTF8.GetBytes(id), false);
        }

        /// <summary>
        /// Creates an Inclusive scan for a term.
        /// </summary>
        /// <param name="id">The term to scan.</param>
        /// <returns>A <see cref="ScanTerm"/> instance for an inclusive scan.</returns>
        public static ScanTerm Inclusive(byte[] id)
        {
            return new ScanTerm(id, false);
        }

        /// <summary>
        /// Creates an Exclusive scan for the minimum term 0x00.
        /// </summary>
        /// <returns>A <see cref="ScanTerm"/> instance for an exclusive scan.</returns>
        public static ScanTerm Minimum()
        {
            return Inclusive(MinimumPattern);
        }

        /// <summary>
        /// Creates an Exclusive scan for the maximum term 0xFF.
        /// </summary>
        /// <returns>A <see cref="ScanTerm"/> instance for an exclusive scan.</returns>
        public static ScanTerm Maximum()
        {
            return Inclusive(MaximumPattern);
        }
    }
}
