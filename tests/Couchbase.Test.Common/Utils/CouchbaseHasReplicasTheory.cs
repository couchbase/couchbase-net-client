using System;
using System.Collections.Generic;
using System.Text;
using Xunit;

namespace Couchbase.IntegrationTests.Utils
{
    /// <summary>
    /// A <c>Theory</c> that may be skipped due to the CB_SERVER_VERSION environment variable.
    /// </summary>
    /// <remarks>
    /// Checking against versions of irregular format requires use of the ExplicitDeny or ExplicitAllow parameters.
    /// </remarks>
    public class CouchbaseHasReplicasTheory : TheoryAttribute
    {
        /// <summary>
        /// Gets or sets the minimum Version necessary to run the test.
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        public int MinNumReplicas { get; set; }

        /// <summary>
        /// Gets or sets the maximum Version necessary to run the test.
        /// </summary>
        /// <remarks>If it does not parse as a Version, it will be ignored.</remarks>
        public int MaxNumReplicas { get; set; }

        /// <summary>
        /// Gets or sets the list of versions that will be considered as no-skip. Supercedes everything but the Skip parameter itself.
        /// </summary>
        public int[] ExplicitAllow { get; set; }

        /// <summary>
        /// Gets or sets the list of versions that will be skipped. Supercedes MinVersion and MaxVersion.
        /// </summary>
        public int[] ExplicitSkip { get; set; }

        /// <summary>
        /// Gets or sets a value indicating this test should be skipped.  Supercedes all other version checking.
        /// </summary>
        public override string Skip
        {
            get => CouchbaseHasReplicasFact.SkipBasedOnNumReplicas(base.Skip, ExplicitAllow, ExplicitSkip, MinNumReplicas, MaxNumReplicas);
            set => base.Skip = value;
        }
    }
}