namespace Couchbase
{
    /// <summary>
    /// Specifies the network resolution strategy to use for alternative network; used in some container
    /// environments where there maybe internal and external addresses for connecting.
    /// </summary>
    public static class NetworkResolution
    {
        /// <summary>
        /// Alternative addresses will be used if available. The default.
        /// </summary>
        public const string Auto ="auto";

        /// <summary>
        /// Do not use alternative addresses.
        /// </summary>
        public const string Default = "default";

        /// <summary>
        /// Use alternative addresses.
        /// </summary>
        public const string External = "external";
    }
}
