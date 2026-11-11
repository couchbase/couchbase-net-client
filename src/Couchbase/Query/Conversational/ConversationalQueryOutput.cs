using Couchbase.Core.Compatibility;

#nullable enable

namespace Couchbase.Query.Conversational;

/// <summary>
/// The kind of statement the model generates from a prompt.
/// </summary>
[InterfaceStability(Level.Volatile)]
public enum ConversationalQueryOutput
{
    /// <summary>
    /// A SQL++ statement.
    /// </summary>
    Sqlpp,

    /// <summary>
    /// A SQL++ statement that uses Full Text Search.
    /// </summary>
    FtsSqlpp,

    /// <summary>
    /// A JavaScript user defined function.
    /// </summary>
    JsUdf
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
