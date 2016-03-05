using System;

namespace Couchbase.Core
{
    public interface IMutateInBuilder<TDocument>
    {
        long Cas { get; }

        string Key { get; }

        TimeSpan Expiry { get; }

        PersistTo PersistTo { get; }

        ReplicateTo ReplicateTo { get; }

        IMutateInBuilder<TDocument> Insert(string path, object value, bool createParents = true);

        IMutateInBuilder<TDocument> Upsert(string path, object value, bool createParents = true);

        IMutateInBuilder<TDocument> Replace(string path, object value);

        IMutateInBuilder<TDocument> Remove(string path);

        IMutateInBuilder<TDocument> PushBack(string path, object value, bool createParents = true);

        IMutateInBuilder<TDocument> PushFront(string path, object value, bool createParents = true);

        IMutateInBuilder<TDocument> ArrayInsert(string path, object value);

        IMutateInBuilder<TDocument> AddUnique(string path, object value, bool createParents = true);

        IMutateInBuilder<TDocument> Counter(string path, long delta, bool createParents = true);

        IMutateInBuilder<TDocument> WithExpiry(TimeSpan expiry);

        IMutateInBuilder<TDocument> WithCas(long cas);

        IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo);

        IMutateInBuilder<TDocument> WithDurability(ReplicateTo replicateTo);

        IMutateInBuilder<TDocument> WithDurability(PersistTo persistTo, ReplicateTo replicateTo);

        IDocumentFragment<TDocument> Execute();
    }
}
