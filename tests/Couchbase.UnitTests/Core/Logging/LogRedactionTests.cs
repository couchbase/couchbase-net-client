using Couchbase.Core;
using Couchbase.Core.Logging;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Regression coverage for <see cref="ClusterOptions.RedactionLevel"/> actually threading through DI into
    /// a live <see cref="ClusterContext"/>, rather than only being exercised via <see cref="TypedRedactor"/>
    /// constructed directly in a unit test.
    /// </summary>
    public class RedactionLevelDiWiringTests
    {
        [Theory]
        [InlineData(RedactionLevel.None, "user")]
        [InlineData(RedactionLevel.Partial, "<ud>user</ud>")]
        [InlineData(RedactionLevel.Full, "<ud>user</ud>")]
        public void ClusterOptions_RedactionLevel_Flows_Through_Di_To_IRedactor(RedactionLevel level, string expected)
        {
            var options = new ClusterOptions { RedactionLevel = level }.WithPasswordAuthentication("username", "password");
            using var clusterContext = new ClusterContext(null, options);

            var redactor = clusterContext.ServiceProvider.GetRequiredService<IRedactor>();

            Assert.Equal(expected, redactor.UserData("user")?.ToString());
        }

        [Theory]
        [InlineData(RedactionLevel.None, "system")]
        [InlineData(RedactionLevel.Partial, "system")]
        [InlineData(RedactionLevel.Full, "<sd>system</sd>")]
        public void ClusterOptions_RedactionLevel_Flows_Through_Di_To_TypedRedactor(RedactionLevel level, string expected)
        {
            var options = new ClusterOptions { RedactionLevel = level }.WithPasswordAuthentication("username", "password");
            using var clusterContext = new ClusterContext(null, options);

            var redactor = clusterContext.ServiceProvider.GetRequiredService<TypedRedactor>();

            Assert.Equal(level, redactor.RedactionLevel);
            Assert.Equal(expected, redactor.SystemData("system").ToString());
        }

        [Fact]
        public void IRedactor_And_TypedRedactor_Resolve_To_The_Same_Configured_Level()
        {
            var options = new ClusterOptions { RedactionLevel = RedactionLevel.Partial }.WithPasswordAuthentication("username", "password");
            using var clusterContext = new ClusterContext(null, options);

            var redactor = clusterContext.ServiceProvider.GetRequiredService<IRedactor>();
            var typedRedactor = clusterContext.ServiceProvider.GetRequiredService<TypedRedactor>();

            // Partial redacts user data but not system data, on both the public and internal-typed redactor.
            Assert.Equal("<ud>user</ud>", redactor.UserData("user")?.ToString());
            Assert.Equal("system", redactor.SystemData("system")?.ToString());
            Assert.Equal("<ud>user</ud>", typedRedactor.UserData("user").ToString());
            Assert.Equal("system", typedRedactor.SystemData("system").ToString());
        }
    }
}
