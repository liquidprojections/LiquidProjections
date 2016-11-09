using System.Collections.Generic;

namespace LiquidProjections.Specs
{
    internal static class EnumerableExtensions
    {
        public static IEnumerable<IList<T>> InBatchesOf<T>(this IEnumerable<T> items, int batchSize)
        {
            var batch = new List<T>(batchSize);

            foreach (var item in items)
            {
                batch.Add(item);

                if (batch.Count >= batchSize)
                {
                    yield return batch;
                    batch = new List<T>(batchSize);
                }
            }

            if (batch.Count != 0)
            {
                yield return batch;
            }
        }
    }
}