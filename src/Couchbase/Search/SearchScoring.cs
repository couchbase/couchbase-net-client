using Couchbase.Core.Compatibility;
using Newtonsoft.Json.Linq;

#nullable enable

namespace Couchbase.Search
{
    /// <summary>
    /// Controls how the search service scores the results of a request.
    /// </summary>
    /// <remarks>
    /// The fusion strategies control how the FTS and vector result sets of a hybrid request - one which
    /// combines a search query with a vector search - are merged into a single ranked list. They require
    /// a version of Couchbase Server that supports score fusion. Applied to a single result set they
    /// re-score the hits, but leave their ordering unchanged.
    /// <para>
    /// Under a fusion strategy the top-level <c>boost</c> of the search query and of each vector query is
    /// that side's fusion weight. A boost of 2.0 on the search query and 1.0 on the vector query counts the
    /// search side twice as much as the vector side. Boosts within a compound query keep their existing
    /// meaning, scaling a clause within the FTS score.
    /// </para>
    /// </remarks>
    [InterfaceStability(Level.Uncommitted)]
    public abstract class SearchScoring
    {
        internal const string PropParams = "params";
        internal const string PropRankConstant = "score_rank_constant";
        internal const string PropWindowSize = "score_window_size";

        internal SearchScoring()
        {
        }

        /// <summary>
        /// The value of the top-level "score" field sent to the server.
        /// </summary>
        internal abstract string ScoreValue { get; }

        /// <summary>
        /// Whether the cluster must support score fusion for this scoring mode to be used.
        /// </summary>
        internal virtual bool RequiresScoreFusionCapability => true;

        /// <summary>
        /// The contents of the top-level "params" object, or null if no parameters were set.
        /// </summary>
        internal virtual JObject? ExportParams() => null;

        /// <summary>
        /// Merges the FTS and vector result sets by rank rather than by raw score. The recommended
        /// fusion strategy; it works well with the server defaults.
        /// </summary>
        /// <returns>A <see cref="ReciprocalRankFusionScoring"/> for chaining method calls.</returns>
        public static ReciprocalRankFusionScoring ReciprocalRankFusion() => new();

        /// <summary>
        /// Merges the FTS and vector result sets by normalized score rather than by rank.
        /// </summary>
        /// <returns>A <see cref="RelativeScoreFusionScoring"/> for chaining method calls.</returns>
        public static RelativeScoreFusionScoring RelativeScoreFusion() => new();

        /// <summary>
        /// Disables scoring. This is not a fusion strategy, and works against any server version.
        /// </summary>
        /// <returns>A <see cref="NoneScoring"/>.</returns>
        public static NoneScoring None() => new();

        private protected static JObject? ExportParams(uint? rankConstant, uint? windowSize)
        {
            if (rankConstant is null && windowSize is null)
            {
                // The "params" object is omitted entirely when nothing was set.
                return null;
            }

            var parameters = new JObject();
            if (rankConstant.HasValue)
            {
                parameters.Add(new JProperty(PropRankConstant, rankConstant.Value));
            }
            if (windowSize.HasValue)
            {
                parameters.Add(new JProperty(PropWindowSize, windowSize.Value));
            }

            return parameters;
        }
    }

    /// <summary>
    /// Reciprocal Rank Fusion. Merges the FTS and vector result sets by rank rather than by raw score.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public sealed class ReciprocalRankFusionScoring : SearchScoring
    {
        private uint? _rankConstant;
        private uint? _windowSize;

        internal ReciprocalRankFusionScoring()
        {
        }

        internal override string ScoreValue => "rrf";

        internal override JObject? ExportParams() => ExportParams(_rankConstant, _windowSize);

        /// <summary>
        /// The constant added to each rank before it is inverted. Larger values flatten the influence
        /// of the top ranks. The server defaults this to 60.
        /// </summary>
        /// <param name="rankConstant">The rank constant.</param>
        /// <returns>The <see cref="ReciprocalRankFusionScoring"/> for chaining method calls.</returns>
        public ReciprocalRankFusionScoring RankConstant(uint rankConstant)
        {
            _rankConstant = rankConstant;
            return this;
        }

        /// <summary>
        /// How many results per list are considered for fusion. The server defaults this to the
        /// limit of the request.
        /// </summary>
        /// <param name="windowSize">The window size.</param>
        /// <returns>The <see cref="ReciprocalRankFusionScoring"/> for chaining method calls.</returns>
        public ReciprocalRankFusionScoring WindowSize(uint windowSize)
        {
            _windowSize = windowSize;
            return this;
        }
    }

    /// <summary>
    /// Relative Score Fusion. Merges the FTS and vector result sets by normalized score rather than by rank.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public sealed class RelativeScoreFusionScoring : SearchScoring
    {
        private uint? _windowSize;

        internal RelativeScoreFusionScoring()
        {
        }

        internal override string ScoreValue => "rsf";

        internal override JObject? ExportParams() => ExportParams(rankConstant: null, _windowSize);

        /// <summary>
        /// How many results per list are considered for fusion. The server defaults this to the
        /// limit of the request.
        /// </summary>
        /// <param name="windowSize">The window size.</param>
        /// <returns>The <see cref="RelativeScoreFusionScoring"/> for chaining method calls.</returns>
        public RelativeScoreFusionScoring WindowSize(uint windowSize)
        {
            _windowSize = windowSize;
            return this;
        }
    }

    /// <summary>
    /// Disables scoring. Not a fusion strategy; it sends the same value that the deprecated
    /// <see cref="SearchOptions.DisableScoring"/> sends, and works against any server version.
    /// </summary>
    [InterfaceStability(Level.Uncommitted)]
    public sealed class NoneScoring : SearchScoring
    {
        internal NoneScoring()
        {
        }

        internal override string ScoreValue => "none";

        internal override bool RequiresScoreFusionCapability => false;
    }
}

#region [ License information          ]

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

#endregion
