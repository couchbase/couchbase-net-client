namespace Couchbase.Core.Compatibility
{
    /// <summary>
    /// Designates the interface stability of a given API; how likely the interface is to change or be removed entirely.
    /// </summary>
    public enum Level
    {
        /// <summary>
        /// This stability level is used to indicate the most stable interfaces that are guaranteed to be
        /// supported and remain stable between SDK versions.
        /// </summary>
        Committed = 0x00,

        /// <summary>
        /// This level is used to indicate APIs that are unlikely to change, but may still change as final
        /// consensus on their behavior has not yet been reached. Uncommitted APIs usually end up becoming
        /// stable APIs.
        /// </summary>
        Uncommitted = 0x01,

        /// <summary>
        /// This level is used to indicate experimental APIs that are still in flux and may likely be changed.
        /// It may also be used to indicate inherently private APIs that may be exposed, but "YMMV"
        /// (your mileage may vary) principles apply. Volatile APIs typically end up being promoted to
        /// Uncommitted after undergoing some modifications.
        /// </summary>
        Volatile = 0x02
    }
}
