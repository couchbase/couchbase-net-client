namespace Couchbase.Views
{

    public class ViewOptions
    {
        public ViewScanConsistency ScanConsistency { get; set; }

        public int? Skip { get; set; }
        public int? Limit { get; set; }
        public object StartKey { get; set; }
        public object StartKeyDocId { get; set; }
        public object EndKey { get; set; }
        public object EndKeyDocId { get; set; }
        public bool? InclusiveEnd { get; set; }
        public bool? Group { get; set; }
        public int? GroupLevel { get; set; }
        public object Key { get; set; }
        public object[] Keys { get; set; }
        public bool? Descending { get; set; }
        public bool? Reduce { get; set; }
        public bool? Development { get; set; }
        public bool? FullSet { get; set; }
        public bool? ContinueOnError { get; set; }
        public int? ConnectionTimeout { get; set; }

        public ViewOptions WithScanConsistency(ViewScanConsistency scanConsistency)
        {
            ScanConsistency = scanConsistency;
            return this;
        }

        public ViewOptions WithSkip(int skip)
        {
            Skip = skip;
            return this;
        }

        public ViewOptions WithLimit(int limit)
        {
            Limit = limit;
            return this;
        }

        public ViewOptions WithStartKey(object startKey)
        {
            StartKey = startKey;
            return this;
        }

        public ViewOptions WithStartKeyDocId(object startKyDocId)
        {
            StartKeyDocId = startKyDocId;
            return this;
        }

        public ViewOptions WithEndKey(object endKey)
        {
            EndKey = endKey;
            return this;
        }

        public ViewOptions WithEndKeyDocId(object endKeyDocId)
        {
            EndKeyDocId = endKeyDocId;
            return this;
        }

        public ViewOptions WithInclusiveEnd(bool inclusiveEnd)
        {
            InclusiveEnd = inclusiveEnd;
            return this;
        }

        public ViewOptions WithKey(object key)
        {
            Key = key;
            return this;
        }

        public ViewOptions WithKeys(params object[] keys)
        {
            Keys = keys;
            return this;
        }

        public ViewOptions WithAscending(bool ascending)
        {
            Descending = !ascending;
            return this;
        }

        public ViewOptions WithGroup(bool group)
        {
            Group = group;
            return this;
        }

        public ViewOptions WithGroupLevel(int groupLevel)
        {
            GroupLevel = groupLevel;
            return this;
        }

        public ViewOptions WithReduce(bool reduce)
        {
            Reduce = reduce;
            return this;
        }

        public ViewOptions WithDevelopment(bool development)
        {
            Development = development;
            return this;
        }

        public ViewOptions WithFullSet(bool fullSet)
        {
            FullSet = fullSet;
            return this;
        }

        public ViewOptions WithContinueOnError(bool continueOnError)
        {
            ContinueOnError = continueOnError;
            return this;
        }

        public ViewOptions WithConnectionTimeout(int connectionTimeout)
        {
            ConnectionTimeout = connectionTimeout;
            return this;
        }
    }
}
