namespace Couchbase.Services.Analytics
{
    public enum IngestMethod
    {
        /// <summary>
        /// Insert the document, failing if it exists
        /// </summary>
        Insert,

        /// <summary>
        /// Inserts the document, updating it if it exists
        /// </summary>
        Upsert,

        /// <summary>
        /// Replaces and existing document, failing if does not exist
        /// </summary>
        Replace
    }
}
