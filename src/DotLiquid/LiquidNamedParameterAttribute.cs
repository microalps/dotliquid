using System;

namespace DotLiquid
{
    /// <summary>
    /// Specifies a filter parameter as being named only.
    /// </summary>
    [AttributeUsage(AttributeTargets.Parameter)]
    public class LiquidNamedParameterAttribute : Attribute
    {
        /// <summary>
        /// Initializes a new instance of the DotLiquid.LiquidNamedArgumentAttribute class.
        /// </summary>]
        public LiquidNamedParameterAttribute()
        {
        }
    }
}
