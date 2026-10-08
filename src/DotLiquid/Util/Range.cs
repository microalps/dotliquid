using System.Collections.Generic;
using DotLiquid.Exceptions;

namespace DotLiquid.Util
{
    internal static class Range
    {
        public static IEnumerable<int> Inclusive(Context context, int start, int finish)
        {
            if (start > finish)
            {
                yield break;
            }

            // MaxIterations is enforced incrementally as the range is enumerated, so that
            // only elements actually consumed (e.g. before a "limit" stops the for loop)
            // count toward the limit, rather than the range's total cardinality.
            int current = start;
            long count = 0;
            do
            {
                if (context.MaxIterations > 0 && ++count > context.MaxIterations)
                {
                    throw new MaximumIterationsExceededException(Tags.For.ForTagMaxIterationsExceededException, context.MaxIterations.ToString());
                }

                yield return current;
            }
            while (current++ != finish);
        }
    }
}
