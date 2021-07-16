using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue
{
    /// <summary>
    /// Strongly typed version of <see cref="MutateInSpecBuilder"/>.
    /// </summary>
    /// <typeparam name="TDocument">Type of the whole document.</typeparam>
    public class MutateInSpecBuilder<TDocument> : MutateInSpecBuilder, ITypeSerializerProvider
    {
        /// <inheritdoc />
        public ITypeSerializer Serializer { get; }

        /// <summary>
        /// Creates a new MutateInSpecBuilder.
        /// </summary>
        /// <param name="serializer">Type serializer used for generating paths from lambda expressions.</param>
        public MutateInSpecBuilder(ITypeSerializer serializer)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (serializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }

            Serializer = serializer;
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
