#nullable enable
using System;
using Couchbase.Integrated.Transactions.Error.External;

namespace Couchbase.Integrated.Transactions.Error
{
    internal class ErrorBuilder
    {
        public const ErrorBuilder? None = null;
        private readonly AttemptContext? _ctx;
        private readonly ErrorClass _causingErrorClass;
        private TransactionOperationFailedException.FinalErrorToRaise _toRaise = TransactionOperationFailedException.FinalErrorToRaise.TransactionFailed;
        private bool _rollbackAttempt = true;
        private bool _retryTransaction = false;
        private bool _updateStateBits = true;
        private Exception _cause = new Exception("generic exception cause");

        private ErrorBuilder(AttemptContext? ctx, ErrorClass causingErrorClass)
        {
            _ctx = ctx;
            _causingErrorClass = causingErrorClass;
        }

        public static ErrorBuilder CreateError(AttemptContext? ctx, ErrorClass causingErrorClass, Exception? causingException = null)
        {
            var builder = new ErrorBuilder(ctx, causingErrorClass);
            if (causingException != null)
            {
                builder.Cause(causingException);
            }

            return builder;
        }
        public static TransactionOperationFailedException WrapError(AttemptContext? ctx, Exception err) => CreateError(ctx, err.Classify(), err).Build();

        public ErrorBuilder RaiseException(TransactionOperationFailedException.FinalErrorToRaise toRaise)
        {
            _toRaise = toRaise;
            return this;
        }

        public ErrorBuilder DoNotRollbackAttempt()
        {
            _rollbackAttempt = false;
            return this;
        }

        public ErrorBuilder DoNotUpdateStateBits()
        {
            _updateStateBits = false;
            return this;
        }

        public ErrorBuilder RetryTransaction()
        {
            _retryTransaction = true;
            return this;
        }

        public ErrorBuilder Cause(Exception cause)
        {
            _cause = cause ?? new Exception("UnknownCause");
            return this;
        }

        public TransactionOperationFailedException Build()
        {
            var err = new TransactionOperationFailedException(
                _causingErrorClass,
                _rollbackAttempt,
                _retryTransaction,
                _cause,
                _toRaise,
                _updateStateBits);

            if (_updateStateBits)
            {
                _ctx?.UpdateStateBits(err);
            }

            return err;
        }
    }
}


/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2024 Couchbase, Inc.
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





