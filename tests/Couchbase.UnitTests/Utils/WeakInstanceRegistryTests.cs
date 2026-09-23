using System;
using System.Linq;
using Couchbase.Utils;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
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
        }

        [Fact]
        public void Remove_DropsTheEntry()
        {
            var registry = new WeakInstanceRegistry<Tracked>();
            var instance = new Tracked(1);

            var id = registry.Add(instance);
            registry.Remove(id);

            Assert.Empty(registry.EnumerateLive());
            Assert.Equal(0, registry.Count);

            // The instance is still reachable, so this is removal, not collection.
            Assert.Equal(1, instance.Value);
        }

        [Fact]
        public void Remove_OnlyDropsTheRequestedEntry()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            var id = registry.Add(new Tracked(1));
            registry.Add(new Tracked(2));

            registry.Remove(id);

            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));
        }

        [Fact]
        public void Remove_IsIdempotent()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            var id = registry.Add(new Tracked(1));
            registry.Remove(id);
            registry.Remove(id);

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void Add_ReusesNoIdentifiers()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            var first = registry.Add(new Tracked(1));
            registry.Remove(first);
            var second = registry.Add(new Tracked(2));

            Assert.NotEqual(first, second);

            // Removing the stale identifier must not evict the entry which followed it.
            registry.Remove(first);
            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));
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
            }

            Assert.Equal(0, registry.Count);
        }

        [Fact]
        public void EnumerateLive_SkipsCollectedInstances()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            registry.AddWeak(Collected());
            registry.Add(new Tracked(2));
            registry.AddWeak(Collected());

            Assert.Equal(new[] { 2 }, registry.EnumerateLive().Select(x => x.Value));
        }

        // The weak references are the safety net for instances abandoned without being disposed. Those
        // entries must not accumulate either, so enumerating has to prune them.
        [Fact]
        public void EnumerateLive_PrunesCollectedInstances()
        {
            var registry = new WeakInstanceRegistry<Tracked>();

            registry.AddWeak(Collected());
            registry.AddWeak(Collected());
            registry.Add(new Tracked(3));

            Assert.Equal(3, registry.Count);

            // Enumerate fully; the pruning happens as each dead entry is passed over.
            _ = registry.EnumerateLive().ToList();

            Assert.Equal(1, registry.Count);
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

            registry.Add(new Tracked(1));
            registry.Remove(default);

            Assert.Equal(new[] { 1 }, registry.EnumerateLive().Select(x => x.Value));
        }
    }
}
