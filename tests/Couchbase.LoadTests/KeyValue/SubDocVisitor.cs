using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using BenchmarkDotNet.Attributes;
using Couchbase.Core.IO.Serializers;
using Couchbase.KeyValue.ExpressionVisitors;

namespace Couchbase.LoadTests.KeyValue
{
    [MemoryDiagnoser]
    public class SubDocVisitor
    {
        private Expression<Func<Document, string>> _expression;

        [Params("Property", "NestedProperty", "ArrayIndex", "VariableArrayIndex", "VariableDictKey", "DictKey")]
        public string Type { get; set; }

        [GlobalSetup]
        public void GlobalSetup()
        {
            var x = "dynamickey";
            var i = 50;

            _expression = Type switch
            {
                "Property" => p => p.StringProperty,
                "NestedProperty" => p => p.SubDocument.StringProperty,
                "ArrayIndex" => p => p.StringArray[0],
                "VariableArrayIndex" => p => p.StringArray[i],
                "DictKey" => p => p.StringDictionary["key"],
                "VariableDictKey" => p => p.StringDictionary[x],
                _ => throw new Exception("Invalid param")
            };
        }

        [Benchmark(Baseline = true)]
        public string Current()
        {
            return SubDocumentPathExpressionVisitor.GetPath(DefaultSerializer.Instance, _expression);
        }

        public class Document
        {
            public string StringProperty { get; set; }
            public Document SubDocument { get; set; }
            public string[] StringArray { get; set; }
            public Dictionary<string, string> StringDictionary { get; set; }
        }
    }
}
