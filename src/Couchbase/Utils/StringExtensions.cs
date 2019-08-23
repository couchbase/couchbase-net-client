using System;
using System.ComponentModel;
using System.Net;
using Couchbase.Core.Configuration.Server;
using Newtonsoft.Json;

namespace Couchbase.Utils
{
    internal static class StringExtensions
    {
        private const char Colon = ':';

        /// <summary>
        /// Converts a <see cref="System.String"/> to an <see cref="System.Enum"/>. Assumes that
        /// the conversion is possible; e.g. the <see cref="value"/> field must match an enum name.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        /// <exception cref="InvalidEnumArgumentException">Thrown if the conversion cannot be made.</exception>
        public static T ToEnum<T>(this string value) where T : struct
        {
            if (!Enum.TryParse(value, true, out T result))
            {
                throw new ArgumentException();
            }

            return result;
        }

        public static string EncodeParameter(this object parameter)
        {
            return Uri.EscapeDataString(JsonConvert.SerializeObject(parameter));
        }

        /// <summary>
        /// Escape's a string with a N1QL delimiter - the backtick.
        /// </summary>
        /// <param name="theString">The string.</param>
        /// <returns></returns>
        public static string N1QlEscape(this string theString)
        {
            if (!theString.StartsWith("`", StringComparison.OrdinalIgnoreCase))
            {
                theString = theString.Insert(0, "`");
            }

            if (!theString.EndsWith("`", StringComparison.OrdinalIgnoreCase))
            {
                theString = theString.Insert(theString.Length, "`");
            }

            return theString;
        }

        //TODO refactor/harden/tests - perhaps move to different class
        public static IPEndPoint GetIpEndPoint(this NodesExt nodesExt, ClusterOptions clusterOptions)
        {
            var useSsl = clusterOptions.UseSsl;
            var uriBuilder = new UriBuilder
            {
                Scheme = useSsl ? Uri.UriSchemeHttps : Uri.UriSchemeHttp,
                Host = nodesExt.Hostname,
                Port = useSsl ? nodesExt.Services.KvSsl : nodesExt.Services.Kv
            };

            var ipAddress = uriBuilder.Uri.GetIpAddress(false);//TODO support IPv6
            return new IPEndPoint(ipAddress, uriBuilder.Port);
        }
    }
}
