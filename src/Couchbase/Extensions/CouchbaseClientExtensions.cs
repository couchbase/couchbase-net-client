using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Enyim.Caching.Memcached;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Enyim.Caching.Memcached.Results;
using Enyim.Caching.Memcached.Results.Extensions;
using System.Reflection;
using Couchbase.Helpers;
using Couchbase.Operations;

namespace Couchbase.Extensions
{
    public static class CouchbaseClientExtensions
    {
        public static JsonSerializerSettings JsonSerializerSettings;

        static CouchbaseClientExtensions()
        {
            JsonSerializerSettings = new JsonSerializerSettings
                {
                    ContractResolver = new DocumentIdContractResolver()
                };
        }

        private const string Null = "null";

        #region No expiry

        public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json);
        }

        public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json).Success;
        }

        public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, PersistTo persistTo, ReplicateTo replicateTo = ReplicateTo.Zero)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json, persistTo, replicateTo);
        }

        public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, cas);
        }

        public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, cas).Success;
        }

        #endregion

        #region DateTime expiry

        public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json, expiresAt);
        }

        public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json, expiresAt).Success;
        }

        public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, DateTime expiresAt, ulong cas)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, expiresAt, cas);
        }

        public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas, DateTime expiresAt)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, expiresAt, cas).Success;
        }

        #endregion

        #region TimeSpan expiry

        public static IStoreOperationResult ExecuteStoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json, validFor);
        }

        public static bool StoreJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor)
        {
            var json = SerializeObject(value);
            return client.ExecuteStore(mode, key, json, validFor).Success;
        }

        public static IStoreOperationResult ExecuteCasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, TimeSpan validFor, ulong cas)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, validFor, cas);
        }

        public static bool CasJson(this ICouchbaseClient client, StoreMode mode, string key, object value, ulong cas, TimeSpan validFor)
        {
            var json = SerializeObject(value);
            return client.ExecuteCas(mode, key, json, validFor, cas).Success;
        }

        #endregion

        internal static bool IsArrayOrCollection(Type type)
        {
            return type.GetInterface(typeof (IEnumerable<>).FullName) != null || type.Name== typeof(IEnumerable<>).Name;
        }

        public static T GetJson<T>(this ICouchbaseClient client, string key) where T : class
        {
            var json = client.Get<string>(key);
            return json == null || json == Null ? null : DeserializeObject<T>(key, json);
        }

        public static IGetOperationResult<T> ExecuteGetJson<T>(this ICouchbaseClient client, string key) where T : class
        {
            var result = client.ExecuteGet<string>(key);
            var retVal = new GetOperationResult<T>();
            result.Combine(retVal);
            retVal.Cas = result.Cas;

            if (! result.Success)
            {
                return retVal;
            }
            retVal.Value = DeserializeObject<T>(key, result.Value);
            return retVal;
        }

        private static T DeserializeObject<T>(string key, string value)
        {
            if (!IsArrayOrCollection(typeof(T)))
            {
                value = DocHelper.InsertId(value, key);
            }
            return JsonConvert.DeserializeObject<T>(value, JsonSerializerSettings);
        }

        private static string SerializeObject(object value)
        {
            var json = JsonConvert.SerializeObject(value,
                                    Formatting.None,
                                    JsonSerializerSettings);
            return json;
        }

        private class DocumentIdContractResolver : CamelCasePropertyNamesContractResolver
        {
            protected override List<MemberInfo> GetSerializableMembers(Type objectType)
            {
                return base.GetSerializableMembers(objectType).Where(o => o.Name != "Id").ToList();
            }
        }
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2012 Couchbase, Inc.
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

#endregion
