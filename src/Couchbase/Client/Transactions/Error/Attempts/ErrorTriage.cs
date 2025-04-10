#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Couchbase.Core.Exceptions;
using Couchbase.Client.Transactions.Error.External;
using Microsoft.Extensions.Logging;
using static Couchbase.Client.Transactions.Error.ErrorBuilder;
using static Couchbase.Client.Transactions.Error.ErrorClass;
using static Couchbase.Client.Transactions.Error.External.TransactionOperationFailedException.FinalError;

namespace Couchbase.Client.Transactions.Error.Attempts
{
    internal class ErrorTriage
    {
        private readonly AttemptContext _ctx;
        private readonly ILogger? _logger;

        public ErrorTriage(AttemptContext ctx, ILoggerFactory? loggerFactory)
        {
            _ctx = ctx;
            _logger = loggerFactory?.CreateLogger(nameof(ErrorTriage));
        }

        public TransactionOperationFailedException AssertNotNull(TransactionOperationFailedException? toThrow, ErrorClass ec, Exception innerException) =>
            toThrow ?? CreateError(_ctx, ec,
                    new InvalidOperationException("Failed to generate proper exception wrapper", innerException))
                .Build();

        public TransactionOperationFailedException AssertNotNull(
            (ErrorClass ec, TransactionOperationFailedException? toThrow) triageResult, Exception innerException) =>
            AssertNotNull(triageResult.toThrow, triageResult.ec, innerException);

        private ErrorBuilder Error(ErrorClass ec, Exception err, bool? retry = null, bool? rollback = null, TransactionOperationFailedException.FinalError? raise = null)
        {
            var eb = CreateError(_ctx, ec, err);
            if (retry.HasValue && retry.Value)
            {
                eb.RetryTransaction();
            }

            if (rollback.HasValue && rollback.Value == false)
            {
                eb.DoNotRollbackAttempt();
            }

            if (raise.HasValue)
            {
                eb.RaiseException(raise.Value);
            }

            return eb;
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageGetErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#GetOptionalAsync
            // On error err of any of the above, classify as ErrorClass ec then:
            //   FAIL_DOC_NOT_FOUND -> return empty
            //   Else FAIL_HARD -> Error(ec, err, rollback=false)
            //   Else FAIL_TRANSIENT -> Error(ec, err, retry=true)
            //   Else -> raise Error(ec, cause=err)

            var ec = err.Classify();
            TransactionOperationFailedException? toThrow = ec switch
            {
                FailDocNotFound => null,
                FailHard => Error(ec, err, rollback: false).Build(),
                FailTransient => Error(ec, err, retry:true).Build(),
                _ => err is TransactionOperationFailedException alreadyClassified ? alreadyClassified : Error(ec, err).Build()
            };

            return (ec, toThrow);
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageCreateStagedRemoveOrReplaceError(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Creating-Staged-ReplaceAsync
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => Error(ec, new AttemptExpiredException(_ctx), raise: TransactionExpired),
                FailDocAlreadyExists => Error(ec, err, retry: true), // <-- CasMismatch
                FailDocNotFound => Error(ec, err, retry: true),
                FailCasMismatch => Error(ec, err, retry: true),
                FailTransient => Error(ec, err, retry: true),
                FailAmbiguous => Error(ec, err, retry: true),
                FailHard => Error(ec, err, rollback: false),
                 _ => Error(ec, err)
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageCreateStagedInsertErrors(Exception err, in bool expirationOvertimeMode)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Creating-Staged-Inserts-Protocol-20-version
            _logger?.LogDebug("{method} called for {err}, with expirationOvertimeMode={otMode}", nameof(TriageCreateStagedInsertErrors), err, expirationOvertimeMode);
            if (err is FeatureNotAvailableException)
            {
                // If err is FeatureNotFoundException, then this cluster does not support creating shadow
                // documents. (Unfortunately we cannot perform this check at the Transactions.create point,
                // as we may not have a cluster config available then).
                // Raise Error(ec=FAIL_OTHER, cause=err) to terminate the transaction.
                return (FailOther, Error(FailOther, err).Build());
            }

            if (expirationOvertimeMode)
            {
                return (FailExpiry,
                    Error(FailExpiry, new AttemptExpiredException(_ctx), rollback: false, raise: TransactionExpired)
                        .Build());
            }

            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => Error(ec, new AttemptExpiredException(_ctx), raise: TransactionExpired),
                FailAmbiguous => null, // retry after delay
                FailTransient => Error(ec, err, retry: true),
                FailHard => Error(ec, err, rollback: false),
                FailCasMismatch => null, // handles the same as FailDocAlreadyExists
                FailDocAlreadyExists => null, // special handling
                FailDocNotFound => Error(ec, err),
                _ => Error(ec, err)
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageDocExistsOnStagedInsertErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Creating-Staged-Inserts-Protocol-20-version
            var ec = err.Classify();
            bool defaultRetry = false;
            TransactionOperationFailedException.FinalError? finalError = null;
            if (err is TransactionOperationFailedException tofe)
            {
                defaultRetry = tofe.RetryTransaction;
                finalError = tofe.FinalErrorToRaise;
            }

            ErrorBuilder? toThrow = ec switch
            {
                FailDocNotFound => Error(ec, err, retry: true),
                FailPathNotFound => Error(ec, err, retry:true),
                FailTransient => Error(ec, err, retry: true),
                _ => Error(ec, err, defaultRetry)
            };

            if (finalError != null && toThrow != null)
            {
                toThrow.RaiseException(finalError.Value);
            }

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageSetAtrPendingErrors(Exception err, in bool expirationOvertimeMode)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Creating-Staged-Inserts-Protocol-20-version
            if (expirationOvertimeMode)
            {
                return (FailExpiry,
                    Error(FailExpiry, new AttemptExpiredException(_ctx), rollback: false, raise: TransactionExpired)
                        .Build());
            }

            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => Error(ec, new AttemptExpiredException(_ctx), raise: TransactionExpired),
                FailAtrFull => Error(ec, new ActiveTransactionRecordsFullException(_ctx)),
                FailAmbiguous => null, // retry from the top of section
                FailPathAlreadyExists => null, // treat as successful
                FailHard => Error(ec, err, rollback: false),
                FailTransient => Error(ec, err, retry:true),
                _ => Error(ec, err)
            };

            return (ec, toThrow?.Build());
        }

