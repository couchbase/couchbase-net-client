using System;
using System.Collections.Generic;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    internal sealed class StringKeyValueComparer : IEqualityComparer<KeyValuePair<string, string>>
    {
        public static readonly StringKeyValueComparer Instance = new();

        public bool Equals(KeyValuePair<string, string> x, KeyValuePair<string, string> y) =>
            x.Key == y.Key && x.Value == y.Value;

        public int GetHashCode(KeyValuePair<string, string> obj) =>
            HashCode.Combine(obj.Key, obj.Value);
    }
}
