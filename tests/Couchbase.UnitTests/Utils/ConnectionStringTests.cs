using System;
using System.Linq;
using Xunit;

namespace Couchbase.UnitTests.Utils
{
    public class ConnectionStringTests
    {
        [Fact]
        public void Can_parse_valid_schemes()
        {
            var parsed = ConnectionString.Parse("http://");
            Assert.Equal(Scheme.Http, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Empty(parsed.Hosts);
            Assert.Empty(parsed.Parameters);

            parsed = ConnectionString.Parse("couchbase://");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Empty(parsed.Hosts);
            Assert.Empty(parsed.Parameters);

            parsed = ConnectionString.Parse("couchbases://");
            Assert.Equal(Scheme.Couchbases, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Empty(parsed.Hosts);
            Assert.Empty(parsed.Parameters);
        }

        [Fact]
        public void Throws_ArgumentException_for_invalid_scheme()
        {
            Assert.Throws<ArgumentException>(() => ConnectionString.Parse("invalid://"));
        }

        [Fact]
        public void Can_parse_hosts()
        {
            var parsed = ConnectionString.Parse("couchbase://localhost");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Empty(parsed.Parameters);
            Assert.Single(parsed.Hosts);
            Assert.Equal("localhost", parsed.Hosts.First());
            Assert.Null(parsed.Username);

            parsed = ConnectionString.Parse("couchbase://localhost:1234");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Empty(parsed.Parameters);
            Assert.Single(parsed.Hosts);
            Assert.Equal("localhost:1234", parsed.Hosts.First());
            Assert.Null(parsed.Username);

            parsed = ConnectionString.Parse("couchbase://foo:1234,bar:5678");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Empty(parsed.Parameters);
            Assert.Equal(2, parsed.Hosts.Count);
            Assert.Equal("foo:1234", parsed.Hosts[0]);
            Assert.Equal("bar:5678", parsed.Hosts[1]);
            Assert.Null(parsed.Username);

            parsed = ConnectionString.Parse("couchbase://foo,bar:5678,baz");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Empty(parsed.Parameters);
            Assert.Equal(3, parsed.Hosts.Count);
            Assert.Equal("foo", parsed.Hosts[0]);
            Assert.Equal("bar:5678", parsed.Hosts[1]);
            Assert.Equal("baz", parsed.Hosts[2]);
            Assert.Null(parsed.Username);
        }

        [Fact]
        public void Can_parse_parameters()
        {
            var parsed = ConnectionString.Parse("couchbase://localhost?foo=bar");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Single(parsed.Hosts);
            Assert.Single(parsed.Parameters);
            Assert.Equal("bar", parsed.Parameters["foo"]);
            Assert.Null(parsed.Username);

            parsed = ConnectionString.Parse("couchbase://localhost?foo=bar&setting=true");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Single(parsed.Hosts);
            Assert.Equal(2, parsed.Parameters.Count);
            Assert.Equal("bar", parsed.Parameters["foo"]);
            Assert.Equal("true", parsed.Parameters["setting"]);
            Assert.Null(parsed.Username);
        }

        [Fact]
        public void Can_parse_username()
        {
            var parsed = ConnectionString.Parse("couchbase://user@localhost?foo=bar");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Equal("user", parsed.Username);
            Assert.Equal("localhost", parsed.Hosts.First());
            Assert.Single(parsed.Parameters);
            Assert.Equal("bar", parsed.Parameters["foo"]);

            parsed = ConnectionString.Parse("couchbase://user123@host1,host2?foo=bar&setting=true");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Equal("user123", parsed.Username);
            Assert.Equal("host1", parsed.Hosts.First());
            Assert.Equal("host2", parsed.Hosts.Last());
            Assert.Equal(2, parsed.Parameters.Count);
            Assert.Equal("bar", parsed.Parameters["foo"]);
            Assert.Equal("true", parsed.Parameters["setting"]);
        }

        [Fact]
        public void Can_parse_single_ipv6_without_port()
        {
            var parsed = ConnectionString.Parse("couchbase://[::1]");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Single(parsed.Hosts);
            Assert.Equal("[::1]", parsed.Hosts.First());
            Assert.Empty(parsed.Parameters);

            parsed = ConnectionString.Parse("couchbase://[::1/128]");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Single(parsed.Hosts);
            Assert.Equal("[::1/128]", parsed.Hosts.First());
            Assert.Empty(parsed.Parameters);
        }

        [Fact]
        public void Can_parse_multiple_ipv6_without_port()
        {
            var parsed = ConnectionString.Parse("couchbase://[::1], [::2]");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Equal(2, parsed.Hosts.Count);
            Assert.Equal("[::1]", parsed.Hosts[0]);
            Assert.Equal("[::2]", parsed.Hosts[1]);
            Assert.Empty(parsed.Parameters);

            parsed = ConnectionString.Parse("couchbase://[::1/128], [::2/128],[::3/128]");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Equal(3, parsed.Hosts.Count);
            Assert.Equal("[::1/128]", parsed.Hosts[0]);
            Assert.Equal("[::2/128]", parsed.Hosts[1]);
            Assert.Equal("[::3/128]", parsed.Hosts[2]);
            Assert.Empty(parsed.Parameters);
        }

        [Fact]
        public void Can_parse_single_ipv6_with_port()
        {
            var parsed = ConnectionString.Parse("couchbase://[::1]:8091, [::1]:11210");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Equal(2, parsed.Hosts.Count);
            Assert.Equal("[::1]:8091", parsed.Hosts[0]);
            Assert.Equal("[::1]:11210", parsed.Hosts[1]);
            Assert.Empty(parsed.Parameters);

            parsed = ConnectionString.Parse("couchbase://[::1/128]:1234, [::1/128]:11210,[::1/128]:1");
            Assert.Equal(Scheme.Couchbase, parsed.Scheme);
            Assert.Null(parsed.Username);
            Assert.Equal(3, parsed.Hosts.Count);
            Assert.Equal("[::1/128]:1234", parsed.Hosts[0]);
            Assert.Equal("[::1/128]:11210", parsed.Hosts[1]);
            Assert.Equal("[::1/128]:1", parsed.Hosts[2]);
            Assert.Empty(parsed.Parameters);
        }
    }
}
