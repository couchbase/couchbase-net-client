using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

#nullable enable

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Represents the Couchbase Server Cluster version.
    /// </summary>
    public readonly struct ClusterVersion : IEquatable<ClusterVersion>, IComparable<ClusterVersion>
    {
        /// <summary>
        /// Version number.
        /// </summary>
        public System.Version? Version { get; }

        /// <summary>
        /// Build number.
        /// </summary>
        public int Build { get; }

        /// <summary>
        /// Additional information, such as "community" or "enterprise".
        /// </summary>
        public string? Suffix { get; }

        /// <summary>
        /// Create a new ClusterVersion.
        /// </summary>
        /// <param name="version">Version number.</param>
        /// <param name="build">Build number.</param>
        /// <param name="suffix">Additional information, such as "community" or "enterprise".</param>
        public ClusterVersion(System.Version? version = null, int build = -1, string? suffix = null)
        {
            Version = version;
            Build = build < 0 ? -1 : build;
            Suffix = suffix;
        }

        /// <inheritdoc />
        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append(Version);

            if (Build >= 0)
            {
                builder.AppendFormat("-{0}", Build);
            }

            if (!string.IsNullOrEmpty(Suffix))
            {
                builder.AppendFormat("-{0}", Suffix);
            }

            return builder.ToString();
        }

        #region Deconstructors

        /// <summary>
        /// Deconstruct into a tuple.
        /// </summary>
        public void Deconstruct(out System.Version? version, out int build)
        {
            version = Version;
            build = Build;
        }

        /// <summary>
        /// Deconstruct into a tuple.
        /// </summary>
        public void Deconstruct(out System.Version? version, out int build, out string? suffix)
        {
            version = Version;
            build = Build;
            suffix = Suffix;
        }

        #endregion

        #region Comparison

        public override bool Equals(object? obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            return obj is ClusterVersion && Equals((ClusterVersion) obj);
        }

        public bool Equals(ClusterVersion other)
        {
            return Equals(Version, other.Version) && Build == other.Build && string.Equals(Suffix, other.Suffix);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = (Version != null ? Version.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ Build;
                hashCode = (hashCode * 397) ^ (Suffix != null ? Suffix.GetHashCode() : 0);
                return hashCode;
            }
        }

        public int CompareTo(ClusterVersion other)
        {
            if (Version == null && other.Version == null)
            {
                return 0;
            }
            else if (Version == null)
            {
                return -1;
            }
            else if (other.Version == null)
            {
                return 1;
            }

            var versionComparison = Comparer<System.Version>.Default.Compare(Version, other.Version);
            if (versionComparison != 0)
            {
                return versionComparison;
            }

            var buildComparison = Build.CompareTo(other.Build);
            if (buildComparison != 0)
            {
                return buildComparison;
            }

            return string.Compare(Suffix, other.Suffix, StringComparison.Ordinal);
        }

        public static bool operator ==(ClusterVersion left, ClusterVersion right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ClusterVersion left, ClusterVersion right)
        {
            return !left.Equals(right);
        }

        public static bool operator <(ClusterVersion left, ClusterVersion right)
        {
            if (left.Version == right.Version)
            {
                if (left.Build == right.Build)
                {
                    return string.Compare(left.Suffix ?? "", right.Suffix ?? "", StringComparison.Ordinal) < 0;
                }

                return left.Build < right.Build;
            }

            return left.Version < right.Version;
        }

        public static bool operator >(ClusterVersion left, ClusterVersion right)
        {
            if (left.Version == right.Version)
            {
                if (left.Build == right.Build)
                {
                    return string.Compare(left.Suffix ?? "", right.Suffix ?? "", StringComparison.Ordinal) > 0;
                }

                return left.Build > right.Build;
            }

            return left.Version > right.Version;
        }

        public static bool operator <=(ClusterVersion left, ClusterVersion right)
        {
            if (left.Version == right.Version)
            {
                if (left.Build == right.Build)
                {
                    return string.Compare(left.Suffix ?? "", right.Suffix ?? "", StringComparison.Ordinal) <= 0;
                }

                return left.Build < right.Build;
            }

            return left.Version < right.Version;
        }

        public static bool operator >=(ClusterVersion left, ClusterVersion right)
        {
            if (left.Version == right.Version)
            {
                if (left.Build == right.Build)
                {
                    return string.Compare(left.Suffix ?? "", right.Suffix ?? "", StringComparison.Ordinal) >= 0;
                }

                return left.Build > right.Build;
            }

            return left.Version > right.Version;
        }

        #endregion

        #region Parsing

        /// <summary>
        /// Parse a string to a <see cref="ClusterVersion"/>.
        /// </summary>
        /// <param name="versionString">String to parse.</param>
        /// <returns>The parsed <see cref="ClusterVersion"/>.</returns>
        /// <exception cref="FormatException"><paramref name="versionString"/> is not a valid version string.</exception>
        public static ClusterVersion Parse(string versionString)
        {
            if (TryParse(versionString, out ClusterVersion version))
            {
                return version;
            }

            throw new FormatException("Invalid version string");
        }

        /// <summary>
        /// Parse a string to a <see cref="ClusterVersion"/>.
        /// </summary>
        /// <param name="versionString">String to parse.</param>
        /// <param name="version">The parsed <see cref="ClusterVersion"/>.</param>
        /// <returns>True if parsed successfully.</returns>
        public static bool TryParse(string versionString, out ClusterVersion version)
        {
            if (versionString == null)
            {
                throw new ArgumentNullException(nameof(versionString));
            }

            var split = versionString.Split('-');

            if (!System.Version.TryParse(split[0], out var versionNumber))
            {
                version = new ClusterVersion();
                return false;
            }

            if (split.Length > 1)
            {
                if (int.TryParse(split[1], out var build))
                {
                    version = new ClusterVersion(versionNumber, build,
                        split.Length > 2 ? string.Join("-", split.Skip(2)) : null);
                }
                else
                {
                    // No build number, put remainder into Suffix
                    version = new ClusterVersion(versionNumber, -1, string.Join("-", split.Skip(1)));
                }
            }
            else
            {
                version = new ClusterVersion(versionNumber);
            }

            return true;
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
