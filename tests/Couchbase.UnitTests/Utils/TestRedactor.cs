using Couchbase.Core.Logging;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Shared redactors, one per <see cref="RedactionLevel"/>, for tests that construct a service
    /// client or assert on a redacted field. A redactor is immutable and stateless, so a test can
    /// take one of these rather than build its own.
    /// </summary>
    internal static class TestRedactor
    {
        /// <summary>
        /// A real redactor with redaction disabled, which is the SDK default. Values pass through
        /// unchanged.
        /// </summary>
        public static Redactor None { get; } = new Redactor(RedactionLevel.None);

        /// <summary>
        /// A real redactor that tags user data, for asserting that a field is redacted.
        /// </summary>
        public static Redactor Partial { get; } = new Redactor(RedactionLevel.Partial);
    }
}