        internal void ThrowIfCommitWithPreviousErrors(IEnumerable<TransactionOperationFailedException> previousErrorOperations)
        {
            // We have potentially multiple ErrorWrappers, and will construct a new single ErrorWrapper err from them using this algo:
            // err.retryTransaction is true iff it’s true for ALL errors.
            // err.rollback is true iff it’s true for ALL errors.
            // err.cause = PreviousOperationFailed, with that exception taking and providing access to the causes of all the errors
            // Then, raise err
            var previousErrors = previousErrorOperations.ToList();
            var retryTransaction = previousErrors.All(ex => ex.RetryTransaction);
            var rollback = previousErrors.All(ex => ex.AutoRollbackAttempt);
            var builder = CreateError(_ctx, FailOther);
            if (retryTransaction)
            {
                builder.RetryTransaction();
            }

            if (!rollback)
            {
                builder.DoNotRollbackAttempt();
            }

            throw builder.Build();
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageSetAtrCompleteErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#SetATRComplete
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailHard => Error(ec, err, rollback: false, raise: TransactionOperationFailedException.FinalError.TransactionFailedPostCommit),
                // Setting the ATR to COMPLETED is purely a cleanup step, there’s no need to retry it until expiry.
                // Simply return success (leaving state at COMMITTED).
                _ => null,
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageUnstageRemoveErrors(Exception err, in bool expirationOvertimeMode)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Unstaging-Removes
            if (expirationOvertimeMode)
            {
                return (FailExpiry,
                    Error(FailExpiry, new AttemptExpiredException(_ctx), rollback: false, raise: TransactionFailedPostCommit)
                        .Build());
            }

            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailAmbiguous => null, // retry after opRetryDelay
                FailDocNotFound => Error(ec, err, rollback:false, raise: TransactionFailedPostCommit),
                _ => Error(ec, err, rollback: false, raise: TransactionFailedPostCommit)
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageUnstageInsertOrReplaceErrors(Exception err, in bool expirationOvertimeMode)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Unstaging-Inserts-and-Replaces-Protocol-20-version
            if (expirationOvertimeMode)
            {
                return (FailExpiry,
                    Error(FailExpiry, new AttemptExpiredException(_ctx), rollback: false, raise: TransactionFailedPostCommit)
                        .Build());
            }

            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailAmbiguous => null, // retry
                FailCasMismatch => Error(ec, err, rollback:false, raise:TransactionFailedPostCommit),
                FailDocNotFound => null, // retry
                FailDocAlreadyExists => Error(ec, err, rollback: false, raise: TransactionFailedPostCommit),
                _ => Error(ec, err, rollback: false, raise: TransactionFailedPostCommit)
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageSetAtrAbortedErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#SetATRAborted
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => null,
                FailPathNotFound => Error(ec, new ActiveTransactionRecordEntryNotFoundException(), rollback: false),
                FailDocNotFound => Error(ec, new ActiveTransactionRecordNotFoundException(), rollback: false),
                FailAtrFull => Error(ec, new ActiveTransactionRecordsFullException(_ctx, "ATR Full during SetAtrAborted."), rollback: false),
                FailHard => Error(ec, err, rollback: false),
                _ => null
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageSetAtrRolledBackErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#SetATRRolledBack
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => Error(ec, err, rollback: false, raise: TransactionExpired),
                FailPathNotFound => null,
                FailDocNotFound => Error(ec, new ActiveTransactionRecordNotFoundException(), rollback: false),
                FailAtrFull => Error(ec, new ActiveTransactionRecordsFullException(_ctx, "ATR Full during SetAtrRolledBack."), rollback: false),
                FailHard => Error(ec, err, rollback: false),
                _ => null
            };

