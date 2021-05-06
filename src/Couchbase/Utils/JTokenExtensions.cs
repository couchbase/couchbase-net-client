using Newtonsoft.Json.Linq;

namespace Couchbase.Utils
{
    internal static class JTokenExtensions
    {
        public static T GetTokenValue<T>(this JToken jToken, string name)
        {
            var value = jToken.SelectToken(name);
            return value == null ? default : value.Value<T>();
        }
    }
}
