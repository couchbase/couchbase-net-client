#nullable enable
using System.Collections.Generic;
using Couchbase.Test.Common.Configuration.Model;

namespace Couchbase.Test.Common.Configuration.Scenarios
{
    /// <summary>
    /// What a running <see cref="Scenario"/> is made of, for test code in a step and for asserts after a run.
    /// </summary>
    internal sealed class ScenarioContext(ConfigHarness harness, ClusterModel model, ModelServer server,
        IReadOnlyDictionary<string, CouchbaseBucket> buckets)
    {
        public ConfigHarness Harness { get; } = harness;

        public ClusterModel Model { get; } = model;

        public ModelServer Server { get; } = server;

        /// <summary>
        /// The SDK bucket of each model bucket.
        /// </summary>
        public IReadOnlyDictionary<string, CouchbaseBucket> Buckets { get; } = buckets;

        /// <summary>
        /// The SDK bucket, the first bucket of the model when the name is null.
        /// </summary>
        public CouchbaseBucket Bucket(string? name = null) => Buckets[name ?? Model.Buckets[0]];

        /// <summary>
        /// What the SDK has for the bucket now, the first bucket of the model when the name is null.
        /// </summary>
        public ConfigHarness.BucketView View(string? name = null) => Harness.Bucket(name ?? Model.Buckets[0]);

        /// <summary>
        /// The model hostname for a host. A name with no dot gets the model domain, so node-1 is node-1.example.com.
        /// </summary>
        public static string Hostname(string host) => host.IndexOf('.') < 0 ? $"{host}.{ClusterModel.Domain}" : host;
    }
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
