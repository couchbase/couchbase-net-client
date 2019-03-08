using System.Threading;

namespace Couchbase.Services.Query
{
    /// <summary>
    /// Generates a linear progression of sequence numbers, overlapping if the storage is exceeded.
    /// </summary>
    public static class QuerySequenceGenerator
    {
        private static int _sequenceId;

        /// <summary>
        /// Gets the next sequence in the progression.
        /// </summary>
        /// <returns></returns>
        public static uint GetNext()
        {
            var temp = Interlocked.Increment(ref _sequenceId);
            return (uint)temp;
        }

        /// <summary>
        /// Gets the next sequence in the progression as a <see cref="string"/>.
        /// </summary>
        /// <returns></returns>
        public static string GetNextAsString()
        {
            return GetNext().ToString();
        }

        /// <summary>
        /// Resets the sequence to zero. Mainly for testing.
        /// </summary>
        public static void Reset()
        {
            Interlocked.Exchange(ref _sequenceId, 0);
        }
    }
}
