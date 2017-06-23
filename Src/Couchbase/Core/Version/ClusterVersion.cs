using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Couchbase.Core.Version
{
    /// <summary>
    /// Represents the Couchbase Server Cluster version.
    /// </summary>
    public struct ClusterVersion : IEquatable<ClusterVersion>, IComparable<ClusterVersion>
    {
        /// <summary>
        /// Version number.
        /// </summary>
        public System.Version Version { get; internal set; }

        /// <summary>
        /// Build number.
        /// </summary>
        public int Build { get; internal set; }

        /// <summary>
        /// Additional information, such as "community" or "enterprise".
        /// </summary>
        public string Suffix { get; internal set;}

        public ClusterVersion(System.Version version = null, int build = -1, string suffix = null)
        {
            Version = version;
            Build = build < 0 ? -1 : build;
            Suffix = suffix;
        }

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

        #region Comparison

        public override bool Equals(object obj)
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

        public static ClusterVersion Parse(string versionString)
        {
            if (TryParse(versionString, out ClusterVersion version))
            {
                return version;
            }

            throw new FormatException("Invalid version string");
        }

        public static bool TryParse(string versionString, out ClusterVersion version)
        {
            if (versionString == null)
            {
                throw new ArgumentNullException("versionString");
            }

            version = new ClusterVersion
            {
                Build = -1
            };

            var split = versionString.Split('-');

            System.Version mainVersion;
            if (!System.Version.TryParse(split[0], out mainVersion))
            {
                return false;
            }

            version.Version = mainVersion;

            if (split.Length > 1)
            {
                if (int.TryParse(split[1], out int build))
                {
                    version.Build = build;

                    if (split.Length > 2)
                    {
                        version.Suffix = string.Join("-", split.Skip(2));
                    }
                }
                else
                {
                    // No build number, put remainder into Suffix
                    version.Build = -1;
                    version.Suffix = string.Join("-", split.Skip(1));
                }
            }

            return true;
        }

        #endregion
    }
}
