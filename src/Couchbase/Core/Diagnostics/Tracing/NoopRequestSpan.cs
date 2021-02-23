#nullable enable
using System;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A NOOP implementation of <see cref="IRequestSpan"/>. Calling any method will do nothing as it's a NOOP.
    /// </summary>
    public class NoopRequestSpan : IRequestSpan
    {
        public static IRequestSpan Instance = new NoopRequestSpan();

        /// <inheritdoc />
        public void Dispose()
        {
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, bool value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, string value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan SetAttribute(string key, uint value)
        {
            return this;
        }

        /// <inheritdoc />
        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            return this;
        }

        /// <inheritdoc />
        public void End()
        {
        }

        /// <inheritdoc />
        public IRequestSpan? Parent { get; set; }

        /// <inheritdoc />
        public IRequestSpan ChildSpan(string name)
        {
            return Instance;
        }

        /// <inheritdoc />
        public bool CanWrite { get; } = false;

        /// <inheritdoc />
        public string? Id { get; }
    }
}
