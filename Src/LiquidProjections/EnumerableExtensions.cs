using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;

namespace LiquidProjections
{
    [DebuggerNonUserCode]
    internal static class EnumerableExtensions
    {
        public static IReadOnlyList<T> ToReadOnly<T>(this IEnumerable<T> items)
        {
            return new ReadOnlyCollection<T>(items.ToList());
        }
    }
}