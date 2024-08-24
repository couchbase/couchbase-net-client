using System;
using System.Diagnostics.CodeAnalysis;
using Newtonsoft.Json;

namespace Couchbase.Core.IO.Serializers
{
    /// <summary>
    /// <see cref="JsonConverter"/> that wraps an <see cref="ICustomObjectCreator"/> to support Json.Net deserialization.
    /// </summary>
    /// <remarks>
    /// Used by <see cref="DefaultSerializer"/> if an <see cref="ICustomObjectCreator"/> is supplied.
    /// </remarks>
    [RequiresUnreferencedCode(DefaultSerializer.UnreferencedCodeMessage)]
    internal sealed class JsonNetCustomObjectCreatorWrapper : JsonConverter
    {
        private readonly ICustomObjectCreator _creator;

        public JsonNetCustomObjectCreatorWrapper(ICustomObjectCreator creator)
        {
            _creator = creator ?? throw new ArgumentNullException(nameof(creator));
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            throw new NotImplementedException();
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            object obj = _creator.CreateObject(objectType);
            if (obj == null)
            {
                throw new NullReferenceException("ICustomObjectCreator returned a null reference.");
            }

            serializer.Populate(reader, obj);

            return obj;
        }

        public override bool CanConvert(Type objectType)
        {
            return _creator.CanCreateObject(objectType);
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
