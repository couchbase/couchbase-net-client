#nullable enable
using System;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A wrapper interface for all spans flowing through the SDK.
    /// </summary>
    public interface IRequestSpan : IDisposable
    {
        /// <summary>
        /// Sets an attribute on the span which is translated to a corresponding implementation specific tag.
        /// </summary>
        /// <remarks>Depending upon the implementation the attribute might be ignored (NOOP) for example.</remarks>
        /// <param name="key">The key of attribute.</param>
        /// <param name="value">The boolean value of the attribute.</param>
        IRequestSpan SetAttribute(string key, bool value);

        /// <summary>
        /// Sets an attribute on the span which is translated to a corresponding implementation specific tag.
        /// </summary>
        /// <remarks>Depending upon the implementation the attribute might be ignored (NOOP) for example.</remarks>
        /// <param name="key">The key of attribute.</param>
        /// <param name="value">The string value of the attribute.</param>
        IRequestSpan SetAttribute(string key, string value);

        /// <summary>
        /// Sets an attribute on the span which is translated to a corresponding implementation specific tag.
        /// </summary>
        /// <remarks>Depending upon the implementation the attribute might be ignored (NOOP) for example.</remarks>
        /// <param name="key">The key of attribute.</param>
        /// <param name="value">The uint value of the attribute.</param>
        IRequestSpan SetAttribute(string key, uint value);

        /// <summary>
        /// Sets an event on the span which translates to the corresponding implementation.
        /// </summary>
        /// <remarks>Depending upon the implementation the event might be ignored (NOOP) for example.</remarks>
        /// <param name="name">The name of the event.</param>
        /// <param name="timestamp">The timestamp when it happened.</param>
        IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null);

        /// <summary>
        /// Completes the span.
        /// </summary>
        void End();

        /// <summary>
        /// The optional parent span.
        /// </summary>
        IRequestSpan? Parent { get; set; }

        /// <summary>
        /// Creates a child span from this span.
        /// </summary>
        /// <param name="name">The name of the span.</param>
        /// <returns>A child span with references to the parent span.</returns>
        IRequestSpan ChildSpan(string name);

        /// <summary>
        /// If true <see cref="IRequestSpan"/> attributes will be written for this tag. In cases such
        /// as the <see cref="NoopRequestSpan"/> it should be false as no attributes/tags are collected.
        /// </summary>
        bool CanWrite { get; }

        /// <summary>
        /// The id, possibly from the underlying activity, of the span.
        /// </summary>
        string? Id { get; }

        /// <summary>
        /// The duration of the span set after Dispose() or <see cref="End()"/> is called.
        /// </summary>
        uint? Duration { get; }
    }
}
