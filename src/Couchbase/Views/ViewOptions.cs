using System;
using System.Collections.Generic;
using Couchbase.Core.IO.Serializers;
using Couchbase.Management.Views;

#nullable enable

namespace Couchbase.Views
{
    public class ViewOptions
    {
        internal ViewScanConsistency ScanConsistencyValue { get; set; }
        internal int? SkipValue { get; set; }
        internal int? LimitValue { get; set; }
        internal object? StartKeyValue { get; set; }
        internal object? StartKeyDocIdValue { get; set; }
        internal object? EndKeyValue { get; set; }
        internal object? EndKeyDocIdValue { get; set; }
        internal bool? InclusiveEndValue { get; set; }
        internal bool? GroupValue { get; set; }
        internal int? GroupLevelValue { get; set; }
        internal object? KeyValue { get; set; }
        internal object[]? KeysValue { get; set; }
        internal ViewOrdering ViewOrderingValue { get; set; } = ViewOrdering.Decesending;
        internal bool? ReduceValue { get; set; }
        internal bool? DevelopmentValue { get; set; }
        internal bool? FullSetValue { get; set; }
        internal bool? DebugValue { get; set; }
        internal TimeSpan? TimeoutValue { get; set; }
        internal ViewErrorMode OnErrorValue { get; set; } = ViewErrorMode.Stop;
        internal Dictionary<string, string> RawParameters = new Dictionary<string, string>();
        internal DesignDocumentNamespace @NamespaceValue { get; set; } = DesignDocumentNamespace.Production;
        internal ITypeSerializer? SerializerValue { get; set; }

        public ViewOptions ScanConsistency(ViewScanConsistency scanConsistency)
        {
            ScanConsistencyValue = scanConsistency;
            return this;
        }

        public ViewOptions Skip(int skip)
        {
            SkipValue = skip;
            return this;
        }

        public ViewOptions Limit(int limit)
        {
            LimitValue = limit;
            return this;
        }

        public ViewOptions StartKey(object? startKey)
        {
            StartKeyValue = startKey;
            return this;
        }

        public ViewOptions StartKeyDocId(object? startKyDocId)
        {
            StartKeyDocIdValue = startKyDocId;
            return this;
        }

        public ViewOptions EndKey(object? endKey)
        {
            EndKeyValue = endKey;
            return this;
        }

        public ViewOptions EndKeyDocId(object? endKeyDocId)
        {
            EndKeyDocIdValue = endKeyDocId;
            return this;
        }

        public ViewOptions InclusiveEnd(bool inclusiveEnd)
        {
            InclusiveEndValue = inclusiveEnd;
            return this;
        }

        public ViewOptions Key(object? key)
        {
            KeyValue = key;
            return this;
        }

        public ViewOptions Keys(params object[]? keys)
        {
            KeysValue = keys;
            return this;
        }

        public ViewOptions Ordering(ViewOrdering viewOrdering)
        {
            ViewOrderingValue = viewOrdering;
            return this;
        }

        public ViewOptions Group(bool group)
        {
            GroupValue = group;
            return this;
        }

        public ViewOptions GroupLevel(int groupLevel)
        {
            GroupLevelValue = groupLevel;
            return this;
        }

        public ViewOptions Reduce(bool reduce)
        {
            ReduceValue = reduce;
            return this;
        }

        public ViewOptions Development(bool development)
        {
            DevelopmentValue = development;
            return this;
        }

        public ViewOptions FullSet(bool fullSet)
        {
            FullSetValue = fullSet;
            return this;
        }

        public ViewOptions OnError(ViewErrorMode errorMode)
        {
            OnErrorValue = errorMode;
            return this;
        }

        public ViewOptions Debug(bool debug)
        {
            DebugValue = debug;
            return this;
        }

        public ViewOptions Raw(string key, string value)
        {
            RawParameters[key] = value;
            return this;
        }

        public ViewOptions Namespace(DesignDocumentNamespace @namespace)
        {
            @NamespaceValue = @namespace;
            return this;
        }

        public ViewOptions Timeout(TimeSpan timeout)
        {
            TimeoutValue = timeout;
            return this;
        }

        public ViewOptions Serializer(ITypeSerializer? serializer)
        {
            SerializerValue = serializer;
            return this;
        }

        public static ViewOptions Default => new ViewOptions();
    }
}