            return (ec, toThrow?.Build());
        }

        public (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageRollbackStagedInsertErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#RollbackAsync-Staged-InsertAsync
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => null,
                FailPathNotFound => null,
                FailDocNotFound => null,
                FailCasMismatch => Error(ec, err, rollback: false),
                FailHard => Error(ec, err, rollback: false),
                _ => null
            };

            return (ec, toThrow?.Build());
        }

        internal (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageRollbackStagedRemoveOrReplaceErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A?view#RollbackAsync-Staged-ReplaceAsync-or-RemoveAsync
            var ec = err.Classify();
            ErrorBuilder? toThrow = ec switch
            {
                FailExpiry => null,
                FailPathNotFound => null,
                FailDocNotFound => Error(ec, err, rollback: false),
                FailCasMismatch => Error(ec, err, rollback: false),
                FailHard => Error(ec, err, rollback: false),
                _ => null
            };

            return (ec, toThrow?.Build());
        }

        internal (ErrorClass ec, TransactionOperationFailedException? toThrow) TriageAtrLookupInMavErrors(Exception err)
        {
            // https://hackmd.io/Eaf20XhtRhi8aGEn_xIH8A#Get-a-Document-With-MAV-Logic
            // after "Do a Sub-Document lookup of the transaction's ATR entry"
            var ec = err.Classify();
            TransactionOperationFailedException? toThrow = ec switch
            {
                FailPathNotFound => throw new ActiveTransactionRecordEntryNotFoundException(),
                FailDocNotFound => Error(ec, new ActiveTransactionRecordNotFoundException()).Build(),
                _ => err is TransactionOperationFailedException alreadyClassified ? alreadyClassified : Error(ec, err).Build()
            };

            return (ec, toThrow);
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
