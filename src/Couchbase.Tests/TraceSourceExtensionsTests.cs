using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using Couchbase.Diagnostics;
using NUnit.Framework;

namespace Couchbase.Tests
{
    [TestFixture(Category = "Integration")]
    public class TraceSourceExtensionsTests
    {
        static readonly TraceSource TraceSourceRegex = new TraceSource("CouchbaseTraceRegex");
        static readonly TraceSource TraceSourceLiteral = new TraceSource("CouchbaseTraceLiteral");

        public void SetUp()
        {
            var sourceSwitchRegex = new SourceSwitch("SourceSwitchRegex", "Verbose");
            TraceSourceRegex.Switch = sourceSwitchRegex;
            TraceSourceRegex.Listeners.Add(new ConsoleTraceListener());
            TraceSourceRegex.Listeners[0].Name = "console1";
            TraceSourceRegex.Listeners[0].Filter = new KeyFilter("^([0-9]*-[a-zA-Z]*)+$");

            var sourceSwitchLiteral = new SourceSwitch("SourceSwitchLiteral", "Verbose");
            TraceSourceLiteral.Switch = sourceSwitchLiteral;
            TraceSourceLiteral.Listeners.Add(new ConsoleTraceListener());
            TraceSourceLiteral.Listeners[0].Name = "console2";
            TraceSourceLiteral.Listeners[0].Filter = new KeyFilter("10-article");
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_That_TraceKey_Will_Not_Find_Match()
        {
            var key = "hh-somekey";
            TraceSourceRegex.TraceKey(key);
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_That_TraceKey2_Will_Not_Find_Match()
        {
            var key = "somekey-10";
            var message = "This is a great message, but you won't see it in stdout";
            TraceSourceRegex.TraceKey(key, message);
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_That_TraceKey_Will_Find_Match()
        {
            var key = "10-somekey";
            TraceSourceRegex.TraceKey(key);
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_That_TraceKey_Will_Find_Match_With_Message()
        {
            var key = "10-somekey";
            var message = "This is a great message and you will see it displayed by stdout";
            TraceSourceRegex.TraceKey(key, message);
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_that_TraceKey_supports_constant_regex()
        {
            var key = "10-article";
            var message = "This is a key that matches the string '10-article' and you will see it displayed by stdout";
            TraceSourceLiteral.TraceKey(key, message);
        }

        [Test(Description = "Results are written to stdout, so no way to Assert at the moment")]
        public void Test_that_TraceKey_supports_constant_regex2()
        {
            var key = "11-article";
            var message = "This is a key that matches the string '11-article' and you will NOT see it displayed by stdout";
            TraceSourceLiteral.TraceKey(key, message);
        }
    }
}
