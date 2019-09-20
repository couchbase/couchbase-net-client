namespace Couchbase.Services.KeyValue
{
    /// <summary>
    /// Server defined MutationMacro values to be expanded by the server.
    /// </summary>
    public static class MutationMacro
    {
        /// <summary>
        /// The server will perform a CAS macro expansion.
        /// </summary>
        public static IMutationMacro Cas => new MutationMacroCas();

        /// <summary>
        /// The server will perform a SeqNo macro expansion.
        /// </summary>
        public static IMutationMacro SeqNo => new MutationMacroSeqNo();

        /// <summary>
        /// The server will do a ValueCRC32c macro expansion.
        /// </summary>
        // ReSharper disable once InconsistentNaming
        public static IMutationMacro ValueCRC32c => new MutationMacroValueCrc32C();
    }
}
