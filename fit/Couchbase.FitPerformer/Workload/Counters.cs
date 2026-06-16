using System;
using System.Collections.Concurrent;
using System.Threading;

namespace Couchbase.FitPerformer.Workload;

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
        return Volatile.Read(ref _count);
    }

    public void Set(int newCount) {
        Interlocked.Exchange(ref _count, newCount);
    }
}

public class Counters {
    private readonly ConcurrentDictionary<string, Counter> _counters = new ConcurrentDictionary<string, Counter>();

    public Counter GetCounter(string counterId, int initialCount) {
        return _counters.GetOrAdd(counterId, _ => new Counter(initialCount));
    }

    public Counter GetCounter(Couchbase.Grpc.Protocol.Shared.Counter counter) {
        if (counter.CounterCase == Couchbase.Grpc.Protocol.Shared.Counter.CounterOneofCase.Global) {
            return GetCounter(counter.CounterId, counter.Global.Count);
        }
        throw new NotSupportedException();
    }

    public void SetCounter(string counterId, int newCount) {
        if (!_counters.TryGetValue(counterId, out var counter)) {
            throw new NotSupportedException($"Counter does not exist: {counterId}");
        }
        counter.Set(newCount);
    }

    public void ClearCounters() {
        _counters.Clear();
    }
}
