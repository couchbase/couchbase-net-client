using Couchbase.Core.DI;
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

            var redactor = new TypedRedactor(options);

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

            var redactor = new TypedRedactor(options);

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

            var redactor = new TypedRedactor(options);

            Assert.Equal("<ud>user</ud>", redactor.UserData("user").ToString());
            Assert.Equal("<md>meta</md>", redactor.MetaData("meta").ToString());
            Assert.Equal("<sd>system</sd>", redactor.SystemData("system").ToString());
        }

        [Fact]
        public void IRedactor_Redacts_The_Same_As_The_Typed_Methods()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            IRedactor redactor = new TypedRedactor(options);

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

            IRedactor redactor = new TypedRedactor(options);

            // Null is passed straight through rather than being wrapped in a Redacted<object>.
            Assert.Null(redactor.UserData(null));
            Assert.Null(redactor.MetaData(null));
            Assert.Null(redactor.SystemData(null));
        }

        [Fact]
        public void IRedactor_And_TypedRedactor_Resolve_To_The_Same_Instance()
        {
            var provider = new ClusterOptions().BuildServiceProvider();

            var typed = provider.GetService(typeof(TypedRedactor));
            var byInterface = provider.GetService(typeof(IRedactor));

            Assert.NotNull(typed);
            Assert.Same(typed, byInterface);
        }

        [Fact]
        public void SpanFormattable_Redacts_Properly()
        {
            var options = new ClusterOptions
            {
                RedactionLevel = RedactionLevel.Full
            };

            var redactor = new TypedRedactor(options);
            var spanFormattable = new HostEndpointWithPort("localhost", 8675309);
            var asString = $"{redactor.UserData(spanFormattable)} is formatted";
            Assert.Contains("</ud>", asString);
        }
    }
}
