using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Couchbase.Analytics;
using Couchbase.Authentication;
using Couchbase.Configuration.Client;
using Couchbase.Core.Monitoring;
using Couchbase.Core.Version;
using Couchbase.IO.Operations;
using Couchbase.Management;
using Couchbase.N1QL;
using Couchbase.Search;
using Couchbase.Views;

namespace Couchbase.Core
{
    /// <summary>
    /// Represents a Couchbase Bucket object for performing CRUD operations on Documents and Key/Value pairs, View
    /// queries, and executing N1QL queries.
    /// </summary>
    public interface IBucket : IDisposable
    {
        /// <summary>
        /// The name of the Couchbase Bucket. This is visible from the Couchbase Server Management Console.
        /// </summary>
        string Name { get; }

        /// <summary>
        /// Returns type of the bucket (either Couchbase or Memcached)
        /// </summary>
        Buckets.BucketTypeEnum BucketType { get; }

        /// <summary>
        /// Returns the <see cref="ICluster"/> that this bucket belongs to
        /// </summary>
        ICluster Cluster { get; }

        /// <summary>
        /// Checks for the existance of a given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>True if the key exists.</returns>
        bool Exists(string key);

        /// <summary>
        /// Checks for the existance of a given key.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>True if the key exists.</returns>
        bool Exists(string key, TimeSpan timeout);

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <returns>A <see cref="Task{boolean}"/> object representing the asynchronous operation.</returns>
        Task<bool> ExistsAsync(string key);

