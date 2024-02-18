using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

#nullable enable

namespace Couchbase.Core.Diagnostics.Metrics
{
    /// <summary>
    /// A collection of histograms keyed by tags.
    /// </summary>
    internal sealed class HistogramCollectorSet(string name) : IEnumerable<HistogramCollector>
    {
        private HistogramCollector? _taglessHistogram;

        private ConcurrentDictionary<KeyValuePair<string, string>, HistogramCollector>? _histograms;

        public string Name { get; } = name;

        public HistogramCollector GetOrAdd(KeyValuePair<string, string>? tag)
        {
            if (tag is null)
            {
                // Optimize for the common case where there is no tag (i.e. query operations)
                // by avoiding the ConcurrentDictionary.

                var taglessHistogram = _taglessHistogram;
                if (taglessHistogram is not null)
                {
                    return taglessHistogram;
                }

                taglessHistogram = new HistogramCollector();
                return Interlocked.CompareExchange(ref _taglessHistogram, taglessHistogram, null) ??
                       taglessHistogram;
            }

            var histograms = _histograms;
            if (histograms is null)
            {
                histograms = new ConcurrentDictionary<KeyValuePair<string, string>, HistogramCollector>(StringKeyValueComparer.Instance);
                histograms = Interlocked.CompareExchange(ref _histograms, histograms, null)
                             ?? histograms;
            }

            return histograms.GetOrAdd(tag.GetValueOrDefault(),
                static tag => new HistogramCollector(tag));
        }

        public IEnumerator<HistogramCollector> GetEnumerator()
        {
            var taglessHistogram = _taglessHistogram;
            if (taglessHistogram is not null)
            {
                yield return taglessHistogram;
            }

            var histograms = _histograms;
            if (histograms is not null)
            {
                foreach (var histogram in histograms.Values)
                {
                    yield return histogram;
                }
            }
        }


        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
