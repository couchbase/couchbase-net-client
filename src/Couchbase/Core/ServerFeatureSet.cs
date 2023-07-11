using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Couchbase.Core.IO.Operations;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.Core
{
    /// <summary>
    /// Stores an immutable set of server features with faster lookup than searching an array.
    /// </summary>
    internal class ServerFeatureSet
    {
        public static ServerFeatureSet Empty { get; } = new(Array.Empty<ServerFeatures>());

        /// <summary>
        /// List of all available features.
        /// </summary>
        public IReadOnlyCollection<ServerFeatures> Features { get; }

        /// <inheritdoc cref="ServerFeatures.Datatype"/>
        public bool DataType { get; private set; }

        /// <inheritdoc cref="ServerFeatures.TcpNoDelay"/>
        public bool TcpNoDelay { get; private set; }

        /// <inheritdoc cref="ServerFeatures.MutationSeqno"/>
        public bool MutationSeqno { get; private set; }

        /// <inheritdoc cref="ServerFeatures.TcpDelay"/>
        public bool TcpDelay { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SubdocXAttributes"/>
        public bool SubdocXAttributes { get; private set; }

        /// <inheritdoc cref="ServerFeatures.XError"/>
        public bool XError { get; private set; }

        /// <inheritdoc cref="ServerFeatures.ServerDuration"/>
        public bool ServerDuration { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SelectBucket"/>
        public bool SelectBucket { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SnappyCompression"/>
        public bool SnappyCompression { get; private set; }

        /// <inheritdoc cref="ServerFeatures.AlternateRequestSupport"/>
        public bool AlternateRequestSupport { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SynchronousReplication"/>
        public bool SynchronousReplication { get; private set; }

        /// <inheritdoc cref="ServerFeatures.Collections"/>
        public bool Collections { get; private set; }

        /// <inheritdoc cref="ServerFeatures.UnorderedExecution"/>
        public bool UnorderedExecution { get; private set; }

        /// <inheritdoc cref="ServerFeatures.CreateAsDeleted"/>
        public bool CreateAsDeleted { get; private set; }

        /// <inheritdoc cref="ServerFeatures.PreserveTtl"/>
        public bool PreserveTtl { get; private set; }

        /// <inheritdoc cref="ServerFeatures.JSON"/>
        public bool Json { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SubDocReplicaRead"/>
        public bool SubDocReplicaRead { get; private set; }

        /// <inheritdoc cref="ServerFeatures.ClustermapChangeNotificationBrief"/>
        public bool ClustermapChangeNotificationBrief { get; private set; }

        /// <inheritdoc cref="ServerFeatures.Duplex"/>
        public bool Duplex { get; private set; }

        /// <inheritdoc cref="ServerFeatures.GetClusterConfigWithKnownVersion"/>
        public bool GetClusterConfigWithKnownVersion { get; private set; }

        /// <inheritdoc cref="ServerFeatures.SnappyEverywhere"/>
        public bool SnappyEverywhere { get; private set; }

        /// <summary>
        /// Create a new ServerFeatureSet.
        /// </summary>
        /// <param name="features">Features that are available.</param>
        public ServerFeatureSet(ServerFeatures[] features)
        {
            if (features == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(features));
            }

            Features = new ReadOnlyCollection<ServerFeatures>(features);

            ParseFeatures(features);
        }

        /// <summary>
        /// Check for feature support.
        /// </summary>
        /// <param name="feature">Feature to check.</param>
        /// <returns>True if this feature is supported.</returns>
        /// <remarks>
        /// This method is less efficient than using the predefined properties.
        /// </remarks>
        public bool Supports(ServerFeatures feature)
        {
            return Features.Contains(feature);
        }

        private void ParseFeatures(IEnumerable<ServerFeatures> features)
        {
            foreach (var feature in features)
            {
                switch (feature)
                {
                    case ServerFeatures.Datatype:
                        DataType = true;
                        break;

                    case ServerFeatures.TcpNoDelay:
                        TcpNoDelay = true;
                        break;

                    case ServerFeatures.MutationSeqno:
                        MutationSeqno = true;
                        break;

                    case ServerFeatures.TcpDelay:
                        TcpDelay = true;
                        break;

                    case ServerFeatures.SubdocXAttributes:
                        SubdocXAttributes = true;
                        break;

                    case ServerFeatures.XError:
                        XError = true;
                        break;

                    case ServerFeatures.SelectBucket:
                        SelectBucket = true;
                        break;

                    case ServerFeatures.SnappyCompression:
                        SnappyCompression = true;
                        break;

                    case ServerFeatures.ServerDuration:
                        ServerDuration = true;
                        break;

                    case ServerFeatures.AlternateRequestSupport:
                        AlternateRequestSupport = true;
                        break;

                    case ServerFeatures.SynchronousReplication:
                        SynchronousReplication = true;
                        break;

                    case ServerFeatures.Collections:
                        Collections = true;
                        break;

                    case ServerFeatures.UnorderedExecution:
                        UnorderedExecution = true;
                        break;

                    case ServerFeatures.CreateAsDeleted:
                        CreateAsDeleted = true;
                        break;

                    case ServerFeatures.PreserveTtl:
                        PreserveTtl = true;
                        break;

                    case ServerFeatures.JSON:
                        Json = true;
                        break;

                    case ServerFeatures.SubDocReplicaRead:
                        SubDocReplicaRead = true;
                        break;

                    case ServerFeatures.ClustermapChangeNotificationBrief:
                        ClustermapChangeNotificationBrief = true;
                        break;

                    case ServerFeatures.Duplex:
                        Duplex = true;
                        break;

                    case ServerFeatures.GetClusterConfigWithKnownVersion:
                        GetClusterConfigWithKnownVersion = true;
                        break;

                    case ServerFeatures.SnappyEverywhere:
                        SnappyEverywhere = true;
                        break;
                }
            }
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
