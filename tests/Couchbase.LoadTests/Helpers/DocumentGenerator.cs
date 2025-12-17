using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.LoadTests.Helpers
{
    public abstract class DocumentGenerator
    {
        protected static Random Random { get; } = new();

        public static IList<int> GenerateDocumentSizes(int minSize, int maxSize, int grades = 10)
        {
            var result = new List<int>(grades);

            var diff = maxSize - minSize;
            var factor = diff / grades;

            if (factor == 0 || minSize == maxSize)
            {
                result.Add(maxSize);
            }
            else
            {
                for (var i = 0; i <= grades; i++)
                {
                    result.Add(minSize + factor * i);
                }
            }

            return result;
        }

        public abstract IEnumerable<object> GenerateDocuments(int count);

        public IEnumerable<KeyValuePair<string, object>> GenerateDocumentsWithKeys(GuidKeyGenerator keyGenerator, int count)
        {
            return GenerateDocuments(count).Zip(keyGenerator.GenerateKeys(count),
                (p, q) => new KeyValuePair<string, object>(q, p));
        }
    }
}
