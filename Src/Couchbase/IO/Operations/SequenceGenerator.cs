using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

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
