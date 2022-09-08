using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using Couchbase.Utils;

#nullable enable

namespace Couchbase.KeyValue.ExpressionVisitors
{
    /// <summary>
    /// Simplifies an expression tree by evaluating any branches of the tree that do not include
    /// lambda parameter references.  This will remove references to variables external to the lambda
    /// by converting them to constants, perform arithmetic, and execute method calls as needed.
    /// For example, a call to "str.ToUpper()" where string is an external variable would be simplified
    /// to a <see cref="ConstantExpression"/> containing the uppercase version of str.
    /// </summary>
    internal sealed class LambdaSimplifyingExpressionVisitor : ExpressionVisitor
    {
        // Stores the current state of the tree as we're recursing through it
        private bool _isEvaluatable = true;

        /// <summary>
        /// Simplifies an expression tree by evaluating any branches of the tree that do not include
        /// lambda parameter references.  This will remove references to variables external to the lambda
        /// by converting them to constants, perform arithmetic, and execute method calls as needed.
        /// For example, a call to "str.ToUpper()" where string is an external variable would be simplified
        /// to a <see cref="ConstantExpression"/> containing the uppercase version of str.
        /// </summary>
        /// <param name="expression">Expression to simplify.</param>
        /// <returns>The simplified expression.</returns>
        public static Expression Simplify(Expression expression)
        {
            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            if (expression == null)
            {
                ThrowHelper.ThrowArgumentNullException(nameof(expression));
            }

            var visitor = new LambdaSimplifyingExpressionVisitor();

            var newExpression = visitor.Visit(expression);

            return visitor._isEvaluatable
                ? ConvertToConstant(newExpression)
                : newExpression;
        }

        /// <summary>
        /// Private constructor, only accessible via static method <see cref="Simplify"/>.
        /// </summary>
        private LambdaSimplifyingExpressionVisitor()
        {
        }

        [return: NotNullIfNotNull("node")]
        public override Expression? Visit(Expression? node)
        {
            if (node == null)
            {
                return null;
            }

            while (node.CanReduce)
            {
                node = node.ReduceAndCheck();
            }

            return base.Visit(node);
        }

        #region VisitChildren

        private bool IsChildEvaluatable(ref Expression? child)
        {
            if (child == null)
            {
                return true;
            }
            else
            {
                // Evaluate the child to see if the entire child tree is evaluatable

                _isEvaluatable = true;
                child = Visit(child);
                return _isEvaluatable;
            }
        }

        /// <summary>
        /// Visits a list of children to see if they are evaluatable or not.  If a branch of the tree
        /// can be evaluated but another cannot, simplifies the branches that can be evaluated to
        /// constants.  Modifies the provided collection with the new expressions.
        /// </summary>
        private void VisitChildren(ref Expression? child1, ref Expression? child2)
        {
            var evaluatableChild1 = IsChildEvaluatable(ref child1);
            var evaluatableChild2 = IsChildEvaluatable(ref child2);

            var allChildrenAreEvaluatable = evaluatableChild1 && evaluatableChild2;

            // When moving back up the tree, we are only evaluatable if all children are evaluatable
            _isEvaluatable = allChildrenAreEvaluatable;

            if (!allChildrenAreEvaluatable)
            {
                // Some of the children are evaluatable and others are not, so go ahead and evaluate
                // the children that can be evaluated and convert them to constants

                if (evaluatableChild1)
                {
                    child1 = ConvertToConstant(child1!);
                }
                if (evaluatableChild2)
                {
                    child2 = ConvertToConstant(child2!);
                }
            }
        }

        /// <summary>
        /// Visits a list of children to see if they are evaluatable or not.  If a branch of the tree
        /// can be evaluated but another cannot, simplifies the branches that can be evaluated to
        /// constants.  Modifies the provided collection with the new expressions.
        /// </summary>
        private void VisitChildren(ref Expression? child1, ref Expression? child2, ref Expression? child3)
        {
            var evaluatableChild1 = IsChildEvaluatable(ref child1);
            var evaluatableChild2 = IsChildEvaluatable(ref child2);
            var evaluatableChild3 = IsChildEvaluatable(ref child3);

            var allChildrenAreEvaluatable = evaluatableChild1 && evaluatableChild2 && evaluatableChild3;

            // When moving back up the tree, we are only evaluatable if all children are evaluatable
            _isEvaluatable = allChildrenAreEvaluatable;

            if (!allChildrenAreEvaluatable)
            {
                // Some of the children are evaluatable and others are not, so go ahead and evaluate
                // the children that can be evaluated and convert them to constants

                if (evaluatableChild1)
                {
                    child1 = ConvertToConstant(child1!);
                }
                if (evaluatableChild2)
                {
                    child2 = ConvertToConstant(child2!);
                }
                if (evaluatableChild3)
                {
                    child3 = ConvertToConstant(child3!);
                }
            }
        }

