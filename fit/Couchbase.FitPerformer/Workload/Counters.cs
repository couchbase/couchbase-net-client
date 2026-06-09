using System.Collections.Generic;
using System.Threading;
using System;

public class Counter {
    private int _count = 0;

    public Counter(int initial) {
        this._count = initial;
    }

    public int IncrementAndGet()
    {
        return Interlocked.Increment(ref _count);
    }

    public int DecrementAndGet()
    {
        return Interlocked.Decrement(ref _count);
    }

    public int Get() {
        return _count;
    }
}

public class Counters {
    private Dictionary<string, Counter> _counters = new Dictionary<string, Counter>();

    public Counter GetCounter(string counterId, int initialCount) {
        lock (_counters) {
            if (_counters.ContainsKey(counterId)) {
                return _counters[counterId];
            }
            var counter = new Counter(initialCount);
            _counters[counterId] = counter;
            return counter;
        }
    }

    public Counter GetCounter(Couchbase.Grpc.Protocol.Shared.Counter counter) {
        if (counter.CounterCase == Couchbase.Grpc.Protocol.Shared.Counter.CounterOneofCase.Global) {
            return GetCounter(counter.CounterId, counter.Global.Count);
        }
        throw new NotSupportedException();
    }
}