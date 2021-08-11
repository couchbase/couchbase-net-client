using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace Couchbase.Utils
{
    internal static class HttpContentExtensions
    {
        public static bool TryDeserialize<T>(this string jsonString, out T result)
        {
            try
            {
                result = JsonConvert.DeserializeObject<T>(jsonString);
                return true;
            }
            catch
            {
                result = default;
                return false;
            }
        }
    }
}
