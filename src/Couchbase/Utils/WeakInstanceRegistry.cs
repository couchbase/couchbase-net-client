using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Couchbase.Utils
{
    /// <summary>
    /// Tracks a set of live instances of <typeparamref name="T"/> without keeping them alive, for the
    /// purpose of reporting process-wide diagnostics.
    /// </summary>
    /// <typeparam name="T">Type of instance being tracked.</typeparam>
    /// <remarks>
    /// <para>
    /// Instances should be removed deterministically via <see cref="Remove"/> when they are disposed, which
    /// keeps the registry sized to the number of live instances rather than the number ever created. The
    /// weak references are a safety net for instances which are abandoned without being disposed; entries for
    /// those are dropped the next time <see cref="EnumerateLive"/> runs.
    /// </para>
    /// <para>
    /// <see cref="EnumerateLive"/> may be called concurrently with <see cref="Add"/> and <see cref="Remove"/>.
    /// It reflects a moment-in-time view rather than a consistent snapshot, which is sufficient for
    /// diagnostics and avoids the whole-collection locking a <see cref="ConcurrentBag{T}"/> requires to
    /// enumerate.
    /// </para>
    /// </remarks>
    internal sealed class WeakInstanceRegistry<T>
        where T : class
    {
        private readonly ConcurrentDictionary<long, WeakReference<T>> _entries = new();
        private long _nextId;

        /// <summary>
        /// Number of entries currently held, including any whose target has been collected but which have
        /// not yet been pruned by <see cref="EnumerateLive"/>. For unit testing only.
        /// </summary>
        internal int Count => _entries.Count;

        /// <summary>
        /// Begins tracking an instance.
        /// </summary>
        /// <param name="instance">Instance to track.</param>
        /// <returns>Identifier to pass to <see cref="Remove"/> once the instance is disposed.</returns>
        public long Add(T instance) => AddCore(new WeakReference<T>(instance));

        /// <summary>
        /// Stops tracking an instance. Safe to call more than once for the same identifier.
        /// </summary>
        /// <param name="id">Identifier returned by <see cref="Add"/>.</param>
        public void Remove(long id) => _entries.TryRemove(id, out _);

        /// <summary>
        /// Returns the tracked instances which are still reachable, pruning the entries for any which have
        /// been collected.
        /// </summary>
        public IEnumerable<T> EnumerateLive()
        {
            foreach (var entry in _entries)
            {
                if (entry.Value.TryGetTarget(out var instance))
                {
                    yield return instance;
                }
                else
                {
                    // The instance was collected without being disposed. Removing during enumeration is
                    // supported by ConcurrentDictionary.
                    _entries.TryRemove(entry.Key, out _);
                }
            }
        }

        /// <summary>
        /// Adds a pre-built weak reference. For unit testing only, so that collected entries can be
        /// simulated without relying on the garbage collector.
        /// </summary>
        internal long AddWeak(WeakReference<T> reference) => AddCore(reference);

        private long AddCore(WeakReference<T> reference)
        {
            // Identifiers start at 1, so the default value of a field holding one is never a valid entry.
            // This makes Remove a no-op for an instance which was never added.
            var id = Interlocked.Increment(ref _nextId);
            _entries[id] = reference;
            return id;
        }
    }
}
