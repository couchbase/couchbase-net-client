using System;
using System.Linq;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    // The registry holds only weak references, so any instance a test expects to still be tracked must be
    // kept reachable for the whole test. A local variable is not enough on its own: once the JIT sees the
    // last use of a local it may report it as dead, and a collection between that point and the assertion
    // will empty the registry. Every such instance is therefore held in a local and pinned with a
    // GC.KeepAlive placed after the assertions.
    public class WeakInstanceRegistryTests
    {
        private sealed class Tracked
        {
            public Tracked(int value) => Value = value;

            public int Value { get; }
        }

        // A weak reference whose target has already been collected. Constructing one with a null target
        // gives the same observable behaviour (TryGetTarget returns false) without having to provoke the
        // garbage collector, which would make these tests runtime-dependent.
        private static WeakReference<Tracked> Collected() => new(null!);

        [Fact]
        public void EnumerateLive_ReturnsAddedInstances()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var first = new Tracked(1);
            var second = new Tracked(2);

            registry.Add(first);
            registry.Add(second);

            Assert.Equal(new[] { 1, 2 }, registry.EnumerateLive().Select(x => x.Value).OrderBy(x => x));

            GC.KeepAlive(first);
            GC.KeepAlive(second);
        }

        [Fact]
        public void Remove_DropsTheEntry()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var instance = new Tracked(1);

            var id = registry.Add(instance);
            registry.Remove(id);

            // The instance is still reachable, so an empty registry is the result of removal rather than
            // of collection.
            Assert.Empty(registry.EnumerateLive());
            Assert.Equal(0, registry.Count);

            GC.KeepAlive(instance);
        }

        [Fact]
        public void Remove_OnlyDropsTheRequestedEntry()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var removed = new Tracked(1);
            var kept = new Tracked(2);

            var id = registry.Add(removed);
            registry.Add(kept);

            registry.Remove(id);

            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));

            GC.KeepAlive(removed);
            GC.KeepAlive(kept);
        }

        [Fact]
        public void Remove_IsIdempotent()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var instance = new Tracked(1);

            var id = registry.Add(instance);
            registry.Remove(id);
            registry.Remove(id);

            Assert.Equal(0, registry.Count);

            GC.KeepAlive(instance);
        }

        [Fact]
        public void Add_ReusesNoIdentifiers()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var removed = new Tracked(1);
            var kept = new Tracked(2);

            var firstId = registry.Add(removed);
            registry.Remove(firstId);
            var secondId = registry.Add(kept);

            Assert.NotEqual(firstId, secondId);

            // Removing the stale identifier must not evict the entry which followed it.
            registry.Remove(firstId);
            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));

            GC.KeepAlive(removed);
            GC.KeepAlive(kept);
        }

        // This is the regression the registry exists for: the previous ConcurrentBag grew by one entry for
        // every pool and connection ever created and had no way to remove them, so a long-lived process
        // leaked an entry (and a weak GC handle) per connection cycle and paid an O(created) cost on every
        // diagnostics read.
        [Fact]
        public void AddAndRemove_DoesNotGrowWithChurn()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            for (var i = 0; i < 1000; i++)
            {
                var instance = new Tracked(i);
                var id = registry.Add(instance);

                Assert.Equal(1, registry.Count);

                registry.Remove(id);

                GC.KeepAlive(instance);
            }

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void EnumerateLive_SkipsCollectedInstances()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var live = new Tracked(2);

            registry.AddWeak(Collected());
            registry.Add(live);
            registry.AddWeak(Collected());

            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));

            GC.KeepAlive(live);
        }

        // The weak references are the safety net for instances abandoned without being disposed. Those
        // entries must not accumulate either, so enumerating has to prune them.
        [Fact]
        public void EnumerateLive_PrunesCollectedInstances()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var live = new Tracked(3);

            registry.AddWeak(Collected());
            registry.AddWeak(Collected());
            registry.Add(live);

            Assert.Equal(3, registry.Count);

            // Enumerate fully; the pruning happens as each dead entry is passed over.
            _ = registry.EnumerateLive().ToList();

            Assert.Equal(1, registry.Count);

            GC.KeepAlive(live);
        }

        [Fact]
        public void EnumerateLive_IsEmpty_ForNewRegistry()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            Assert.Empty(registry.EnumerateLive());
            Assert.Equal(0, registry.Count);
        }

        // Identifiers start at 1, so the default value of a field holding one is not a valid entry. A pool
        // which was never tracked can therefore call Remove safely.
        [Fact]
        public void Remove_DefaultIdentifier_DoesNotEvictAnything()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var instance = new Tracked(1);

            registry.Add(instance);
            registry.Remove(default);

            Assert.Equal(new[] { 1 }, registry.EnumerateLive().Select(x => x.Value));

            GC.KeepAlive(instance);
        }
    }
}
