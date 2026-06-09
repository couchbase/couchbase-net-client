using System;
using Couchbase.Grpc.Protocol.Shared;

namespace Couchbase.FitPerformer.Utils.Options;

public class DurabilityUtil
{
    public static Couchbase.KeyValue.DurabilityLevel ConvertDurabilityLevel(Couchbase.Grpc.Protocol.Shared.Durability dl)
    {
        switch (dl)
        {
            case Couchbase.Grpc.Protocol.Shared.Durability.None: return Couchbase.KeyValue.DurabilityLevel.None;
            case Couchbase.Grpc.Protocol.Shared.Durability.Majority: return Couchbase.KeyValue.DurabilityLevel.Majority;
            case Couchbase.Grpc.Protocol.Shared.Durability.MajorityAndPersistToActive: return Couchbase.KeyValue.DurabilityLevel.MajorityAndPersistToActive;
            case Couchbase.Grpc.Protocol.Shared.Durability.PersistToMajority: return Couchbase.KeyValue.DurabilityLevel.PersistToMajority;
            default: throw new NotSupportedException();
        }
    }

    public static Couchbase.KeyValue.ReplicateTo convertReplicateTo(Couchbase.Grpc.Protocol.Shared.ReplicateTo dl)
    {
        switch (dl)
        {
            case Couchbase.Grpc.Protocol.Shared.ReplicateTo.None: return Couchbase.KeyValue.ReplicateTo.None;
            case Couchbase.Grpc.Protocol.Shared.ReplicateTo.One: return Couchbase.KeyValue.ReplicateTo.One;
            case Couchbase.Grpc.Protocol.Shared.ReplicateTo.Two: return Couchbase.KeyValue.ReplicateTo.Two;
            case Couchbase.Grpc.Protocol.Shared.ReplicateTo.Three: return Couchbase.KeyValue.ReplicateTo.Three;
            default: throw new NotSupportedException();
        }
    }

    public static Couchbase.KeyValue.PersistTo convertPersistTo(Couchbase.Grpc.Protocol.Shared.PersistTo dl)
    {
        switch (dl)
        {
            case Couchbase.Grpc.Protocol.Shared.PersistTo.None: return Couchbase.KeyValue.PersistTo.None;
            case Couchbase.Grpc.Protocol.Shared.PersistTo.One: return Couchbase.KeyValue.PersistTo.One;
            case Couchbase.Grpc.Protocol.Shared.PersistTo.Two: return Couchbase.KeyValue.PersistTo.Two;
            case Couchbase.Grpc.Protocol.Shared.PersistTo.Three: return Couchbase.KeyValue.PersistTo.Three;
            case Couchbase.Grpc.Protocol.Shared.PersistTo.Four: return Couchbase.KeyValue.PersistTo.Four;
            case Couchbase.Grpc.Protocol.Shared.PersistTo.Active: throw new NotSupportedException();
            default: throw new NotSupportedException();
        }
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.InsertOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.UpsertOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.ReplaceOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.RemoveOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.AppendOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.PrependOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.IncrementOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }

    public static void ConvertDurability(DurabilityType durability, Couchbase.KeyValue.DecrementOptions options)
    {
        if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.DurabilityLevel) options.Durability(ConvertDurabilityLevel(durability.DurabilityLevel));
        else if (durability.DurabilityCase == DurabilityType.DurabilityOneofCase.Observe) options.Durability(convertPersistTo(durability.Observe.PersistTo), convertReplicateTo(durability.Observe.ReplicateTo));
        else throw new NotSupportedException("Unknown durability");
    }
}