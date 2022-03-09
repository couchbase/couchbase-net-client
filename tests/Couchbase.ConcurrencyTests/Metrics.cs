using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Couchbase.ConcurrencyTests
{
    internal static class Metrics
    {
        internal static readonly Meter TestMeter = new Meter(name: "CouchbaseNetClient.ConcurrencyTests");
        internal const string RootName = "cb.test";
        internal static string CounterName(string name) => RootName + "." + name;
    }
}
