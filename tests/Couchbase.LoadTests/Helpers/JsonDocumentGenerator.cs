using System;
using System.Collections.Generic;

namespace Couchbase.LoadTests.Helpers
{
    public class JsonDocumentGenerator : DocumentGenerator
    {
        private const int JsonWrapperSize = 1; // 2 - 1 because there is no trailing comma on the last field
        private const int FieldWrapperSize = 10;
        private const int MaximumFieldSize = 1024;

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
            var remainingSize = docSize - JsonWrapperSize;

            var fieldCount = remainingSize / MaximumFieldSize;
            if (remainingSize % MaximumFieldSize > 0)
            {
                fieldCount++;
            }

            var document = new Dictionary<string, string>(fieldCount);

            var i = 0;
            while (remainingSize > 0)
            {
                var fieldSize = Math.Min(remainingSize, MaximumFieldSize);

                document.Add(GetFieldName(i), Random.GetAlphanumericString(Math.Max(fieldSize - FieldWrapperSize, 0)));

                remainingSize -= fieldSize;
                i++;
            }

            return document;
        }

        private static string GetFieldName(int index)
        {
            return index.ToString("x4");
        }
    }
}
