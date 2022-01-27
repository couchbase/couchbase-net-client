using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;

namespace Couchbase.Transactions.Tests.UnitTests
{
    public static class ResourceHelper
    {
        private static readonly Assembly Assembly = typeof(ResourceHelper).GetTypeInfo().Assembly;

        public static T ReadResource<T>(string resourcePath)
        {
            return JsonConvert.DeserializeObject<T>(ReadResource(resourcePath));
        }

        public static string ReadResource(string resourcePath)
        {
            using (var stream = ReadResourceAsStream(resourcePath))
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

        public static List<string> ReadResourceAsArray(string resourcePath)
        {
            using (var stream = ReadResourceAsStream(resourcePath))
            {
                if (stream == null)
                {
                    return null;
                }

                var resources = new List<string>();
                using (var reader = new StreamReader(stream))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        resources.Add(line);
                    }
                }

                return resources;
            }
        }

        public static Stream ReadResourceAsStream(string resourcePath)
        {
            //NOTE: buildOptions.embed for .NET Core ignores the path structure so do a lookup by name
            var index = resourcePath.LastIndexOf("\\", StringComparison.Ordinal) + 1;
            var name = resourcePath.Substring(index, resourcePath.Length - index);
            var resourceName = Assembly.GetManifestResourceNames().FirstOrDefault(x => x.Contains(name));

            return Assembly.GetManifestResourceStream(resourceName);
        }
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
 *
 *    Licensed under the Apache License, Version 2.0 (the "License");
 *    you may not use this file except in compliance with the License.
 *    You may obtain a copy of the License at
 *
 *        http://www.apache.org/licenses/LICENSE-2.0
 *
 *    Unless required by applicable law or agreed to in writing, software
 *    distributed under the License is distributed on an "AS IS" BASIS,
 *    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *    See the License for the specific language governing permissions and
 *    limitations under the License.
 *
 * ************************************************************/