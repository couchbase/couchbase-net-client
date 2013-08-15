using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Couchbase.Diagnostics;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture]
    public class KeyFilterTests
    {
        [Test]
        public void Test_that_ShouldTrace_returns_true_when_data_and_message_are_same()
        {
            const string pattern = "10-contentid";
            const string message = pattern;
            var filter = new KeyFilter(pattern);
            Assert.IsTrue(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_returns_false_when_data_and_message_are_not_same()
        {
            const string pattern = "10-contentid";
            const string message = "11-contentid";
            var filter = new KeyFilter(pattern);
            Assert.IsFalse(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_returns_true_when_key_matches_regex()
        {
            const string pattern = "^([0-9]*-[a-zA-Z]*)+$";
            const string message = "11-contentid";
            var filter = new KeyFilter(pattern);
            Assert.IsTrue(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_returns_false_when_key_does_not_match_regex()
        {
            const string pattern = "^([0-9]*-[a-zA-Z]*)+$";
            const string message = "11contentid";
            var filter = new KeyFilter(pattern);
            Assert.IsFalse(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_returns_false_when_key_does_not_match_regex2()
        {
            const string pattern = "^([0-9]*-[a-zA-Z]*)+$";
            const string message = "aa-contentid";
            var filter = new KeyFilter(pattern);
            Assert.IsFalse(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_returns_false_when_key_does_not_match_regex3()
        {
            const string pattern = "^([0-9]*-[a-zA-Z]*)+$";
            const string message = "contentid-11";
            var filter = new KeyFilter(pattern);
            Assert.IsFalse(filter.ShouldTrace(new TraceEventCache(), "source", TraceEventType.Information, 1, message, null, null, null));
        }

        [Test]
        public void Test_that_ShouldTrace_throws_not_supported_exception_when_TraceEventType_is_not_Information()
        {
            const TraceEventType notSupportedTraceEventType = TraceEventType.Error;
            const string pattern = "10-contentid";
            const string message = pattern;
            var filter = new KeyFilter(pattern);
            Assert.Throws<NotSupportedException>(() => filter.ShouldTrace(new TraceEventCache(), "source", notSupportedTraceEventType, 1, message, null, null, null));
        }
    }
}
