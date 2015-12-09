using System.Threading;

namespace Couchbase.IO.Operations
{
    public static class SequenceGenerator
    {
        private static int _sequenceId;

        public static uint GetNext()
        {
            var temp = Interlocked.Increment(ref _sequenceId);
            return (uint)temp;
        }

        public static void Reset()
        {
            Interlocked.Exchange(ref _sequenceId, 0);
        }
    }
}
