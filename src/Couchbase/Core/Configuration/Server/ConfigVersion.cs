using System;

namespace Couchbase.Core.Configuration.Server;

/// <summary>
/// ConfigVersion represents the revision of a Cluster Map via it's Epoch and Revision fields if they exist.
/// These values range from 1 to INT64_MAX, the higher the value the newer the configuration. If Epoch does
/// exist for early server versions, the value will be 0 and only the revision compared. The Epoch should always
/// be compared first and if equal the revision should be compared.
/// <remarks>https://issues.couchbase.com/browse/CBD-4083</remarks>
/// </summary>
internal readonly struct ConfigVersion : IEquatable<ConfigVersion>, IComparable<ConfigVersion>
{
    public ConfigVersion(ulong epoch, ulong revision)
    {
        Epoch = epoch;
        Revision = revision;
    }

    /// <summary>
    /// The Epoch of the version.
    /// </summary>
    ///<remarks>
    /// Note that in all cases the comparision should be done using the
    /// ConfigVersion instance itself as all operators are overridden.
    /// </remarks>
    public ulong Epoch { get; } = 0;

    /// <summary>
    /// The Revision of the version.
    /// </summary>
    ///<remarks>
    /// Note that in all cases the comparision should be done using the
    /// ConfigVersion instance itself as all operators are overridden.
    /// </remarks>
    public ulong Revision { get; }

    public int CompareTo(ConfigVersion other)
    {
        var epochComparison = Epoch.CompareTo(other.Epoch);
        if (epochComparison != 0) return epochComparison;
        return Revision.CompareTo(other.Revision);
    }

    public bool Equals(ConfigVersion other)
    {
        if (Epoch == other.Epoch)
        {
            return Revision == other.Revision;
        }

        return false;
    }

    public override bool Equals(object obj)
    {
        return obj is ConfigVersion other && Equals(other);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            return (Epoch.GetHashCode() * 397) ^ Revision.GetHashCode();
        }
    }

    public static bool operator ==(ConfigVersion left, ConfigVersion right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(ConfigVersion left, ConfigVersion right)
    {
        return !left.Equals(right);
    }

    public static bool operator >=(ConfigVersion left, ConfigVersion right)
    {
        if (left.Epoch == right.Epoch)
        {
            return left.Revision >= right.Revision;
        }

        return left.Epoch >= right.Epoch;
    }

    public static bool operator <=(ConfigVersion left, ConfigVersion right)
    {
        if (left.Epoch == right.Epoch)
        {
            return left.Revision <= right.Revision;
        }

        return left.Epoch <= right.Epoch;
    }

    public static bool operator >(ConfigVersion left, ConfigVersion right)
    {
        if (left.Epoch == right.Epoch)
        {
            return left.Revision > right.Revision;
        }

        return left.Epoch > right.Epoch;
    }

    public static bool operator <(ConfigVersion left, ConfigVersion right)
    {
        if (left.Epoch == right.Epoch)
        {
            return left.Revision < right.Revision;
        }

        return left.Epoch < right.Epoch;
    }

    public override string ToString()
    {
        return $"{Epoch}/{Revision}";
    }
}
