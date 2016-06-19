using System;
using System.Reflection;

namespace eVision.QueryHost.Common
{
    internal static class ExceptionExtensions
    {
        /// <summary>
        /// Returns the inner exception which is wrapped by the <see cref="TargetInvocationException"/> while preserving the stack trace.
        /// </summary>
        /// <param name="ex">The TargetInvocationException which is thrown.</param>
        /// <returns>The inner exception with correct stack trace.</returns>
        public static Exception Unwrap(this Exception ex)
        {
            while (ex is TargetInvocationException)
            {
                FieldInfo remoteStackTraceString =
                    typeof(Exception).GetField("_remoteStackTraceString", BindingFlags.Instance | BindingFlags.NonPublic);

                remoteStackTraceString.SetValue(ex.InnerException, ex.InnerException.StackTrace + Environment.NewLine);

                ex = ex.InnerException;
            }

            return ex;
        }
    }
}