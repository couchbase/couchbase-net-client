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
        public static string ReadResource(string resourcePath)
        {
            var resourceName = Assembly.GetExecutingAssembly().GetName().Name + "." + resourcePath.Replace("\\", ".");

            using (var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName))
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
            var resourceName = Assembly.GetExecutingAssembly().GetName().Name + "." + resourcePath.Replace("\\", ".");
            return Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
        }
    }
}
