namespace Couchbase.FitPerformer
{
    /// <summary>
    /// TOML configuration file for FIT Transactions Performer
    /// </summary>
    public sealed class Settings
    {
        // ReSharper disable once InconsistentNaming
        public bool deferred_commit { get; set; }

        // ReSharper disable once InconsistentNaming
        public bool transaction_id { get; set; }

        // ReSharper disable once InconsistentNaming
        public string protocol { get; set; }

        // ReSharper disable once InconsistentNaming
        public bool improved_timestamps { get; set; }
    }
}
