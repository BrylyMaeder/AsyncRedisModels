using AsyncRedisModels.Attributes;
using AsyncRedisModels.Helper;
using AsyncRedisModels.Index;
using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;

namespace AsyncRedisModels.Query
{
    public class RedisQuery<TModel> 
    {
        public readonly List<string> conditions;

        public RedisQuery()
        {
            conditions = new List<string>();
        }
        public RedisQuery<TModel> Where(Expression<Func<TModel, bool>> predicate)
        {
            var condition = ParseExpression(predicate.Body, predicate.Parameters[0]);
            if (!string.IsNullOrEmpty(condition))
                conditions.Add(condition);
            return this;
        }

        public (string indexName, string query) Build()
        {
            var index = ModelHelper.GetIndex<TModel>();
            if (conditions.Count == 0)
                return (index, "*");

            return (index, conditions.Count == 1
                ? conditions[0]
                : $"({string.Join(" ", conditions)})");
        }

        private string ParseExpression(Expression expression, ParameterExpression parameter)
        {
            switch (expression)
            {
                case BinaryExpression binary:
                    return ParseBinaryExpression(binary, parameter);

                case UnaryExpression unary when unary.NodeType == ExpressionType.Not:
                    return $"-({ParseExpression(unary.Operand, parameter)})";

                case MethodCallExpression methodCall:
                    return ParseMethodCallExpression(methodCall, parameter);

                case ConstantExpression constant:
                    return constant.Value?.ToString() ?? "null";

                default:
                    throw new NotSupportedException($"Expression type {expression.NodeType} is not supported");
            }
        }

        private string ParseBinaryExpression(BinaryExpression binary, ParameterExpression parameter)
        {
            string left;
            IndexType indexType = IndexType.Auto; // Default to Auto
            if (binary.Left is MemberExpression member)
            {
                left = GetFieldName(member);
                // Get the IndexType from the IndexedAttribute if present
                var propertyInfo = member.Member as System.Reflection.PropertyInfo;
                if (propertyInfo != null)
                {
                    var indexedAttr = propertyInfo.GetCustomAttribute<IndexedAttribute>();
                    indexType = indexedAttr?.IndexType ?? IndexType.Auto;
                }
            }
            else
            {
                left = ParseExpression(binary.Left, parameter);
            }

            // Handle the right side properly
            object rightValue = EvaluateExpression(binary.Right, parameter);
            if (rightValue is DateTime dateTime)
                rightValue = ToUnixSeconds(dateTime);
            else if (rightValue is TimeSpan timeSpan)
                rightValue = (long)timeSpan.TotalSeconds;

            // Determine how to format based on IndexType
            switch (indexType)
            {
                case IndexType.Text:
                case IndexType.Auto when rightValue is string: // Auto defaults to Text for strings
                                                               // Full-text search
                    if (rightValue is string textValue)
                    {
                        string escapedValue = EscapeValue(textValue);
                        switch (binary.NodeType)
                        {
                            case ExpressionType.Equal:
                                return $"@{left}:{escapedValue}";
                            case ExpressionType.NotEqual:
                                return $"-@{left}:{escapedValue}";
                            default:
                                throw new NotSupportedException($"Operator {binary.NodeType} is not supported for Text index");
                        }
                    }
                    break;

                case IndexType.Tag:
                    // Tag search
                    if (rightValue is string tagValue)
                    {
                        string escapedValue = EscapeValue(tagValue);
                        switch (binary.NodeType)
                        {
                            case ExpressionType.Equal:
                                return $"@{left}:{{{escapedValue}}}";
                            case ExpressionType.NotEqual:
                                return $"-@{left}:{{{escapedValue}}}";
                            default:
                                throw new NotSupportedException($"Operator {binary.NodeType} is not supported for Tag index");
                        }
                    }
                    break;

                case IndexType.Numeric:
                case IndexType.Auto: // Auto defaults to Numeric for non-strings
                                     // Numeric search
                    switch (binary.NodeType)
                    {
                        case ExpressionType.Equal:
                            return $"@{left}:[{rightValue} {rightValue}]";
                        case ExpressionType.NotEqual:
                            return $"-@{left}:[{rightValue} {rightValue}]";
                        case ExpressionType.GreaterThan:
                            return $"@{left}:[{Convert.ToDouble(rightValue) + 0.001} +inf]";
                        case ExpressionType.GreaterThanOrEqual:
                            return $"@{left}:[{rightValue} +inf]";
                        case ExpressionType.LessThan:
                            return $"@{left}:[-inf {Convert.ToDouble(rightValue) - 0.001}]";
                        case ExpressionType.LessThanOrEqual:
                            return $"@{left}:[-inf {rightValue}]";
                        case ExpressionType.AndAlso:
                        case ExpressionType.And:
                            return $"({ParseExpression(binary.Left, parameter)} {ParseExpression(binary.Right, parameter)})";
                        case ExpressionType.OrElse:
                        case ExpressionType.Or:
                            return $"({ParseExpression(binary.Left, parameter)} | {ParseExpression(binary.Right, parameter)})";
                        default:
                            throw new NotSupportedException($"Operator {binary.NodeType} is not supported");
                    }
            }

            throw new NotSupportedException($"Cannot process value {rightValue} with index type {indexType}");
        }

