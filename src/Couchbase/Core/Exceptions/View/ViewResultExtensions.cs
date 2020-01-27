using System.Net;
using Couchbase.Views;

namespace Couchbase.Core.Exceptions.View
{
    internal static class ViewResultExtensions
    {
        public static bool ViewNotFound<TKey, TValue>(this ViewResult<TKey, TValue> result)
        {
            return result.StatusCode == HttpStatusCode.NotFound &&
                   result.Message.Contains("not_found");
        }
    }
}
