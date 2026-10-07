using System.ComponentModel.DataAnnotations;

namespace DotLiquid.Website
{
    /// <summary>
    /// Configuration controlling how user-submitted templates are rendered on the "Try Online" page.
    /// Both settings are safeguards against excessively expensive templates (e.g. large or nested
    /// loops) and are given safe defaults in case the configuration section is missing or incomplete.
    /// A value of 0 means "unlimited" for either setting.
    /// </summary>
    public class LiquidRenderingOptions
    {
        /// <summary>
        /// The name of the configuration section these options are bound to.
        /// </summary>
        public const string SectionName = "LiquidRendering";

        /// <summary>
        /// Maximum number of iterations permitted for a single "for" loop. A value of 0 means unlimited.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "LiquidRendering:MaxIterations must not be negative.")]
        public int MaxIterations { get; set; } = 10_000;

        /// <summary>
        /// Maximum number of seconds rendering is allowed to run before being cancelled. A value of 0 means unlimited.
        /// </summary>
        [Range(0, int.MaxValue, ErrorMessage = "LiquidRendering:TimeoutSeconds must not be negative.")]
        public int TimeoutSeconds { get; set; } = 5;
    }
}
