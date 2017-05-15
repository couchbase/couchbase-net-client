namespace Couchbase.IO.Operations.Errors
{
    /// <summary>
    /// Thee type of retry strategy.
    /// </summary>
    public enum RetryStrategy
    {
        /// <summary>
        /// No retry strategy. This is the default value.
        /// </summary>
        None,

        /// <summary>
        /// The retry interval is a constant value.
        /// </summary>
        Constant,

        /// <summary>
        /// The retry interval grows in a linear fashion.
        /// </summary>
        Linear,

        /// <summary>
        /// The retry interval grows in an exponential fashion.
        /// </summary>
        Exponential
    }
}