#nullable enable
using Couchbase.Core.Exceptions.KeyValue;

namespace Couchbase.KeyValue;

/// <summary>
/// Provides an interface for 'touching' a document, but instead of throwing
/// <see cref="DocumentNotFoundException"/> exception if the document key is
/// not found, allows for the existence to be checked via <see cref="Exists"/>.
/// </summary>
public interface ITryTouchResult
{
    /// <summary>
    /// If false, the document does not exist on the server for a given key.
    /// </summary>
    bool Exists { get; }

    /// <summary>
    /// The mutation result containing the Cas value after the Touch operation.
    /// If Exists is false, this will be empty.
    /// </summary>
    IMutationResult? MutationResult { get; }
}
/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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
 */