        /// <summary>
        /// Visits a list of children to see if they are evaluatable or not.  If a branch of the tree
        /// can be evaluated but another cannot, simplifies the branches that can be evaluated to
        /// constants.  Modifies the provided collection with the new expressions.
        /// </summary>
        /// <param name="children">List of children to evaluate.  Null children are skipped.  This list is updated with the new children.</param>
        private void VisitChildren(Span<Expression?> children)
        {
            var allChildrenAreEvaluatable = true;
            Span<bool> evaluatableChildren = children.Length <= 8 // Safety measure to avoid stack overflow
                ? stackalloc bool[children.Length]
                : new bool[children.Length];

            for (var i=0; i<children.Length; i++)
            {
                var isEvaluatable = IsChildEvaluatable(ref children[i]);
                if (!isEvaluatable)
                {
                    allChildrenAreEvaluatable = false;
                }
            }

            // When moving back up the tree, we are only evaluatable if all children are evaluatable
            _isEvaluatable = allChildrenAreEvaluatable;

            if (!allChildrenAreEvaluatable)
            {
                // Some of the children are evaluatable and others are not, so go ahead and evaluate
                // the children that can be evaluated and convert them to constants

                for (var i = 0; i < children.Length; i++)
                {
                    if (evaluatableChildren[i] && (children[i] != null))
                    {
                        children[i] = ConvertToConstant(children[i]!);
                    }
                }
            }
        }

        #endregion

        protected override Expression VisitParameter(ParameterExpression node)
        {
            // Once we encounter a parameter node, we know this branch of the tree cannot be evaluated to a constant
            _isEvaluatable = false;

            return base.VisitParameter(node);
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            var left = node.Left;
            var right = node.Right;

            VisitChildren(ref left, ref right);

            return node.Update(left!, node.Conversion, right!);
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            var test = node.Test;
            var ifTrue = node.IfTrue;
            var ifFalse = node.IfFalse;

            VisitChildren(ref test, ref ifTrue, ref ifFalse);

            return node.Update(test!, ifTrue!, ifFalse!);
        }

        protected override Expression VisitMethodCall(MethodCallExpression node)
        {
            // Try several optimizations for small argument counts, fallback to a more expensive
            // array from the array pool if required.

            switch (node.Arguments.Count)
            {
                case 0:
                    return node.Update(Visit(node.Object!), node.Arguments);

                case 1:
                {
                    var obj = node.Object;
                    var arg0 = node.Arguments[0];

                    VisitChildren(ref obj, ref arg0);

                    if (!ReferenceEquals(obj, node.Object) || !ReferenceEquals(arg0, node.Arguments[0]))
                    {
                        return node.Update(obj!, new[] {arg0!});
                    }

                    return node;
                }

                case 2:
                {
                    var obj = node.Object;
                    var arg0 = node.Arguments[0];
                    var arg1 = node.Arguments[1];

                    VisitChildren(ref obj, ref arg0, ref arg1);

                    if (!ReferenceEquals(obj, node.Object) || !ReferenceEquals(arg0, node.Arguments[0]) || !ReferenceEquals(arg1, node.Arguments[1]))
                    {
                        return node.Update(obj!, new[] {arg0!, arg1!});
                    }

                    return node;
                }

                default:
                    var nodeCount = node.Arguments.Count + 1;
                    var children = ArrayPool<Expression?>.Shared.Rent(nodeCount);
                    try
                    {
                        children[0] = node.Object;
                        node.Arguments.CopyTo(children!, 1);

                        VisitChildren(children.AsSpan(0, nodeCount));

                        return node.Update(children[0]!, children.Take(nodeCount).Skip(1)!);
                    }
                    finally
                    {
                        ArrayPool<Expression?>.Shared.Return(children);
                    }
            }
        }

        private static ConstantExpression ConvertToConstant(Expression node)
        {
            if (node is ConstantExpression constantExpression)
            {
                // Short circuit for this common case, probably a literal indexing into a list or dictionary.
                return constantExpression;
            }

            if (node is MemberExpression {Expression: ConstantExpression objExpression, Member: FieldInfo memberInfo})
            {
                // Short circuit for the common case where accessing a local variable from the lambda,
                // avoiding a costly JIT calling Compile on a lambda expression. These appear as field
                // accesses on a constant object, which is the hidden closure object generated by the compiler.
                //
                // e.g.
                // int index = CalculateIndex(...);//
                // var result = await _collection.LookupInAsync<DocType>("customer123", specs =>
                //     specs.Get(p => p.Array[index])
                // );
                //
                // Note that this optimization doesn't cover advanced cases like:
                //     specs.Get(p => p.Array[index + 1]);
                // Such cases will still fall through and compile to do the arithmetic

                return Expression.Constant(memberInfo.GetValue(objExpression.Value), node.Type);
            }

            Expression<Func<object>> lambda =
                Expression.Lambda<Func<object>>(Expression.Convert(node, typeof(object)));
            var compiledLambda = lambda.Compile();

            object value = compiledLambda();
            return Expression.Constant(value, node.Type);
        }
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
