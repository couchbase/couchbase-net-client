using System;
using Newtonsoft.Json;

namespace Couchbase.UnitTests.Fixtures
{
    /// <summary>
    /// Saves JsonConvert.DefaultSettings and restores them after tests.
    /// </summary>
    public class JsonConvertFixture : IDisposable
    {
        private readonly Func<JsonSerializerSettings> _savedSerializerSettings = JsonConvert.DefaultSettings;

        public void Dispose()
        {
            JsonConvert.DefaultSettings = _savedSerializerSettings;
        }
    }
}
