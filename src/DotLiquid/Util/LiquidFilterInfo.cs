using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;

namespace DotLiquid.Util
{
    /// <summary>
    /// Represents a native .NET method exposed as a Liquid Filter
    /// </summary>
    public class LiquidFilterInfo
    {
        private readonly MethodInfo method;
        private readonly string[] namedParameters;

        /// <summary>
        /// Gets the number of ordered parameters for the current member.
        /// </summary>
        public int OrderedParameterCount { get; private set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DotLiquid.Util.LiquidFilterInfo">LiquidFilterInfo</see> class
        /// </summary>
        /// <param name="method"></param>
        public LiquidFilterInfo(MethodInfo method)
        {
            this.method = method;

            var parameters = method.GetParameters();
            var namedParametersArray = parameters
                .Where(parameter => parameter.IsOptional && parameter.GetCustomAttributes(typeof(LiquidNamedArgumentAttribute)).FirstOrDefault() != null)
                .Select(parameter => parameter.Name).ToArray();
            OrderedParameterCount = parameters.Count(parameter => parameter.ParameterType != typeof(Context) && !namedParametersArray.Contains(parameter.Name));
            namedParameters = namedParametersArray;
        }

        /// <summary>
        /// Gets the name of the current member.
        /// </summary>
        public string Name => method.Name;

        /// <summary>
        /// Gets the parameters for this method.
        /// </summary>
        /// <returns>An array of type ParameterInfo containing information that matches the signature of the method reflected by this instance.</returns>
        public ParameterInfo[] GetParameters() => method.GetParameters();


        /// <summary>
        /// Invokes the method represented by the current instance, using the specified parameters.
        /// </summary>
        /// <param name="obj">The object on which to invoke the method.</param>
        /// <param name="parameters">An argument list for the invoked method. This is an array of objects with the
        /// same number, order, and type as the parameters of the method or constructor to be invoked</param>
        /// <returns></returns>
        public object Invoke(object obj, object[] parameters) => method.Invoke(obj, parameters);

        /// <summary>
        /// Gets a value indicating whether the method matches the required unnamed argument count and named arguments
        /// </summary>
        /// <param name="expectedOrderedParameters">The expected number of ordered parameters</param>
        /// <param name="expectedNamedParameters">The expected named parameters</param>
        internal bool IsCountAndNamedMatch(int expectedOrderedParameters, IEnumerable<string> expectedNamedParameters)
        {
            if (expectedOrderedParameters != OrderedParameterCount)
                return false;

            var expectedNamedParameterCount = expectedNamedParameters?.Count() ?? 0;
            if (expectedNamedParameterCount == 0)
                return namedParameters.Length == 0;

            return expectedNamedParameterCount == namedParameters.Length
                && namedParameters.Intersect(expectedNamedParameters).Count() == namedParameters.Length;
        }

        /// <summary>
        /// Check if current method matches compareMethod in name and in parameters
        /// </summary>
        /// <param name="compareMethod"></param>
        /// <returns></returns>
        internal bool MatchesMethod(KeyValuePair<string, IList<Tuple<object, LiquidFilterInfo>>> compareMethod)
        {
            if (compareMethod.Key != Template.NamingConvention.GetMemberName(method.Name))
            {
                return false;
            }

            return compareMethod.Value.Any(m => m.Item2.IsCountAndNamedMatch(OrderedParameterCount, namedParameters));
        }
    }
}
