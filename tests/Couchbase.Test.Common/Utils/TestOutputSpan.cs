using Couchbase.Core.Diagnostics.Tracing;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using Xunit.Abstractions;

namespace Couchbase.Test.Common.Utils
{
    public class TestOutputSpan : IRequestSpan
    {
        private readonly ITestOutputHelper _outputHelper;
        private readonly ConcurrentQueue<(string, DateTimeOffset?)> _events = new ConcurrentQueue<(string, DateTimeOffset?)>();
        private readonly ConcurrentDictionary<string, string> _attributes = new ConcurrentDictionary<string, string>();

        public TestOutputSpan(ITestOutputHelper outputHelper)
        {
            _outputHelper = outputHelper;
        }

        public IEnumerable<KeyValuePair<string, string>> Attributes => _attributes;

        public IRequestSpan Parent { get; set; }

        public bool CanWrite => true;

        public string Id { get; } = Guid.NewGuid().ToString();

        public uint? Duration { get; set; }

        public IRequestSpan AddEvent(string name, DateTimeOffset? timestamp = null)
        {
            _events.Enqueue((name, timestamp));
            return this;
        }

        public IRequestSpan ChildSpan(string name)
        {
            var child = new TestOutputSpan(_outputHelper)
            {
                Parent = this
            };

            return child;
        }

        public void Dispose()
        {
            End();
        }

        public void End()
        {
            _outputHelper.WriteLine($"Span: <${Id}>");
            foreach (var evt in _events)
            {
                _outputHelper.WriteLine($"\tEvent({evt.Item1}, {evt.Item2})");
            }

            foreach (var attr in _attributes)
            {
                _outputHelper.WriteLine($"\tAttr({attr.Key}, {attr.Value})");
            }
        }

        public IRequestSpan SetAttribute(string key, bool value) => SetAttribute(key, value.ToString());

        public IRequestSpan SetAttribute(string key, string value)
        {
            _attributes.TryAdd(key, value);
            return this;
        }

        public IRequestSpan SetAttribute(string key, uint value) => SetAttribute(key, value.ToString(System.Globalization.CultureInfo.InvariantCulture));
    }
}
