using Couchbase.Core.Logging;

namespace Couchbase.UnitTests.Utils
{
    /// <summary>
    /// Redactors for tests that need to construct a service client. A <c>Mock&lt;IRedactor&gt;</c> is
    /// not a good substitute: its methods return null, so any redacted field silently becomes null
    /// rather than passing the value through.
    /// </summary>
    internal static class TestRedactor
    {
        /// <summary>
        /// A real redactor with redaction disabled, which is the SDK default. Values pass through
        /// unchanged.
        /// </summary>
        public static IRedactor None { get; } = new Redactor(new TypedRedactor(RedactionLevel.None));

        /// <summary>
        /// A real redactor that tags user data, for asserting that a field is redacted.
        /// </summary>
        public static IRedactor Partial { get; } = new Redactor(new TypedRedactor(RedactionLevel.Partial));
    }
}