        private string ParseMethodCallExpression(MethodCallExpression methodCall, ParameterExpression parameter)
        {
            if (methodCall.Object != null && methodCall.Method.DeclaringType == typeof(string))
            {
                if (methodCall.Object is MemberExpression member)
                {
                    var fieldName = member.Member.Name;
                    var propertyInfo = member.Member as System.Reflection.PropertyInfo;
                    var indexedAttr = propertyInfo?.GetCustomAttribute<IndexedAttribute>();
                    var indexType = indexedAttr?.IndexType ?? IndexType.Auto;

                    var value = EvaluateExpression(methodCall.Arguments[0], parameter);
                    if (value is DateTime dt)
                        value = ToUnixSeconds(dt);
                    else if (value is TimeSpan ts)
                        value = (long)ts.TotalSeconds;

                    string stringValue = value?.ToString() ?? "";
                    string escapedValue = EscapeValue(stringValue);

                    if (indexType == IndexType.Text || (indexType == IndexType.Auto && value is string))
                    {
                        // Text search with wildcard support
                        switch (methodCall.Method.Name)
                        {
                            case "Contains":
                                return $"@{fieldName}:*{escapedValue}*";
                            case "StartsWith":
                                return $"@{fieldName}:{escapedValue}*";
                            case "EndsWith":
                                return $"@{fieldName}:*{escapedValue}";
                            default:
                                throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for Text index");
                        }
                    }
                    else if (indexType == IndexType.Tag)
                    {
                        // Tag search (no wildcards)
                        switch (methodCall.Method.Name)
                        {
                            case "Contains":
                            case "StartsWith":
                            case "EndsWith":
                                return $"@{fieldName}:{{{escapedValue}}}"; // Tags don't support wildcards, so treat as exact match
                            default:
                                throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported for Tag index");
                        }
                    }
                }
            }
            throw new NotSupportedException($"Method {methodCall.Method.Name} is not supported");
        }

        private string GetFieldName(Expression expression)
        {
            if (expression is MemberExpression member)
                return member.Member.Name;

            throw new NotSupportedException("Left side of expression must be a property");
        }

        private object EvaluateExpression(Expression expression, ParameterExpression parameter)
        {
            try
            {
                if (expression is ConstantExpression constant)
                    return constant.Value;

                if (expression is MemberExpression member)
                {
                    if (member.Expression == null && member.Member.Name == "Now" &&
                        member.Member.DeclaringType == typeof(DateTime))
                    {
                        return DateTime.Now; // Handle DateTime.Now directly
                    }

                    var obj = member.Expression == null ? null : EvaluateExpression(member.Expression, parameter);
                    switch (member.Member)
                    {
                        case System.Reflection.FieldInfo field:
                            return field.GetValue(obj);
                        case System.Reflection.PropertyInfo prop:
                            return prop.GetValue(obj);
                        default:
                            throw new NotSupportedException("Unsupported member type");
                    }
                }

                // Compile the expression with the parameter, but don't invoke it with a value since we're just parsing
                var lambda = Expression.Lambda(expression, parameter);
                var compiled = lambda.Compile();

                // If it's a simple value we can evaluate immediately
                if (lambda.Parameters.Count == 0)
                    return compiled.DynamicInvoke();

                // For expressions requiring the parameter, we'll evaluate it at runtime
                // Since we're building a query string, we return null and handle it in ParseBinaryExpression
                return null;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException($"Failed to evaluate expression: {expression}", ex);
            }
        }

        private long ToUnixSeconds(DateTime dateTime)
        {
            return (long)(dateTime.ToUniversalTime() - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        private string EscapeValue(string value)
        {
            return value.Replace("\"", "\\\"")    // Escape quotes
                        .Replace(" ", "\\ ")      // Escape spaces
                        .Replace(":", "\\:")      // Escape colons
                        .Replace("@", "\\@");     // Escape @ symbol
        }
    }
}
