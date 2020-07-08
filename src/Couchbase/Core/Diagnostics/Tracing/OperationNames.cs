using System;
using System.Collections.Generic;
using System.Text;

namespace Couchbase.Core.Diagnostics.Tracing
{
    /// <summary>
    /// A collection of operation names for tracing, to keep in-line with the other SDKs.
    /// </summary>
    public static class OperationNames
    {
        // Convention:  Constant names start with the classname of the operation.
        //              If the value is very different, that is included in the constant name, too.
        public const string AddInsert = "insert";
        public const string Append = "append";
        public const string Decrement = "decrement";
        public const string DeleteRemove = "remove";
        public const string Get = "get";
        public const string GetAndLock = "get_and_lock";
        public const string GetMetaExists = "exists";
        public const string GetAndTouch = "get_and_touch";
        public const string Increment = "increment";
        public const string Prepend = "prepend";
        public const string Replace = "replace";
        public const string ReplicaRead = "replica_read";
        public const string SetUpsert = "upsert";
        public const string MultiLookupSubdocGet = "subdoc_get";
        public const string MultiMutationSubdocMutate = "subdoc_mutate";
        public const string Touch = "touch";
        public const string Unlock = "unlock";

        public const string ViewQuery = "view";
        public const string N1qlQuery = "query";
        public const string Hello = "hello";
        public const string GetManifest = "get_manifest";
        public const string GetClusterMap = "get_cluster_map";
        public const string GetCid = "get_cid";
        public const string GetAnyReplica = "get_any_replica";
        public const string GetAllReplicas = "get_all_replicas";
        public const string GetErrorMap = "get_error_map";
        public const string SelectBucket = "select_bucket";

        public const string AuthenticateScramSha = "authn_scramsha";
        public const string AuthenticatePlain = "authn_plain";
        public const string SaslStart = "sasl_start";
        public const string SaslStep = "sasl_step";
    }
}
