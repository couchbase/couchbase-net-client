using System;
using System.Collections.Generic;

namespace Couchbase.LoadTests.Helpers
{
    public class JsonDocumentGenerator : DocumentGenerator
    {
        private readonly int _minimumSize;
        private readonly int _maximumSize;

        public JsonDocumentGenerator(int minimumSize, int maximumSize)
        {
            if (minimumSize < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(minimumSize));
            }

            if (maximumSize < minimumSize)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumSize));
            }

            _minimumSize = minimumSize;
            _maximumSize = maximumSize;
        }

        public override IEnumerable<object> GenerateDocuments(int count)
        {
            var docSizes = GenerateDocumentSizes(_minimumSize, _maximumSize);

            for (var i = 0; i < count; i++)
            {
                var docSize = docSizes[i % docSizes.Count];

                yield return GenerateDocument(docSize);
            }
        }

        private object GenerateDocument(int docSize)
        {
            return new Document
            {
                Field = Random.GetAlphanumericString(Math.Max(docSize - Document.JsonWrapperSize, 0))
            };
        }

        private class Document
        {
            public const int JsonWrapperSize = 12; // Curly braces, two sets of double quotes, field name, and colon

            // ReSharper disable once UnusedAutoPropertyAccessor.Local
            public string Field { get; set; }
        }
    }
}
