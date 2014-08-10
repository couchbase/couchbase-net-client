using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace Couchbase.Extensions
{
    public static class UriExtensions
    {
        /// <summary>
        /// Enables IRI parsing: https://connect.microsoft.com/VisualStudio/feedback/details/758479/system-uri-tostring-behaviour-change
        /// Note that this only applies to .NET versions before 4.5
        /// </summary>
        /// <param name="uri"></param>
        /// <param name="enable"></param>
        public static void EnableIriParsing(this Uri uri, bool enable)
        {
            //Imp: initialize internal static field s_IriParsing once
            Uri.IsWellFormedUriString("http://microsoft.com/", UriKind.Absolute);

            var assembly = Assembly.GetAssembly(uri.GetType());
            if (assembly != null)
            {
                var uriType = assembly.GetType("System.Uri");
                var iriParsing = uriType.InvokeMember("s_IriParsing", BindingFlags.Static | BindingFlags.GetField | BindingFlags.NonPublic, null, null, new object[] { });
                if (iriParsing != null)
                {
                    uriType.InvokeMember("s_IriParsing", BindingFlags.Static | BindingFlags.SetField | BindingFlags.NonPublic, null, null, new object[] { enable });
                }
            }
        }
    }
}
