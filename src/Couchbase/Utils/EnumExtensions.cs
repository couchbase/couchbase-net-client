using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

#nullable enable

namespace Couchbase.Utils
{
    internal static class EnumExtensions
    {
        private static class EnumDescriptionCache<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>
            where T : struct, Enum
        {
            private static readonly Dictionary<T, string> ToDescriptionCache = new();
            private static readonly Dictionary<string, T> FromDescriptionCache = new();

            static EnumDescriptionCache()
            {
                foreach (var field in typeof(T).GetFields())
                {
                    var description = field.GetCustomAttribute<DescriptionAttribute>();
                    if (description != null)
                    {
                        var value = (T)field.GetValue(null)!;
                        ToDescriptionCache.Add(value, description.Description);

                        // In case two fields have the same description, only keep the first we encounter
#if NETSTANDARD2_0
                        if (!FromDescriptionCache.ContainsKey(description.Description))
                        {
                            FromDescriptionCache.Add(description.Description, value);
                        }
#else
                        FromDescriptionCache.TryAdd(description.Description, value);
#endif
                    }
                }
            }

            public static bool TryGetDescription(T value, [MaybeNullWhen(false)] out string description) =>
                ToDescriptionCache.TryGetValue(value, out description);

            public static bool TryGetValue(string description, out T value) =>
                FromDescriptionCache.TryGetValue(description, out value);
        }

        public static string? GetDescription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
            this T? value)
            where T : struct, Enum =>
            value?.GetDescription();

        public static string? GetDescription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
            this T value)
            where T : struct, Enum
        {
            if (!EnumDescriptionCache<T>.TryGetDescription(value, out var description))
            {
                return null;
            }

            return description;
        }

        public static bool TryGetFromDescription<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicFields)] T>(
            string description, out T @enum)
            where T : struct, Enum =>
            EnumDescriptionCache<T>.TryGetValue(description, out @enum);
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
