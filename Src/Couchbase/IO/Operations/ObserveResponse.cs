namespace Couchbase.IO.Operations
{
    public enum ObserveResponse
    {
        DurabilitySatisfied,
        DurabilityNotSatisfied
    }

    public enum Durability
    {
        Satisfied,
        NotSatisfied,
        Unspecified
    }
}
