using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace Couchbase.FitPerformer.Workload
{
    internal abstract class BoundsExecutor
    {
        // Returns whether a command may be executed.
        public abstract Boolean CanExecute();
    }

    internal class BoundsCounterBased : BoundsExecutor
    {
        private Counter counter;

        public BoundsCounterBased(Counter counter)
        {
            this.counter = counter;
        }

        public override bool CanExecute()
        {
            return counter.DecrementAndGet() >= 0;
        }
    }

    internal class BoundsForTime : BoundsExecutor
    {
        private Stopwatch start = new Stopwatch();
        private int untilSeconds;

        public BoundsForTime(int untilSeconds)
        {
            start.Start();
            this.untilSeconds = untilSeconds;
        }

        public override bool CanExecute()
        {
            // Serilog.Log.Information("Time {V} vs {X}", start.Elapsed.TotalSeconds, untilSeconds);
            return start.Elapsed.TotalSeconds < untilSeconds;
        }
    }
}
