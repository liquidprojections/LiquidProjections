using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace eVision.QueryHost.Specs
{
    public static class EnumerableExtensions
    {
        public static void ForEach<T>(this IEnumerable<T> self, Action<T> action)
        {
            foreach (var item in self)
            {
                action(item);
            }
        }

        public static async Task ForEach<T>(this IEnumerable<T> self, Func<T, Task> asyncAction)
        {
            foreach (var item in self)
            {
                await asyncAction(item);
            }
        }
    }
}