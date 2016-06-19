using System;
using System.Linq.Expressions;
using System.Reflection;

namespace eVision.QueryHost.Common
{
    /// <summary>
    /// Purpose of this class is to get property and method names using static reflection. This can be used to  avoid having
    /// 'magic' strings when calling methods using reflection. Using 'magic' string makes refactoring much harder and
    /// error prone. using the <seealso cref="StaticReflection"/> class makes sure that renaming a method will
    /// not break the application in places where reflection is used.
    /// <br />
    /// <br />
    /// See also:<br />
    /// http://joelabrahamsson.com/getting-property-and-method-names-using-static-reflection-in-c/
    ///  </summary>
    internal static class StaticReflection
    {
        public static string GetMemberName<T>(Expression<Func<T, object>> expression)
        {
            return GetMemberInfo(expression.Body).Name;
        }

        public static string GetMemberName<T>(Expression<Action<T>> expression)
        {
            return GetMemberInfo(expression.Body).Name;
        }

        private static MemberInfo GetMemberInfo(Expression expression)
        {
            var memberExpression = expression as MemberExpression;
            if (memberExpression != null)
            {
                // Reference type property or field
                return memberExpression.Member;
            }

            var methodCallExpression = expression as MethodCallExpression;
            if (methodCallExpression != null)
            {
                // Reference type method
                return methodCallExpression.Method;
            }

            var unaryExpression = expression as UnaryExpression;
            if (unaryExpression != null)
            {
                // Property, field of method returning value type
                return GetMemberInfo(unaryExpression);
            }

            throw new ArgumentException("Invalid expression");
        }

        private static MemberInfo GetMemberInfo(UnaryExpression unaryExpression)
        {
            var methodExpression = unaryExpression.Operand as MethodCallExpression;
            if (methodExpression != null)
            {
                return methodExpression.Method;
            }

            return ((MemberExpression)unaryExpression.Operand).Member;
        }
    }
}