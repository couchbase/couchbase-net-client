using System;
using Couchbase.IntegrationTests.Fixtures;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// A <c>Fact</c> that may be skipped due not having capellaSettings
    /// </summary>
    public class CouchbaseHasCapellaFact : FactAttribute
    {
        public static Lazy<string> CapellaConnectionString = new(() =>
            {

                var _settings = new ConfigurationBuilder()
                    .AddJsonFile("config.json")
                    .Build()
                    .GetSection("capellaSettings")
                    .Get<TestSettings>();
                return _settings != null ? _settings.ConnectionString : null;
            }
        );

        /// <summary>
        /// Gets or sets a value indicating this test should be skipped.  Supercedes all other version checking.
        /// </summary>
        public override string Skip
        {
            get => SkipBasedOnCapella(base.Skip);
            set => base.Skip = value;
        }

        /// <summary>
        /// </summary>
        /// <remarks></remarks>
        internal static string SkipBasedOnCapella(String baseSkip)
        {
            var connectionString = CapellaConnectionString.Value;
            return connectionString != null ? baseSkip : "capellaSettings is required, but not defined in config.json" + (baseSkip != null ? " / " + baseSkip : "");
        }
    }
}
