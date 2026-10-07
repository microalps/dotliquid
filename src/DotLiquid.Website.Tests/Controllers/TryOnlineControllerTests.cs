using System;
using System.Diagnostics;
using DotLiquid.Exceptions;
using DotLiquid.Website.Controllers;
using Microsoft.Extensions.Options;
using NUnit.Framework;

namespace DotLiquid.Website.Tests.Controllers
{
    [TestFixture]
    public class TryOnlineControllerTests
    {
        private static TryOnlineController CreateController(LiquidRenderingOptions options)
        {
            return new TryOnlineController(Options.Create(options));
        }

        /// <summary>
        /// A malicious/careless user can submit a template with a loop over a huge range
        /// (e.g. 0..1,000,000). This test configures a very low MaxIterations setting
        /// (independently of the timeout, which is given a generous value) and asserts the
        /// "Liquify" action aborts rendering with a MaximumIterationsExceededException well
        /// before the timeout would ever be hit.
        /// </summary>
        [Test]
        public void LiquifyAction_WhenMaxIterationsExceeded_ThrowsMaximumIterationsExceededException()
        {
            // Arrange
            const string templateCode = @"{% for i in (0..1000000) %}{{ i }}{% endfor %}";
            var options = new LiquidRenderingOptions
            {
                MaxIterations = 10,
                TimeoutSeconds = 60 // generous, so the timeout cannot be the cause of the failure
            };
            var controller = CreateController(options);

            // Act / Assert
            Assert.Throws<MaximumIterationsExceededException>(() => controller.Liquify(templateCode));
        }

        /// <summary>
        /// A malicious/careless user can submit a template with a loop nested inside another loop,
        /// each iterating over a huge range. Without a render timeout, this ties up a web server
        /// thread for an excessive amount of time (effectively a Denial of Service).
        ///
        /// This test configures a very low timeout (independently of MaxIterations, which is given a
        /// generous value) and asserts the "Liquify" action aborts rendering with an
        /// OperationCanceledException within a bounded amount of time, instead of running the
        /// unbounded nested loops to completion.
        /// </summary>
        [Test]
        public void LiquifyAction_WhenTimeoutExceeded_CancelsWithinTimeout()
        {
            // Arrange
            const string templateCode = @"{% for i in (0..1000000) %}{% for j in (0..1000000) %}{{ i }}{{ j }}{% endfor %}{% endfor %}";
            var options = new LiquidRenderingOptions
            {
                MaxIterations = 0, // infinite, so MaxIterations cannot be the cause of the failure
                TimeoutSeconds = 2
            };
            var controller = CreateController(options);
            var stopwatch = Stopwatch.StartNew();

            // Act / Assert
            Assert.Throws<OperationCanceledException>(() => controller.Liquify(templateCode));
            stopwatch.Stop();

            Assert.That(stopwatch.Elapsed, Is.LessThan(TimeSpan.FromSeconds(5)),
                "Rendering should be cancelled well before it is allowed to run for an excessive amount of time.");
        }

        [Test]
        public void LiquidRenderingOptions_DefaultValues_AreMandatoryAndPositive()
        {
            var options = new LiquidRenderingOptions();

            Assert.That(options.MaxIterations, Is.GreaterThan(0));
            Assert.That(options.TimeoutSeconds, Is.GreaterThan(0));
        }

        /// <summary>
        /// A MaxIterations value of 0 means "unlimited" iterations are allowed, so a moderately
        /// sized loop should render successfully instead of throwing.
        /// </summary>
        [Test]
        public void LiquifyAction_WhenMaxIterationsIsZero_AllowsUnlimitedIterations()
        {
            // Arrange
            const string templateCode = @"{% for i in (1..10000) %}{{ i }}{% endfor %}";
            var options = new LiquidRenderingOptions
            {
                MaxIterations = 0, // unlimited
                TimeoutSeconds = 60
            };
            var controller = CreateController(options);

            // Act / Assert
            Assert.DoesNotThrow(() => controller.Liquify(templateCode));
        }

        /// <summary>
        /// A TimeoutSeconds value of 0 means rendering is never cancelled due to a timeout, so a
        /// quick template should render successfully instead of throwing.
        /// </summary>
        [Test]
        public void LiquifyAction_WhenTimeoutSecondsIsZero_AllowsUnlimitedTime()
        {
            // Arrange
            const string templateCode = @"{% for i in (1..1000) %}{{ i }}{% endfor %}";
            var options = new LiquidRenderingOptions
            {
                MaxIterations = 0,
                TimeoutSeconds = 0 // unlimited
            };
            var controller = CreateController(options);

            // Act / Assert
            Assert.DoesNotThrow(() => controller.Liquify(templateCode));
        }
    }
}
