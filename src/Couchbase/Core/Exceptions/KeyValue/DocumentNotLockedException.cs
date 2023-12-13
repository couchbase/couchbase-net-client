using System;

namespace Couchbase.Core.Exceptions.KeyValue;

/// <summary>
///  Thrown when the server reports the document is already locked - generally raised when an unlocking operation is being performed.
/// </summary>
public class DocumentNotLockedException : CouchbaseException
{
    public DocumentNotLockedException()
    {}

    public DocumentNotLockedException(string message) : base(message)
    {
    }

    public DocumentNotLockedException(string message, Exception exception) : base(message, exception)
    {
    }
}

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2023 Couchbase, Inc.
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
