using System.Linq;
using Newtonsoft.Json;

namespace Couchbase.Analytics
{
    internal static class AnalyticsRequestExtensions
    {
        public static string GetParametersAsJson(this IAnalyticsRequest request)
        {
            if (request.PositionalArguments.Any())
                return JsonConvert.SerializeObject(request.PositionalArguments);
            return JsonConvert.SerializeObject(request.NamedParameters);
        }
    }
}
