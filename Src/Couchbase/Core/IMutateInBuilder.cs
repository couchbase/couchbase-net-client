
using System;

namespace Couchbase.Core
{
    public interface IMutateInBuilder
    {
        long Cas { get; }

        string Key { get; }

        TimeSpan Expiry { get; }

        PersistTo PersistTo { get; }

        ReplicateTo ReplicateTo { get; }

        IMutateInBuilder Insert(string path, object value, bool createParents = true);

        IMutateInBuilder Upsert(string path, object value, bool createParents = true);

        IMutateInBuilder Replace(string path, object value);

        IMutateInBuilder Remove(string path);

        IMutateInBuilder PushBack(string path, object value, bool createParents = true);

        IMutateInBuilder PushFront(string path, object value, bool createParents = true);

        IMutateInBuilder ArrayInsert(string path, object value);

        IMutateInBuilder AddUnique(string path, object value, bool createParents = true);

        IMutateInBuilder Counter(string path, long delta, bool createParents = true);

        IMutateInBuilder WithExpiry(TimeSpan expiry);

        IMutateInBuilder WithCas(long cas);

        IMutateInBuilder WithDurability(PersistTo persistTo);

        IMutateInBuilder WithDurability(ReplicateTo replicateTo);

        IMutateInBuilder WithDurability(PersistTo persistTo, ReplicateTo replicateTo);

        IDocumentFragment<T> Execute<T>();
    }
}
