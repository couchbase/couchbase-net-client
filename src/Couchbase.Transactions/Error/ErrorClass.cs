using System;
using Couchbase.Core.Exceptions;
using Couchbase.Core.Exceptions.KeyValue;
using Couchbase.Transactions.Error.Attempts;
using Couchbase.Transactions.Error.Internal;
#pragma warning disable CS1591

namespace Couchbase.Transactions.Error
{
    // TODO: this should be made internal
    public enum ErrorClass
    {
        Undefined = 0,
        FailTransient = 1,
        FailHard = 2,
        FailOther = 3,
        FailAmbiguous = 4,
        FailDocAlreadyExists = 5,
        FailDocNotFound = 6,
        FailPathAlreadyExists = 7,
        FailPathNotFound = 8,
        FailCasMismatch = 9,
        FailExpiry = 10,
        FailWriteWriteConflict = 11,
        FailAtrFull = 12,
        TransactionOperationFailed = 255
    }

    public static class ErrorClassExtensions
    {
        public static ErrorClass Classify(this Exception ex)
        {
            if (ex is IClassifiedTransactionError classifiedError)
            {
                return classifiedError.CausingErrorClass;
            }

            if (ex is TransactionFailedException)
            {
                return ErrorClass.TransactionOperationFailed;
            }

            if (ex is DocumentAlreadyInTransactionException)
            {
                return ErrorClass.FailWriteWriteConflict;
            }

            if (ex is DocumentNotFoundException)
            {
                return ErrorClass.FailDocNotFound;
            }

            if (ex is DocumentExistsException)
            {
                return ErrorClass.FailDocAlreadyExists;
            }

            if (ex is SubDocException pathInvalid)
            {
                switch (pathInvalid.SubDocumentStatus)
                {
                    case Core.IO.Operations.ResponseStatus.SubDocPathExists:
                        return ErrorClass.FailPathAlreadyExists;
                    case Core.IO.Operations.ResponseStatus.SubdocMultiPathFailureDeleted:
                    case Core.IO.Operations.ResponseStatus.SubDocPathNotFound:
                        return ErrorClass.FailPathNotFound;
                    default:
                        if (pathInvalid is PathInvalidException)
                        {
                            return ErrorClass.FailOther;
                        }

                        break;
                }
            }

            if (ex is PathExistsException)
            {
                return ErrorClass.FailPathAlreadyExists;
            }

            if (ex is PathNotFoundException || ex is PathInvalidException)
            {
                return ErrorClass.FailPathNotFound;
            }

            if (ex is CasMismatchException)
            {
                return ErrorClass.FailCasMismatch;
            }

            if (ex.IsFailTransient())
            {
                return ErrorClass.FailTransient;
            }

            if (ex.IsFailAmbiguous())
            {
                return ErrorClass.FailAmbiguous;
            }

            // ErrorClass.FailHard, from the java code, is handled by IClassifiedTransactionError

            if (ex is AttemptExpiredException)
            {
                return ErrorClass.FailExpiry;
            }

            if (ex is ValueToolargeException)
            {
                return ErrorClass.FailAtrFull;
            }

            if (ex is CouchbaseException cbe)
            {
                if (cbe.Context?.Message?.Contains("XATTR_EINVAL") == true)
                {
                    return ErrorClass.FailHard;
                }
            }

            return ErrorClass.FailOther;
        }

        internal static bool IsFailTransient(this Exception ex)
        {
            switch (ex)
            {
                case CasMismatchException _:

                // TXNJ-156: With BestEffortRetryStrategy, many errors such as TempFails will now surface as
                // timeouts instead.  This will include AmbiguousTimeoutException - we should already be able to
                // handle ambiguity, as with DurabilityAmbiguousException
                case UnambiguousTimeoutException _:

                // These only included because several tests explicitly throw them as an error-injection.  Those
                // should be changed to return a more correct TimeoutException.
                case TemporaryFailureException _:
                case DurableWriteInProgressException _:
                    return true;
                default:
                    return false;
            }
        }

        internal static bool IsFailAmbiguous(this Exception ex)
        {
            switch (ex)
            {
                case DurabilityAmbiguousException _:
                case AmbiguousTimeoutException _:
                case RequestCanceledException _:
                    return true;

                default:
                    return false;
            }
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2021 Couchbase, Inc.
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
