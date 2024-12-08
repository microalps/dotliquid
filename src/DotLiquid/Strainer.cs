using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DotLiquid.Exceptions;
using DotLiquid.Util;

namespace DotLiquid
{
    /// <summary>
    /// Strainer is the parent class for the filters system.
    /// New filters are mixed into the strainer class which is then instantiated for each liquid template render run.
    ///
    /// One of the strainer's responsibilities is to keep malicious method calls out
    /// </summary>
    public class Strainer
    {
        private static readonly Dictionary<string, Type> Filters = new Dictionary<string, Type>();
        private static readonly Dictionary<string, Tuple<object, MethodInfo>> FilterFuncs = new Dictionary<string, Tuple<object, MethodInfo>>();
        private static ConcurrentDictionary<Type, LiquidFilterInfo[]> FilterReflectionCache = new ConcurrentDictionary<Type, LiquidFilterInfo[]>();

        public static void GlobalFilter(Type filter)
        {
            Filters[filter.AssemblyQualifiedName] = filter;
        }

        public static void GlobalFilter(string rawName, object target, MethodInfo methodInfo)
        {
            var name = Template.NamingConvention.GetMemberName(rawName);

            FilterFuncs[name] = Tuple.Create(target, methodInfo);
        }

        public static Strainer Create(Context context)
        {
            Strainer strainer = new Strainer(context);

            foreach (var keyValue in Filters)
                strainer.Extend(keyValue.Value);

            foreach (var keyValue in FilterFuncs)
                strainer.AddMethodInfo(keyValue.Key, keyValue.Value.Item1, keyValue.Value.Item2);
            
            return strainer;
        }

        private readonly Context _context;
        private readonly Dictionary<string, IList<Tuple<object, LiquidFilterInfo>>> _methods = new Dictionary<string, IList<Tuple<object, LiquidFilterInfo>>>();

        public IEnumerable<LiquidFilterInfo> Methods
        {
            get { return _methods.Values.SelectMany(m => m.Select(x => x.Item2)); }
        }

        public Strainer(Context context)
        {
            _context = context;
        }

        /// <summary>
        /// In this C# implementation, we can't use mixins. So we grab all the static
        /// methods from the specified type and use them instead.
        /// </summary>
        /// <param name="type"></param>
        public void Extend(Type type)
        {
            // Calls to Extend replace existing filters with the same number of params.
            var methods = FilterReflectionCache.GetOrAdd(type, k => type.GetRuntimeMethods().Where(m => m.IsPublic && m.IsStatic).Select(m => new LiquidFilterInfo(m)).ToArray());
            foreach (var method in methods)
            {
                string methodName = Template.NamingConvention.GetMemberName(method.Name);
                if  (_methods.Any(m => method.MatchesMethod(m)))
                {
                    _methods.Remove(methodName);
                }
            }

            foreach (LiquidFilterInfo methodInfo in methods)
            {
                AddMethodInfo(methodInfo.Name, null, methodInfo);
            } // foreach
        }

        public void AddFunction<TIn, TOut>(string rawName, Func<TIn, TOut> func)
        {
            AddMethodInfo(rawName, func.Target, func.GetMethodInfo());
        }

        public void AddFunction<TIn, TIn2, TOut>(string rawName, Func<TIn, TIn2, TOut> func)
        {
            AddMethodInfo(rawName, func.Target, func.GetMethodInfo());
        }

        public void AddMethodInfo(string rawName, object target, MethodInfo method) => AddMethodInfo(rawName, target, new LiquidFilterInfo(method));

        internal void AddMethodInfo(string rawName, object target, LiquidFilterInfo method)
        {
            var name = Template.NamingConvention.GetMemberName(rawName);
            _methods.TryAdd(name, () => new List<Tuple<object, LiquidFilterInfo>>()).Add(Tuple.Create(target, method));
        }

        public bool RespondTo(string method)
        {
            return _methods.ContainsKey(method);
        }

        /// <summary>
        /// Invoke specified method with provided arguments
        /// </summary>
        /// <param name="method">The method token.</param>
        /// <param name="args">The ordered arguments for invoking the method</param>
        /// <param name="namedArgs">The named arguments for invoking the method</param>
        /// <returns>The method's return.</returns>
        public object Invoke(string method, List<object> args, Dictionary<string, object> namedArgs)
        {
            // First, try to find a method with the same number of unnamed arguments which we set automatically further down.
            // If we failed to do so, try one with max numbers of arguments, hoping
            // that those not explicitly specified will be taken care of
            // by default values
            var methodInfo = _methods[method].FirstOrDefault(m => m.Item2.IsCountAndNamedMatch(args.Count, namedArgs?.Keys))
                ?? _methods[method].OrderByDescending(m => m.Item2.GetParameters().Length).First();

            ParameterInfo[] parameterInfos = methodInfo.Item2.GetParameters();

            // If first parameter is Context, send in actual context.
            var isFirstParameterContext = parameterInfos.Length > 0 && parameterInfos[0].ParameterType == typeof(Context);
            var firstParameterIndex = isFirstParameterContext ? 1 : 0;
            if (isFirstParameterContext)
                args.Insert(0, _context);

            // Remove any arguments that exceed the method count
            if (args.Count > methodInfo.Item2.OrderedParameterCount + firstParameterIndex)
            {
                var itemsToKeep = methodInfo.Item2.OrderedParameterCount + firstParameterIndex;
                args.RemoveRange(itemsToKeep, args.Count - itemsToKeep);
            }

            // Add in any default parameters - .NET won't do this for us.
            if (parameterInfos.Length > args.Count)
                for (int i = args.Count; i < parameterInfos.Length; ++i)
                {
                    if ((parameterInfos[i].Attributes & ParameterAttributes.HasDefault) != ParameterAttributes.HasDefault)
                        throw new SyntaxException(Liquid.ResourceManager.GetString("StrainerFilterHasNoValueException"), method, parameterInfos[i].Name);
                    args.Add(parameterInfos[i].DefaultValue);
                }

            // Named arguments must have default values since they were created above. Now set their value accordingly 
            if (namedArgs != null)
            {
                foreach (var namedArg in namedArgs)
                {
                    var matchedArg = parameterInfos.FirstOrDefault(p => Template.NamingConvention.GetMemberName(p.Name) == namedArg.Key);
                    if (matchedArg == null)
                        continue;

                    args[matchedArg.Position] = namedArg.Value;
                }
            }

            // Attempt conversions where required by type mismatch and possible by value range.
            // These may be narrowing conversions (e.g. Int64 to Int32) when the actual range doesn't cause an overflow.
            for (var argumentIndex = firstParameterIndex; argumentIndex < parameterInfos.Length; argumentIndex++)
            {
                if (args[argumentIndex] is IConvertible convertibleArg)
                {
                    var parameterType = parameterInfos[argumentIndex].ParameterType;
                    if (convertibleArg.GetType() != parameterType
                        && !parameterType
#if NETSTANDARD1_3
                            .GetTypeInfo()
#endif
                            .IsAssignableFrom(
                                convertibleArg
                                    .GetType()
#if NETSTANDARD1_3
                                    .GetTypeInfo()
#endif
                                    )
                        )
                    {
                        args[argumentIndex] = Convert.ChangeType(convertibleArg, parameterType);
                    }
                }
            }

            try
            {
                return methodInfo.Item2.Invoke(methodInfo.Item1, args.ToArray());
            }
            catch (TargetInvocationException ex)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }
        }
    }
}
