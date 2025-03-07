using System;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Core.IO.Operations;

namespace Couchbase.KeyValue;

/// <summary>
/// Base class for operation results that do not throw <see cref="DocumentNotFoundException"/>.
/// </summary>
internal abstract class TryResultBase
{
    /// <summary>
    /// The <see cref="ResponseStatus"/> returned from the server. In this case
    /// it should only be <see cref="ResponseStatus.Success"/> or <see cref="ResponseStatus.KeyNotFound"/>
    /// </summary>
    internal ResponseStatus Status { get; init; }

    /// <summary>
    /// If false, the document does not exist on the server for a given key.  Use this and not the Exists property which
    /// will be deprecated in the future.
    /// </summary>
    public virtual bool DocumentExists
    {
        get
        {
            return Status switch
            {
                ResponseStatus.Success => true,
                ResponseStatus.SubdocMultiPathFailureDeleted => true,
                ResponseStatus.SubDocMultiPathFailure => true,
                ResponseStatus.None => false,
                ResponseStatus.KeyNotFound => false,
                _ => throw new InvalidOperationException(
                    $"Only Success or KeyNotFound expected, {Status} was received.")
            };
        }
    }
    ///<summary>Exists collides with Exists in ITryLookupInResult, so DocExists is a synonym for this.   We will
    /// deprecate this eventually.</summary>
   /// [Obsolete("Use DocumentExists instead.")]
    public virtual bool Exists => DocumentExists;

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
