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

            // Compute the count using Int64 arithmetic so it cannot overflow,
            // even when startingRange/endingRange are near the Int32 bounds.
            long count = (long)finish - start + 1L;

            if (context.MaxIterations > 0 && count > context.MaxIterations)
            {
                throw new MaximumIterationsExceededException(Tags.For.ForTagMaxIterationsExceededException, context.MaxIterations.ToString());
            }

            int current = start;
            do
            {
                yield return current;
            }
            while (current++ != finish);
        }
    }
}
