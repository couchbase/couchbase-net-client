using System;
using System.Collections.Generic;
using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The result of a conversational query or of a chat prompt.
/// </summary>
/// <typeparam name="T">The type of each row.</typeparam>
[InterfaceStability(Level.Volatile)]
public interface IConversationalQueryResult<out T> : IDisposable, IAsyncEnumerable<T>
{
    /// <summary>
    /// The rows of the generated statement. It is empty when the statement did not run.
    /// </summary>
    /// <remarks>The rows can be read only once.</remarks>
    IAsyncEnumerable<T> Rows { get; }

    /// <summary>
    /// The statement the model generated, if any. It is available before the rows are read.
    /// </summary>
    string? GeneratedStatement { get; }

    /// <summary>
    /// The metadata of the response. The metrics are complete only after the rows are read.
    /// </summary>
    ConversationalQueryMetaData MetaData { get; }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2026 Couchbase, Inc.
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
