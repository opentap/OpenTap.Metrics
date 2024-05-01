using System;
using System.Collections.Generic;

namespace OpenTap.Metrics;

static class ReflectionHelpoer
{
    /// <summary>
    /// Returns true if a type is numeric.
    /// </summary>
    public static bool IsNumeric(this Type t)
    {
        if (t.IsEnum)
            return false;
        switch (Type.GetTypeCode(t))
        {
            case TypeCode.Byte:
            case TypeCode.SByte:
            case TypeCode.UInt16:
            case TypeCode.UInt32:
            case TypeCode.UInt64:
            case TypeCode.Int16:
            case TypeCode.Int32:
            case TypeCode.Int64:
            case TypeCode.Decimal:
            case TypeCode.Double:
            case TypeCode.Single:
                return true;
            default:
                return false;
        }
    }

    public static TypeData AsTypeData(this ITypeData type)
    {
        do
        {
            if (type is TypeData td)
                return td;
            type = type?.BaseType;
        } while (type != null);

        return null;
    }

    /// <summary> Returns true if a type is numeric. </summary>
    public static bool IsNumeric(this ITypeData t)
    {
        return t.AsTypeData()?.Type.IsNumeric() == true;
    }

    struct OnceLogToken
    {
        public object Token;
        public TraceSource Log;
    }

    static HashSet<OnceLogToken> logOnceTokens = new HashSet<OnceLogToken>();

    /// <summary>
    /// Avoids spamming the log with errors that 
    /// should only be shown once by memorizing token and TraceSource. 
    /// </summary>
    /// <returns>True if an error was logged.</returns>
    public static bool ErrorOnce(this TraceSource log, object token, string message, params object[] args)
    {
        lock (logOnceTokens)
        {
            var logtoken = new OnceLogToken { Token = token, Log = log };
            if (!logOnceTokens.Contains(logtoken))
            {
                log.Error(message, args);
                logOnceTokens.Add(logtoken);
                return true;
            }

            return false;
        }
    }

    /// <summary>
    /// Creates a HashSet from an IEnumerable.
    /// </summary>
    public static HashSet<T> ToHashSet<T>(this IEnumerable<T> source)
    {
        return new HashSet<T>(source);
    }
}