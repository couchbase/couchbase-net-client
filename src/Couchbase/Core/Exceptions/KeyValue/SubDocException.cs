namespace Couchbase.Core.Exceptions.KeyValue;

/// <summary>
/// This exception handles unrecoverable Sub-Doc API errors. It resolves the naming issue with <see cref="SubdocExceptionException"/>.
/// </summary>
#pragma warning disable CS0618 // Type or member is obsolete
public class SubDocException : SubdocExceptionException
#pragma warning restore CS0618 // Type or member is obsolete
{
    public SubDocException()
    {
    }

    public SubDocException(IErrorContext context) : base(context)
    {
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
