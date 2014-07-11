namespace Couchbase.IO.Operations
{
    /// <summary>
    /// In an Observe operation, indicates whether the key is persisted or not.
    /// </summary>
    public enum KeyState : byte
    {
        /// <summary>
        /// Found, not persisted. Indicates key is in RAM, but not persisted to disk
        /// </summary>
        FoundNotPersisted = 0x00,

        /// <summary>
        /// Found, persisted. Indicates key is found in RAM, and is persisted to disk
        /// </summary>
        FoundPersisted = 0x01,

        /// <summary>
        /// Not found. Indicates the key is persisted, but not found in RAM. In this case,
        /// a key is not available in any view/index. Couchbase Server will return this keystate
        /// for any item that is not stored in the server. It indicates you will not expect to have
        /// the item in a view/index.
        /// </summary>
        NotFound = 0x80,

        /// <summary>
        /// Logically deleted. Indicates an item is in RAM, but is not yet deleted from disk.
        /// </summary>
        LogicalDeleted = 0x81
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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

#endregion [ License information ]