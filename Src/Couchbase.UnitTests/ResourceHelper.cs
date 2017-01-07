using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Couchbase.UnitTests
{
    public static class ResourceHelper
    {
        private static readonly Assembly Assembly = typeof(ResourceHelper).GetTypeInfo().Assembly;
        public static string ReadResource(string resourcePath)
        {
            var resourceName = Assembly.GetName().Name + "." + resourcePath.Replace("\\", ".");

            using (var stream = Assembly.GetManifestResourceStream(resourceName))
            {
                if (stream == null)
                {
                    return null;
                }

                using (var reader = new StreamReader(stream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        public static Stream ReadResourceAsStream(string resourcePath)
        {
            //NOTE: buildOptions.embed for .NET Core ignores the path structure so do a lookup by name
            var index = resourcePath.LastIndexOf("\\", StringComparison.Ordinal) + 1;
            var name = resourcePath.Substring(index, resourcePath.Length-index);
            var resourceName = Assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(name));

            return Assembly.GetManifestResourceStream(resourceName);
        }
    }
}
