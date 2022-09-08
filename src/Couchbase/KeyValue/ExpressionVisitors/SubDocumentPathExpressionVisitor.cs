using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Couchbase.Core.IO.Serializers;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue.ExpressionVisitors
{
    /// <summary>
    /// Parses an expression tree which navigates a document to return the path to the sub document
    /// being referenced.
    /// </summary>
    internal sealed class SubDocumentPathExpressionVisitor : ExpressionVisitor
    {
        #region Static Fields

        private static readonly Type[] IntegralTypes =
        {
            typeof (int), typeof (uint), typeof (long), typeof (ulong),
            typeof (byte), typeof (sbyte), typeof (short), typeof (ushort)
        };

        private static readonly bool[] EscapeCharacters = new bool[128];

        static SubDocumentPathExpressionVisitor()
        {
            var chars = new[] {'\n', '\r', '\t', '\\', '\f', '\b', '\"', '\'', '`'}
                .Concat(Enumerable.Range(0, 31).Select(p => (char)p));

            foreach (var ch in chars)
            {
                EscapeCharacters[ch] = true;
            }
        }

        #endregion

        #region Fields/Properties

        private enum SpecialExpressionType
        {
            None,
            ArrayIndex,
            DictionaryKey
        }

        private readonly IExtendedTypeSerializer _serializer;
        private readonly StringBuilder _path = new();

        private ParameterExpression? _parameter;
        private SpecialExpressionType _inSpecialExpression;
        private bool _parameterEncountered;

        /// <summary>
        /// After visiting an expression tree, contains the path to the sub document.
        /// </summary>
        public string Path => _path.ToString();

        #endregion

        #region Constructor/Static Entry Point

        public static string GetPath<TDocument, TContent>(ITypeSerializerProvider? typeSerializerProvider, Expression<Func<TDocument, TContent>> path)
        {
            if (typeSerializerProvider?.Serializer is not IExtendedTypeSerializer extendedTypeSerializer)
            {
                ThrowHelper.ThrowNotSupportedException("Serializer must be IExtendedTypeSerializer to support subdocument paths.");
                return null!; // Unreachable but required to compile
            }

            return GetPath(extendedTypeSerializer, path);
        }

        public static string GetPath<TDocument, TContent>(IExtendedTypeSerializer serializer, Expression<Func<TDocument, TContent>> path)
        {
            // ReSharper disable ConditionIsAlwaysTrueOrFalse
            if (serializer == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(serializer));
            }
            if (path == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(path));
            }
            // ReSharper restore ConditionIsAlwaysTrueOrFalse

            var visitor = VisitorCache.Acquire(serializer);
            try
            {
                visitor.Visit(path);

                return visitor.Path;
            }
            finally
            {
                VisitorCache.Release(visitor);
            }
        }

        /// <summary>
        /// Creates a new SubDocumentPathExpressionVisitor.
        /// </summary>
        /// <param name="serializer"><see cref="IExtendedTypeSerializer"/> used for member name resolution.</param>
        private SubDocumentPathExpressionVisitor(IExtendedTypeSerializer serializer)
        {
            _serializer = serializer;
        }

        #endregion

        #region Visitors

        // We override this so we can apply the nullability attribute, which is missing in older frameworks
        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node) => base.Visit(node);

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            if (_parameter != null)
            {
                ThrowHelper.ThrowNotSupportedException("Only one lambda expression is allowed in a subdocument path.");
            }

            _parameter = node.Parameters[0];

            // Simplify the body before processing.  This makes any external variable references constants, and also
            // reduces any expressions that can be simplified.
            var body = LambdaSimplifyingExpressionVisitor.Simplify(node.Body);

            body = Visit(body);

            if (!_parameterEncountered)
            {
                ThrowHelper.ThrowNotSupportedException("The first statement in a subdocument path must be a reference to the lambda parameter.");
            }

            return node.Update(body, node.Parameters)!;
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            EnsureNotInSpecialExpression();

            if (node != _parameter)
            {
                // only allow references to our lambda expression's parameter

                ThrowHelper.ThrowInvalidOperationException("Incorrect lambda parameter encountered parsing the subdocument path.");
            }

            _parameterEncountered = true;

            return node;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (_inSpecialExpression == SpecialExpressionType.ArrayIndex)
            {
                var isIntegralType = false;
                for (var i = 0; i < IntegralTypes.Length; i++)
                {
                    if (IntegralTypes[i] == node.Type)
                    {
                        isIntegralType = true;
                        break;
                    }
                }

                if (!isIntegralType)
                {
                    ThrowHelper.ThrowNotSupportedException("Non-integral array indices are not supported in subdocument paths.");
                }

                _path.Append(node.Value);
            }
            else if (_inSpecialExpression == SpecialExpressionType.DictionaryKey)
            {
                WriteEscapedString(node.Value?.ToString() ?? "");
            }
            else
            {
                ThrowHelper.ThrowNotSupportedException("Constants are only supported in array and dictionary indices in subdocument paths.");
            }

            return node;
        }

        protected override Expression VisitMember(MemberExpression node)
        {
            EnsureNotInSpecialExpression();

            var expression = Visit(node.Expression);
            Debug.Assert(expression != null);

            if (expression!.Type.GetTypeInfo().IsGenericType && expression.Type.GetGenericTypeDefinition() == typeof(Nullable<>)
                && (node.Member.Name == "Value"))
            {
                // Don't include Nullable<T>.Value calls in output path

                return node.Update(expression);
            }

            PrependDotSmart(expression);
            WriteEscapedString(GetMemberName(node.Member));

            return node.Update(expression);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            EnsureNotInSpecialExpression();

            if ((node.Method.DeclaringType == null) || (node.Object == null) ||
                !node.Method.IsSpecialName || !node.Method.Name.StartsWith("get_"))
            {
                ThrowHelper.ThrowNotSupportedException("Method calls are not supported in subdocument paths.");
            }

            var property = node.Method.DeclaringType.GetProperties()
                .FirstOrDefault(p => p.GetMethod == node.Method);
            if (property == null)
            {
                ThrowHelper.ThrowNotSupportedException("Method calls are not supported in subdocument paths.");
            }

            var obj = Visit(node.Object);

            if (node.Arguments.Count > 0)
            {
                if (node.Arguments.Count > 1)
                {
                    ThrowHelper.ThrowNotSupportedException(
                        "Array/dictionary indices with more than one dimension are not supported in subdocument paths.");
                }

                // Ensure that indexed properties are only used if they are the default indexed property for the object

                if (!node.Method.DeclaringType.GetDefaultMembers().Contains(property))
                {
                    ThrowHelper.ThrowNotSupportedException(
                        "Only default indexed properties are not supported in subdocument paths.");
                }

                Expression argument;
                if (node.Arguments[0].Type == typeof (string))
                {
                    // This is a dictionary with a key string, so treat as property accessor
                    _inSpecialExpression = SpecialExpressionType.DictionaryKey;

                    PrependDotSmart(obj);
                    argument = Visit(node.Arguments[0]);
                }
                else
                {
                    _inSpecialExpression = SpecialExpressionType.ArrayIndex;

                    _path.Append('[');
                    argument = Visit(node.Arguments[0]);
                    _path.Append(']');
                }

                _inSpecialExpression = SpecialExpressionType.None;

                return node.Update(obj, new[] {argument});
            }
            else
            {
                PrependDotSmart(obj);
                WriteEscapedString(GetMemberName(property));

                return node.Update(obj, null);
            }
        }

        private Expression VisitArrayIndex(BinaryExpression node)
        {
            EnsureNotInSpecialExpression();

            var left = Visit(node.Left);

            _inSpecialExpression = SpecialExpressionType.ArrayIndex;
            _path.Append('[');

            var right = Visit(node.Right);

            _path.Append(']');
            _inSpecialExpression = SpecialExpressionType.None;

            return node.Update(left, node.Conversion, right);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            if (node.NodeType == ExpressionType.ArrayIndex)
            {
                return VisitArrayIndex(node);
            }
            else
            {
                throw CreateExpressionNotSupportedException(node);
            }
        }

        protected override Expression VisitBlock(BlockExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitDefault(DefaultExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitExtension(Expression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitIndex(IndexExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitListInit(ListInitExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitNew(NewExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitNewArray(NewArrayExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitMemberInit(MemberInitExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        protected override Expression VisitUnary(UnaryExpression node)
        {
            throw CreateExpressionNotSupportedException(node);
        }

        #endregion

        #region Helpers

        private string GetMemberName(MemberInfo member)
        {
            return _serializer.GetMemberName(member) ?? member.Name;
        }

        private void PrependDotSmart(Expression precedingExpression)
        {
            if (precedingExpression.NodeType != ExpressionType.Parameter)
            {
                // Only include dot prefix on member expressions after the first one

                _path.Append('.');
            }
        }

        /// <summary>
        /// Escapes a string using the N1QL variant of JSON escaping rules, and writes it to the path.
        /// </summary>
        /// <param name="str">String to escape and write.</param>
        private void WriteEscapedString(string str)
        {
            // For efficiency, make sure we have enough room for the delimiters, the string, and at least one escaped character
            _path.EnsureCapacity(_path.Length + str.Length + 3);

            _path.Append('`');

            // Convert to a span on the stack with JIT-known length as a performance optimization
            var escapeCharacters = EscapeCharacters.AsSpan(0, 128);

            // ReSharper disable once ForCanBeConvertedToForeach
            for (var i = 0; i < str.Length; i++)
            {
                var ch = str[i];

                if (ch < escapeCharacters.Length && !escapeCharacters[ch])
                {
                    _path.Append(ch);
                }
                else
                {
                    switch (ch)
                    {
                        case '`':
                            _path.Append("``");
                            break;
                        case '\t':
                            _path.Append(@"\t");
                            break;
                        case '\n':
                            _path.Append(@"\n");
                            break;
                        case '\r':
                            _path.Append(@"\r");
                            break;
                        case '\f':
                            _path.Append(@"\f");
                            break;
                        case '\b':
                            _path.Append(@"\b");
                            break;
                        case '\\':
                            _path.Append(@"\\");
                            break;
                        case '\"':
                            _path.Append(@"\""");
                            break;
                        case '\'':
                            _path.Append(@"\'");
                            break;
                        default:
                            _path.Append(@"\u0000");

                            var hexString = ((ushort) ch).ToString("x", System.Globalization.CultureInfo.InvariantCulture);
                            for (var j = 0; j < hexString.Length; j++)
                            {
                                _path[_path.Length - (hexString.Length - j)] = hexString[j];
                            }

                            break;
                    }
                }
            }

            _path.Append('`');
        }

        /// <summary>
        /// For unit testing of WriteEscapedString only.
        /// </summary>
        internal static string GetEscapedString(string str)
        {
            var visitor = new SubDocumentPathExpressionVisitor(DefaultSerializer.Instance);

            visitor.WriteEscapedString(str);

            return visitor.Path;
        }

        private void EnsureNotInSpecialExpression()
        {
            if (_inSpecialExpression == SpecialExpressionType.ArrayIndex)
            {
                ThrowHelper.ThrowNotSupportedException("Non-constant expressions are not supported as array indices in subdocument paths.");
            }
            else if (_inSpecialExpression == SpecialExpressionType.DictionaryKey)
            {
                ThrowHelper.ThrowNotSupportedException("Non-constant expressions are not supported as dictionary indices in subdocument paths.");
            }
        }

        private static NotSupportedException CreateExpressionNotSupportedException(Expression node) =>
            new($"{node.NodeType} expressions are not supported in subdocument paths.");

        #endregion

        #region Cache

        internal static class VisitorCache
        {
            // Stores a separate cached instance for each thread. At most one will be retained per thread,
            // and if the StringBuilder is too large it will be discarded. The cache is also only reused
            // if the same serializer instance is supplied. This is based on the theory that the IExtendedTypeSerializer
            // will be constant, which should be the case in a single cluster environment. In other cases
            // the caching won't be as effective but still has very little cost compared to a new visitor on
            // every invocation.
            [ThreadStatic]
            private static SubDocumentPathExpressionVisitor? t_cachedVisitor;

            public static SubDocumentPathExpressionVisitor Acquire(IExtendedTypeSerializer serializer)
            {
                var visitor = t_cachedVisitor;
                if (visitor is not null && ReferenceEquals(visitor._serializer, serializer))
                {
                    t_cachedVisitor = null;

                    // Ready for reuse
                    visitor._path.Clear();
                    visitor._parameter = null;
                    visitor._parameterEncountered = false;
                    visitor._inSpecialExpression = SpecialExpressionType.None;

                    return visitor;
                }

                return new SubDocumentPathExpressionVisitor(serializer);
            }

            public static void Release(SubDocumentPathExpressionVisitor visitor)
            {
                // Throw away visitors with long StringBuilder instances to keep memory utilization low.
                // Large paths are unlikely.
                if (visitor._path.Length <= 256)
                {
                    t_cachedVisitor = visitor;
                }
            }
        }

        #endregion
    }
}

#region [ License information          ]

/* ************************************************************
 *
 *    @author Couchbase <info@couchbase.com>
 *    @copyright 2017 Couchbase, Inc.
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