        /// <summary>
        /// Checks for the existance of a given key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to check.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{boolean}"/> object representing the asynchronous operation.</returns>
        Task<bool> ExistsAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied asynchronously.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An <see cref="Task{ObserveResponse}"/> value indicating if the durability requirement were or were not met.</returns>
        Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied asynchronously.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="Task{ObserveResponse}"/> value indicating if the durability requirement were or were not met.</returns>
        Task<ObserveResponse> ObserveAsync(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An <see cref="ObserveResponse"/> value indicating if the durability requirement were or were not met.</returns>
        ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Performs 'observe' on a given key to ensure that it's durability requirements with respect to persistence and replication are satisfied.
        /// </summary>
        /// <param name="key">The key to 'observe'.</param>
        /// <param name="cas">The 'Check and Set' or CAS value for the key.</param>
        /// <param name="deletion">True if the operation performed is a 'remove' operation.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="ObserveResponse"/> value indicating if the durability requirement were or were not met.</returns>
        ObserveResponse Observe(string key, ulong cas, bool deletion, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Upsert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing JSON document into <see cref="IBucket"/> on a Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be updated or inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> UpsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously..
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Upserts a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> UpsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, uint expiration);


        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Upsert<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> UpsertAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items);


        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options);

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, TimeSpan timeout);

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize);

        /// <summary>
        /// Inserts or replaces a range of items into Couchbase Server.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="items">A <see cref="IDictionary{K, T}"/> of items to be stored in Couchbase.</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IDictionary{K, V}"/> of <see cref="IOperationResult"/> which for which each is the result of the individual operation.</returns>
        /// <remarks>An item is <see cref="KeyValuePair{K, V}"/> where K is a <see cref="string"/> and V is the <see cref="Type"/>of the value use wish to store.</remarks>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Upsert<T>(IDictionary<string, T> items, ParallelOptions options, int rangeSize, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, TimeSpan timeout);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a list of <see cref="IDocument{T}"/> into a bucket asynchronously.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents to upsert.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Task{IDocumentResult}"/> list.</returns>
        Task<IDocumentResult<T>[]> ReplaceAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Replace<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> ReplaceAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, uint expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Replace<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Replaces a document for a given key if it exists, otherwise fails as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> ReplaceAsync<T>(string key, T value, ulong cas, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, TimeSpan timeout);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a list of JSON documents asynchronously, each document failing if it already exists.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        Task<IDocumentResult<T>[]> InsertAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IDocumentResult<T> Insert<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a JSON document into the <see cref="IBucket"/>failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type T value of the document to be inserted.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> JSON document to add to the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> InsertAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value);

        /// <summary>
        /// Inserts or replaces an existing document into Couchbase Server as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="value"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, uint expiration);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, uint expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Insert<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Inserts a document into the database for a given key, failing if it exists as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value to be inserted.</typeparam>
        /// <param name="key">The unique key for indexing.</param>
        /// <param name="value">The value for the key.</param>
        /// <param name="expiration">The time-to-live (ttl) for the key.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> InsertAsync<T>(string key, T value, TimeSpan expiration, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document from the database.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing <see cref="IDocumentResult{T}"/> with information regarding the operation.</returns>
        IOperationResult Remove<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document from the database as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T of the object.</typeparam>
        /// <param name="document">The <see cref="IDocument{T}"/> to remove from the database.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync<T>(IDocument<T> document, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, TimeSpan timeout);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a list of <see cref="IDocument"/> from  the bucket asynchronously.
        /// </summary>
        /// <typeparam name="T">The type T of the document.</typeparam>
        /// <param name="documents">The documents.</param>
        /// <param name="replicateTo"></param>
        /// <param name="persistTo"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A list of <see cref="Task{IOperationResult}"/> objects representing the asynchronous operation.</returns>
        Task<IOperationResult[]> RemoveAsync<T>(List<IDocument<T>> documents, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, TimeSpan timeout);

        /// <summary>
        /// Asynchronously removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key);

        /// <summary>
        /// Asynchronously removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document for a given key from the database.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult Remove(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo);

        /// <summary>
        /// Removes a document for a given key from the database as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to remove from the database</param>
        /// <param name="cas">The CAS (Check and Set) value for optimistic concurrency.</param>
        /// <param name="replicateTo">The durability requirement for replication.</param>
        /// <param name="persistTo">The durability requirement for persistence.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> RemoveAsync(string key, ulong cas, ReplicateTo replicateTo, PersistTo persistTo, TimeSpan timeout);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult> Remove(IList<string> keys);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        IDictionary<string, IOperationResult> Remove(IList<string> keys, TimeSpan timeout);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, TimeSpan timeout);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize);

        /// <summary>
        /// Removes a range of documents for a given set of keys
        /// </summary>
        /// <param name="keys">The keys to remove</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        IDictionary<string, IOperationResult> Remove(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout);

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        IOperationResult Touch(string key, TimeSpan expiration);

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IOperationResult> TouchAsync(string key, TimeSpan expiration);

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IOperationResult> TouchAsync(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the key's value.</returns>
        IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the key's value.</returns>
        IOperationResult<T> GetAndTouch<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Retrieves a value by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndTouchAsync<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> with the key's document.</returns>
        IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> with the key's document.</returns>
        IDocumentResult<T> GetAndTouchDocument<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Retrieves a document by key and additionally updates the expiry with a new value as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="Task{IOperationResult}"/>object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetAndTouchDocumentAsync<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        IDocumentResult<T> GetDocument<T>(string id);

        /// <summary>
        /// Gets a document by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IDocumentResult{T}"/> object containing the document if it's found and any other operation specific info.</returns>
        IDocumentResult<T> GetDocument<T>(string id, TimeSpan timeout);

        /// <summary>
        /// Gets a document by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetDocumentAsync<T>(string id);

        /// <summary>
        /// Gets a document by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The documents primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetDocumentAsync<T>(string id, TimeSpan timeout);

        /// <summary>
        /// Gets a list of documents by their given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="ids">The documents primary keys.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> array representing the asynchronous operation results.</returns>
        Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids);

        /// <summary>
        /// Gets a list of documents by their given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="ids">The documents primary keys.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult}"/> array representing the asynchronous operation results.</returns>
        Task<IDocumentResult<T>[]> GetDocumentsAsync<T>(IEnumerable<string> ids, TimeSpan timeout);

        /// <summary>
        /// Gets a document using a replica by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="IDocumentResult{T}"/></returns>
        IDocumentResult<T> GetDocumentFromReplica<T>(string id);

        /// <summary>
        /// Gets a document using a replica by it's given id.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="IDocumentResult{T}"/></returns>
        IDocumentResult<T> GetDocumentFromReplica<T>(string id, TimeSpan timeout);

        /// <summary>
        /// Gets a document using a replica by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <returns>The <see cref="Task{IDocumentResult{T}}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id);

        /// <summary>
        /// Gets a document using a replica by it's given id as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="id">The document's primary key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IDocumentResult{T}}"/> object representing the asynchronous operation.</returns>
        Task<IDocumentResult<T>> GetDocumentFromReplicaAsync<T>(string id, TimeSpan timeout);

        /// <summary>
        /// Gets value for a given key
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="key">The key to use as a lookup.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Get<T>(string key);

        /// <summary>
        /// Gets value for a given key
        /// </summary>
        /// <typeparam name="T">The type T to convert the value to.</typeparam>
        /// <param name="key">The key to use as a lookup.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An object implementing the <see cref="IOperationResult{T}"/>interface.</returns>
        IOperationResult<T> Get<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAsync<T>(string key);

        /// <summary>
        /// Gets a Task that can be awaited on for a given Key and value as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The Type of the value object to be retrieved.</typeparam>
        /// <param name="key">The unique Key to use to lookup the value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAsync<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Returns a value for a
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns></returns>
        IOperationResult<T> GetFromReplica<T>(string key);

        /// <summary>
        /// Returns a value for a
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        IOperationResult<T> GetFromReplica<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Gets a value for key and checks it's replicas as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key);

        /// <summary>
        /// Gets a value for key and checks it's replicas as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetFromReplicaAsync<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, TimeSpan timeout);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, TimeSpan timeout);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        [Obsolete("Use the async overloads that take a list of keys for multi-operations.")]
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize);

        /// <summary>
        /// Gets a range of values for a given set of keys
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned</typeparam>
        /// <param name="keys">The keys to get</param>
        /// <param name="options">A <see cref="ParallelOptions"/> instance with the options for the given operation.</param>
        /// <param name="rangeSize">The size of each subrange</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="Dictionary{k, v}"/> of the keys sent and the <see cref="IOperationResult{T}"/> result.</returns>
        /// <remarks>Use the <see cref="ParallelOptions"/> parameter to control the level of parallelism to use and/or to associate a <see cref="CancellationToken"/> with the operation.</remarks>
        IDictionary<string, IOperationResult<T>> Get<T>(IList<string> keys, ParallelOptions options, int rangeSize, TimeSpan timeout);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        [Obsolete("NCBC-1146: GetWithLock has been renamed to GetAndLock.")]
        IOperationResult<T> GetWithLock<T>(string key, uint expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        IOperationResult<T> GetAndLock<T>(string key, uint expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        IOperationResult<T> GetAndLock<T>(string key, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        [Obsolete("NCBC-1146: GetWithLockAsync has been renamed to GetAndLockAsync.")]
        Task<IOperationResult<T>> GetWithLockAsync<T>(string key, uint expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndLockAsync<T>(string key, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        [Obsolete("NCBC-1146: GetWithLock has been renamed to GetAndLock.")]
        IOperationResult<T> GetWithLock<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult{T}"/> with the value.</returns>
        IOperationResult<T> GetAndLock<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        [Obsolete("NCBC-1146: GetWithLockAsync has been renamed to GetAndLockAsync.")]
        Task<IOperationResult<T>> GetWithLockAsync<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration);

        /// <summary>
        /// Gets a document and locks it for a specified time period as an asynchronous operation.
        /// </summary>
        /// <typeparam name="T">The <see cref="Type"/> of the values to be returned.</typeparam>
        /// <param name="key">The key of the document to retrieve.</param>
        /// <param name="expiration">The seconds until the document is unlocked. The default is 15 seconds and the maximum supported by the server is 30 seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<T>> GetAndLockAsync<T>(string key, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetAndLock{T}"/>.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>An <see cref="IOperationResult"/> with the status.</returns>
        IOperationResult Unlock(string key, ulong cas);

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetAndLock{T}"/>.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status.</returns>
        IOperationResult Unlock(string key, ulong cas, TimeSpan timeout);

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetAndLockAsync{T}"/> as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> UnlockAsync(string key, ulong cas);

        /// <summary>
        /// Unlocks a key that was locked with <see cref="GetAndLockAsync{T}"/> as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key of the document to unlock.</param>
        /// <param name="cas">The 'check and set' value to use as a comparison</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult> UnlockAsync(string key, ulong cas, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        IOperationResult<ulong> Increment(string key);

        /// <summary>
        /// Increments the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        IOperationResult<ulong> Increment(string key, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created.
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>/// <returns></returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key);

        /// <summary>
        /// Increments the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created.
        /// and seeded with 1.
        /// </summary>
        /// <param name="key"></param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>/// <returns></returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, ulong initial);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration);

        /// <summary>
        /// Increments the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</returns>
        IOperationResult<ulong> Increment(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration);

        /// <summary>
        /// Increments the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the incremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> IncrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key);

        /// <summary>
        /// Decrements the value of a key by one. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key);

        /// <summary>
        /// Decrements the value of a key by one as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration);


        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter in seconds.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>Expirations over 30 * 24 * 60 * 60 (the amount of seconds in 30 days) are interpreted as a UNIX timestamp of the date at which the document expires.
        /// see <see href="http://docs.couchbase.com/couchbase-devguide-2.5/#about-document-expiration">documentation section about expiration</see>.
        /// </remarks>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, uint expiration, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration);

        /// <summary>
        /// Decrements the value of a key by the delta. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</returns>
        IOperationResult<ulong> Decrement(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration);

        /// <summary>
        /// Decrements the value of a key by the delta as an asynchronous operation. If the key doesn't exist, it will be created
        /// and seeded with the defaut initial value 1.
        /// </summary>
        /// <param name="key">The key to us for the counter.</param>
        /// <param name="delta">The number to increment the key by.</param>
        /// <param name="initial">The initial value to use. If the key doesn't exist, this value will returned.</param>
        /// <param name="expiration">The time-to-live (ttl) for the counter.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <remarks>If the key doesn't exist, the server will respond with the initial value. If not the decremented value will be returned.</remarks>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<ulong>> DecrementAsync(string key, ulong delta, ulong initial, TimeSpan expiration, TimeSpan timeout);

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<string> Append(string key, string value);

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<string> Append(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<string>> AppendAsync(string key, string value);

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<string>> AppendAsync(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<byte[]> Append(string key, byte[] value);

        /// <summary>
        /// Appends a value to a give key.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<byte[]> Append(string key, byte[] value, TimeSpan timeout);

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value);

        /// <summary>
        /// Appends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to append too.</param>
        /// <param name="value">The value to append to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<byte[]>> AppendAsync(string key, byte[] value, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<string>Prepend(string key, string value);

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<string> Prepend(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<string>> PrependAsync(string key, string value);

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<string>> PrependAsync(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<byte[]> Prepend(string key, byte[] value);

        /// <summary>
        /// Prepends a value to a give key.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IOperationResult"/> with the status of the operation.</returns>
        IOperationResult<byte[]> Prepend(string key, byte[] value, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value);

        /// <summary>
        /// Prepends a value to a give key as an asynchronous operation.
        /// </summary>
        /// <param name="key">The key to Prepend too.</param>
        /// <param name="value">The value to prepend to the key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The <see cref="Task{IOperationResult}"/> object representing the asynchronous operation.</returns>
        Task<IOperationResult<byte[]>> PrependAsync(string key, byte[] value, TimeSpan timeout);

        /// <summary>
        /// Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> used to generate the results.</param>
        /// <returns>An instance of an object that implements the <see cref="T:Couchbase.Views.IViewResult{T}"/> Type with the results of the query.</returns>
        /// <remarks>Use one of the IBucket.CreateQuery overloads to generate the query.</remarks>
        IViewResult<T> Query<T>(IViewQueryable query);

        /// <summary>
        /// Asynchronously Executes a View query and returns the result.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">The <see cref="IViewQuery"/> used to generate the results.</param>
        /// <returns>An awaitable <see cref="Task{T}"/> with the T a <see cref="IViewResult{T}"/> instance.</returns>
        /// <remarks>Note this implementation is experimental and subject to change in future release!</remarks>
        Task<IViewResult<T>> QueryAsync<T>(IViewQueryable query);

        /// <summary>
        /// Executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        IQueryResult<T> Query<T>(string query);

        /// <summary>
        /// Asynchronously executes a N1QL query against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="query">An ad-hoc N1QL query.</param>
        /// <returns>An awaitable <see cref="Task{T}"/> with the T a <see cref="IQueryResult{T}"/> instance.</returns>
        /// <remarks>Note this implementation is uncommitted/experimental and subject to change in future release!</remarks>
        Task<IQueryResult<T>> QueryAsync<T>(string query);

        /// <summary>
        /// Executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        IQueryResult<T> Query<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest);

        /// <summary>
        /// Asynchronously executes a N1QL statement or prepared statement via a <see cref="IQueryRequest"/> against the Couchbase Cluster.
        /// </summary>
        /// <typeparam name="T">The Type to deserialze the results to. The dynamic Type works well.</typeparam>
        /// <param name="queryRequest">An <see cref="IQueryRequest"/> object that contains a statement or a prepared statement and the appropriate properties.</param>
        /// <param name="cancellationToken">Token which can cancel the query.</param>
        /// <returns>An instance of an object that implements the <see cref="Couchbase.N1QL.IQueryResult{T}"/> interface; the results of the query.</returns>
        Task<IQueryResult<T>> QueryAsync<T>(IQueryRequest queryRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        IAnalyticsResult<T> Query<T>(IAnalyticsRequest analyticsRequest);

        /// <summary>
        /// Asynchronously executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest);

        /// <summary>
        /// Asynchronously executes an Analytics statemnt via a <see cref="IAnalyticsRequest"/> against the Couchbase cluster.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the results to.</typeparam>
        /// <param name="analyticsRequest">A <see cref="IAnalyticsRequest"/> that contains the statement to be executed.</param>
        /// <param name="cancellationToken">A cancellation token that can be used to stop the query being executed.</param>
        /// <returns>An instance of <see cref="IAnalyticsResult{T}"/> with the result of the query.</returns>
        Task<IAnalyticsResult<T>> QueryAsync<T>(IAnalyticsRequest analyticsRequest, CancellationToken cancellationToken);

        /// <summary>
        /// Creates an instance of an object that implements <see cref="IViewQuery"/>, which targets a given bucket, design document and a published view.
        /// </summary>
        /// <param name="designDoc"></param>
        /// <param name="view"></param>
        /// <returns>An <see cref="T:Couchbase.Views.IViewQuery"/> which can have more filters and options applied to it.</returns>
        IViewQuery CreateQuery(string designDoc, string view);

        /// <summary>
        /// Creates an instance of an object that implements <see cref="IViewQuery"/>, which targets a given bucket and design document.
        /// </summary>
        /// <param name="designdoc">The design document that the View belongs to.</param>
        /// <param name="view">The View to query.</param>
        /// <param name="development">True will execute on the development dataset.</param>
        /// <returns>An <see cref="T:Couchbase.Views.ViewQuery"/> which can have more filters and options applied to it.</returns>
        IViewQuery CreateQuery(string designdoc, string view, bool development);

        /// <summary>
        /// Creates a <see cref="IBucketManager"/> instance for managing buckets.
        /// </summary>
        /// <param name="username">The administrators username</param>
        /// <param name="password">The administrators username</param>
        /// <returns>A <see cref="IBucketManager"/> instance.</returns>
        IBucketManager CreateManager(string username, string password);

        /// <summary>
        /// Creates a <see cref="IBucketManager"/> instance for managing buckets using the <see cref="IClusterCredentials"/> for authentication.
        /// </summary>
        /// <returns>A <see cref="IBucketManager"/> instance.</returns>
        IBucketManager CreateManager();

        /// <summary>
        /// Returns true if bucket is using SSL encryption between the client and the server.
        /// </summary>
        /// <remarks>If the server is not available (<see cref="ServerUnavailableException"/>), will default to false.</remarks>
        bool IsSecure { get; }

        /// <summary>
        /// Gets a value indicating whether enhanced durability is enabled.
        /// </summary>
        /// <value>
        /// <c>true</c> if the server supports enhanced durability and it is enabled; otherwise, <c>false</c>.
        /// </value>
        bool SupportsEnhancedDurability { get; }

        /// <summary>
        /// Gets a value indicating whether the cluster supports an error map that can
        /// be used to return custom error information.
        /// </summary>
        /// <value>
        /// <c>true</c> if the cluster supports KV error map; otherwise, <c>false</c>.
        /// </value>
        bool SupportsKvErrorMap { get; }

        /// <summary>
        /// Gets the configuration for the bucket
        /// </summary>
        /// <value>
        /// The configuration.
        /// </value>
        BucketConfiguration Configuration { get; }

        IMutateInBuilder<TDocument> MutateIn<TDocument>(string key);

        IMutateInBuilder<TDocument> MutateIn<TDocument>(string key, TimeSpan timeout);

        ILookupInBuilder<TDocument> LookupIn<TDocument>(string key);

        ILookupInBuilder<TDocument> LookupIn<TDocument>(string key, TimeSpan timeout);

        ISearchQueryResult Query(SearchQuery searchQuery);

        Task<ISearchQueryResult> QueryAsync(SearchQuery searchQuery);

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        IResult<TContent> MapGet<TContent>(string key, string mapkey);

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        IResult<TContent> MapGet<TContent>(string key, string mapkey, TimeSpan timeout);

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult MapRemove(string key, string mapkey);

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult MapRemove(string key, string mapkey, TimeSpan timeout);

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> MapSize(string key);

        /// <summary>
        /// Gets the size of a hashmap within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> MapSize(string key, TimeSpan timeout);

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult MapAdd(string key, string mapkey, string value, bool createMap);

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult MapAdd(string key, string mapkey, string value, bool createMap, TimeSpan timeout);

        /// <summary>
        /// Returns the value at a given index assuming a JSON array.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        IResult<TContent> ListGet<TContent>(string key, int index);

        /// <summary>
        /// Returns the value at a given index assuming a JSON array.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        IResult<TContent> ListGet<TContent>(string key, int index, TimeSpan timeout);

        /// <summary>
        /// Appends a value to the back of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListAppend(string key, object value, bool createList);

        /// <summary>
        /// Appends a value to the back of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListAppend(string key, object value, bool createList, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListPrepend(string key, object value, bool createList);

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListPrepend(string key, object value, bool createList, TimeSpan timeout);

        /// <summary>
        /// Removes a value at a given index with a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListRemove(string key, int index);

        /// <summary>
        /// Removes a value at a given index with a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListRemove(string key, int index, TimeSpan timeout);

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListSet(string key, int index, string value);

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult ListSet(string key, int index, string value, TimeSpan timeout);

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> ListSize(string key);

        /// <summary>
        /// Gets the size of an array within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> ListSize(string key, TimeSpan timeout);

        /// <summary>
        /// Adds a value to a set within a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult SetAdd(string key, string value, bool createSet);

        /// <summary>
        /// Adds a value to a set within a JSON array within a document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult SetAdd(string key, string value, bool createSet, TimeSpan timeout);

        /// <summary>
        /// Checks if a set contains a given value within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult{boolean}"/> with the operation result.</returns>
        IResult<bool> SetContains(string key, string value);

        /// <summary>
        /// Checks if a set contains a given value within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{boolean}"/> with the operation result.</returns>
        IResult<bool> SetContains(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> SetSize(string key);

        /// <summary>
        /// Gets the size of a set within a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        IResult<int> SetSize(string key, TimeSpan timeout);

        /// <summary>
        /// Removes a value from a set withing a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult SetRemove<T>(string key, T value);

        /// <summary>
        /// Removes a value from a set withing a JSON document.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        IResult SetRemove<T>(string key, T value, TimeSpan timeout);

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        IResult QueuePush<T>(string key, T value, bool createQueue);

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        IResult QueuePush<T>(string key, T value, bool createQueue, TimeSpan timeout);

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        IResult<T> QueuePop<T>(string key);

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        IResult<T> QueuePop<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <returns>An <see cref="IResult{T}"/> with the operation result.</returns>
        IResult<int> QueueSize(string key);

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>An <see cref="IResult{T}"/> with the operation result.</returns>
        IResult<int> QueueSize(string key, TimeSpan timeout);

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey);

        /// <summary>
        /// Gets the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        Task<IResult<TContent>> MapGetAsync<TContent>(string key, string mapkey, TimeSpan timeout);

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> MapRemoveAsync(string key, string mapkey);

        /// <summary>
        /// Removes the value for a given key from a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> MapRemoveAsync(string key, string mapkey, TimeSpan timeout);

        /// <summary>
        /// Gets the size of a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> MapSizeAsync(string key);

        /// <summary>
        /// Gets the size of a hashmap within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> MapSizeAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap);

        /// <summary>
        /// Adds a key/value pair to a JSON hashmap document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="mapkey">The mapkey.</param>
        /// <param name="value">The value.</param>
        /// <param name="createMap">If set to <c>true</c> create document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> MapAddAsync(string key, string mapkey, string value, bool createMap, TimeSpan timeout);

        /// <summary>
        /// Returns the value at a given index assuming a JSON array asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index);

        /// <summary>
        /// Returns the value at a given index assuming a JSON array asynchronously.
        /// </summary>
        /// <typeparam name="TContent">The type of the content.</typeparam>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>The value as <see cref="IResult{TContent}"/></returns>
        Task<IResult<TContent>> ListGetAsync<TContent>(string key, int index, TimeSpan timeout);

        /// <summary>
        /// Appends a value to the back of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListAppendAsync(string key, object value, bool createList);

        /// <summary>
        /// Appends a value to the back of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListAppendAsync(string key, object value, bool createList, TimeSpan timeout);

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListPrependAsync(string key, object value, bool createList);

        /// <summary>
        /// Prepends a value to the front of a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createList">If set to <c>true</c> [create list].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListPrependAsync(string key, object value, bool createList, TimeSpan timeout);

        /// <summary>
        /// Removes a value at a given index with a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListRemoveAsync(string key, int index);

        /// <summary>
        /// Removes a value at a given index with a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListRemoveAsync(string key, int index, TimeSpan timeout);

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListSetAsync(string key, int index, string value);

        /// <summary>
        /// Adds a value to an array within a JSON document at a given index asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="index">The index.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> ListSetAsync(string key, int index, string value, TimeSpan timeout);

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> ListSizeAsync(string key);

        /// <summary>
        /// Gets the size of an array within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> ListSizeAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Adds a value to a set within a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> SetAddAsync(string key, string value, bool createSet);

        /// <summary>
        /// Adds a value to a set within a JSON array within a document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="createSet">If set to <c>true</c> [create set].</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> SetAddAsync(string key, string value, bool createSet, TimeSpan timeout);

        /// <summary>
        /// Checks if a set contains a given value within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult{boolean}"/> with the operation result.</returns>
        Task<IResult<bool>> SetContainsAsync(string key, string value);

        /// <summary>
        /// Checks if a set contains a given value within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{boolean}"/> with the operation result.</returns>
        Task<IResult<bool>> SetContainsAsync(string key, string value, TimeSpan timeout);

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> SetSizeAsync(string key);

        /// <summary>
        /// Gets the size of a set within a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{integer}"/> with the operation result.</returns>
        Task<IResult<int>> SetSizeAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Removes a value from a set withing a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> SetRemoveAsync<T>(string key, T value);

        /// <summary>
        /// Removes a value from a set withing a JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key.</param>
        /// <param name="value">The value.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult"/> with the operation result.</returns>
        Task<IResult> SetRemoveAsync<T>(string key, T value, TimeSpan timeout);

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <returns></returns>
        Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue);

        /// <summary>
        /// Adds a value to the end of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The Type of the value being added to the queue</typeparam>
        /// <param name="key">The key for the document.</param>
        /// <param name="value">The value that is to be added to the queue.</param>
        /// <param name="createQueue">If <c>true</c> then the document will be created if it doesn't exist</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns></returns>
        Task<IResult> QueuePushAsync<T>(string key, T value, bool createQueue, TimeSpan timeout);

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        Task<IResult<T>> QueuePopAsync<T>(string key);

        /// <summary>
        /// Removes a value from the front of a queue stored in a JSON document asynchronously.
        /// </summary>
        /// <typeparam name="T">The type of the value being retrieved.</typeparam>
        /// <param name="key">The key for the queue.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        /// <returns>A <see cref="IResult{T}"/> with the operation result.</returns>
        Task<IResult<T>> QueuePopAsync<T>(string key, TimeSpan timeout);

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        Task<IResult<int>> QueueSizeAsync(string key);

        /// <summary>
        /// Gets the cluster version using the configured bucket or cluster credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        ClusterVersion? GetClusterVersion();

        /// <summary>
        /// Gets the cluster version using the configured bucket or cluster credentials.
        /// </summary>
        /// <returns>The cluster version, or null if unavailable.</returns>
        Task<ClusterVersion?> GetClusterVersionAsync();

        /// <summary>
        /// Returns the number of items in the queue stored in the JSON document asynchronously.
        /// </summary>
        /// <param name="key">The key for the document.</param>
        /// <param name="timeout">The maximum time allowed for an operation to live before timing out.</param>
        Task<IResult<int>> QueueSizeAsync(string key, TimeSpan timeout);

        /// <summary>
        /// Updates the expiration a key without modifying or returning it's value.
        /// </summary>
        /// <param name="key">The key to "touch".</param>
        /// <param name="expiration">The expiration to extend.</param>
        /// <returns>An <see cref="IOperationResult"/> with no value.</returns>
        IOperationResult Touch(string key, TimeSpan expiration, TimeSpan timeout);

        #region Diagnostics API

        /// <summary>
        /// Pings the specified services.
        /// </summary>
        /// <param name="services">The services to ping.</param>
        /// <returns>
        /// A <see cref="IPingReport"/> with details of requested services.
        /// </returns>
        IPingReport Ping(params ServiceType[] services);

        /// <summary>
        /// Pings the specified services.
        /// </summary>
        /// <param name="reportId">The report identifier.</param>
        /// <param name="services">The services to ping.</param>
        /// <returns>
        /// A <see cref="IPingReport"/> with details of requested services.
        /// </returns>
        IPingReport Ping(string reportId, params ServiceType[] services);

        #endregion
    }
}

#region [ License information ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2014 Couchbase, Inc.
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
