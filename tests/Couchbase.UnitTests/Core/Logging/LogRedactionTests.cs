using System;
using System.Reflection;
using Couchbase.Core.Logging;
using Xunit;

namespace Couchbase.UnitTests.Core.Logging
{
    public class RedactableArgumentTests
    {
        [Fact]
        public void When_Redaction_Disabled_No_Redaction_Occurs()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.None
            };

            var redactor = new Redactor(options);

            Assert.Equal("1", redactor.UserData("1").ToString());
            Assert.Equal("", redactor.MetaData((string) null).ToString());
            Assert.Equal("system", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void When_User_Redaction_Redact_Partial()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Partial
            };

            var redactor = new Redactor(options);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("meta", redactor.MetaData("meta").ToString());
            Assert.Equal("system", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void When_Full_Redaction_Redact_Everything()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            var redactor = new Redactor(options);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("<md>meta</md>", redactor.MetaData("meta").ToString());
            Assert.Equal("<sd>system</sd>", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void IRedactor_Redacts_The_Same_As_The_Generic_Methods()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            IRedactor redactor = new Redactor(options);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("<md>meta</md>", redactor.MetaData("meta").ToString());
            Assert.Equal("<sd>system</sd>", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void IRedactor_Returns_Null_For_Null()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            IRedactor redactor = new Redactor(options);

            // Null is passed straight through rather than being wrapped in a Redacted<object>.
            Assert.Null(redactor.UserData(null));
            Assert.Null(redactor.MetaData(null));
            Assert.Null(redactor.SystemData(null));
        }

        [Fact]
        public void IRedactor_And_Redactor_Resolve_To_The_Same_Instance()
        {
            var provider = new ClusterOptions().BuildServiceProvider();

            var typed = provider.GetService(typeof(Redactor));
            var byInterface = provider.GetService(typeof(IRedactor));

            Assert.NotNull(typed);
            Assert.Same(typed, byInterface);
        }

        [Theory]
        [InlineData(RedactionLevel.None)]
        [InlineData(RedactionLevel.Partial)]
        [InlineData(RedactionLevel.Full)]
        public void Every_Defined_Level_Is_Accepted(RedactionLevel level)
        {
            Assert.Equal(level, new Redactor(level).RedactionLevel);
        }

        [Fact]
        public void An_Undefined_Level_Is_Rejected_At_Construction()
        {
            var undefined = (RedactionLevel)99;

            var ex = Assert.Throws<ArgumentOutOfRangeException>(() => new Redactor(undefined));

            // All three carried nothing before: the parameter name was Enum.GetName of an undefined
            // value (null), there was no actual value, and the message held a literal "{redactionLevel}".
            Assert.Equal(nameof(ClusterOptions.RedactionLevel), ex.ParamName);
            Assert.Equal(undefined, ex.ActualValue);
            Assert.DoesNotContain("{", ex.Message);
        }

        [Fact]
        public void An_Undefined_Level_On_ClusterOptions_Is_Rejected()
        {
            var options = new ClusterOptions { RedactionLevel = (RedactionLevel)99 };

            Assert.Throws<ArgumentOutOfRangeException>(() => new Redactor(options));
        }

        [Fact]
        public void An_Undefined_Level_Fails_When_The_Redactor_Is_Resolved()
        {
            var options = new ClusterOptions { RedactionLevel = (RedactionLevel)99 };
            var provider = options.BuildServiceProvider();

            // Rejected when the redactor is built rather than by a later log statement. The service
            // factory constructs by reflection, so the real exception arrives wrapped.
            var ex = Assert.Throws<TargetInvocationException>(() => provider.GetService(typeof(Redactor)));

            Assert.IsType<ArgumentOutOfRangeException>(ex.InnerException);
        }

        [Fact]
        public void SpanFormattable_Redacts_Properly()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            var redactor = new Redactor(options);
            var spanFormattable = new HostEndpointWithPort("localhost", 8675309);
            var asString = $"{redactor.UserData(spanFormattable)} is formatted";
            Assert.Contains("</ud>", asString);
        }
    }
}
