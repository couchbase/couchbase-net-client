#nullable enable
using Couchbase.KeyValue;

namespace Couchbase.Client.Transactions.DataModel
{
    internal record ShortStringDurabilityLevel(DurabilityLevel Value)
    {
        public static readonly ShortStringDurabilityLevel Majority = new(DurabilityLevel.Majority);
        public static readonly ShortStringDurabilityLevel MajorityAndPersistToActive = new(DurabilityLevel.MajorityAndPersistToActive);
        public static readonly ShortStringDurabilityLevel PersistToMajority = new(DurabilityLevel.PersistToMajority);
        public static readonly ShortStringDurabilityLevel None = new(DurabilityLevel.None);
        public static ShortStringDurabilityLevel? FromString(string? s) => s switch
        {
            "m" => Majority,
            "pa" => MajorityAndPersistToActive,
            "pm" => PersistToMajority,
            "n" => None,
            _ => null // For future, unrecognized values
        };

        public override string ToString() => Value switch
        {
            DurabilityLevel.Majority => "m",
            DurabilityLevel.MajorityAndPersistToActive => "pa",
            DurabilityLevel.PersistToMajority => "pm",
            DurabilityLevel.None => "n",
            _ => Value.ToString()
        };
    }
}





