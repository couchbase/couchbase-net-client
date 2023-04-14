using System;

#nullable enable

namespace Couchbase.Core.Configuration.Server
{
    internal interface IConfigHandler : IDisposable
    {
        /// <summary>
        /// Starts the config handler
        /// </summary>
        /// <param name="withPolling">Enable polling via CCCP.</param>
        void Start(bool withPolling = false);

        /// <summary>
        /// Publishes a config to the handler and any subscribers
        /// </summary>
        /// <param name="config">The <see cref="BucketConfig"/> to publish.</param>
        void Publish(BucketConfig config);

        /// <summary>
        /// Subscribe to the config handler to receive cluster map updates.
        /// </summary>
        /// <param name="configSubscriber">The <see cref="IConfigUpdateEventSink"/> subscriber.</param>
        void Subscribe(IConfigUpdateEventSink configSubscriber);

        /// <summary>
        /// Unsubscribes the subscriber.
        /// </summary>
        /// <param name="configSubscriber"></param>
        void Unsubscribe(IConfigUpdateEventSink configSubscriber);

        /// <summary>
        /// Fetch a <see cref="BucketConfig"/> from the listener if they are a subscriber.
        /// </summary>
        /// <param name="bucketName">The name of the bucket.</param>
        /// <returns>A <see cref="BucketConfig"/> for a <see cref="IBucket"/> subscriber.</returns>
        BucketConfig Get(string bucketName);

        /// <summary>
        /// Clears the subscribers.
        /// </summary>
        void Clear();
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
