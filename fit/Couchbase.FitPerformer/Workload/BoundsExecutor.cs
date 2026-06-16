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
        protected Counter counter;

        public BoundsCounterBased(Counter counter)
        {
            this.counter = counter;
        }

        public override bool CanExecute()
        {
            return counter.DecrementAndGet() >= 0;
        }
    }

    internal class BoundsCounterEquals : BoundsCounterBased
    {
        private readonly int _initialCounterValue;

        public BoundsCounterEquals(Counter counter) : base(counter)
        {
            _initialCounterValue = counter.Get();
        }

        public override bool CanExecute()
        {
            return counter.Get() == _initialCounterValue;
        }
    }

    internal class BoundsForTime : BoundsExecutor
    {
        private Stopwatch _start = new Stopwatch();
        private int _untilSeconds;

        public BoundsForTime(int untilSeconds)
        {
            _start.Start();
            _untilSeconds = untilSeconds;
        }

        public override bool CanExecute()
        {
            // Serilog.Log.Information("Time {V} vs {X}", start.Elapsed.TotalSeconds, untilSeconds);
            return _start.Elapsed.TotalSeconds < _untilSeconds;
        }
    }
}
