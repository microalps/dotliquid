using DotLiquid.Exceptions;
using DotLiquid.Tests.Model;
using DotLiquid.Tests.Util;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;

namespace DotLiquid.Tests
{
    public class GoldenLiquidTests
    {
        #region Static Variables For Test Cases
        private static GoldenLiquidRules rules;

        internal static GoldenLiquidRules Rules
        {
            get
            {
                if (rules == null)
                    rules = DeserializeResource<GoldenLiquidRules>("DotLiquid.Tests.Embedded.golden_rules.json");

                return rules;
            }
        }

        public static List<GoldenLiquidTest> GetGoldenTests(bool passing)
        {
            var tests = new List<GoldenLiquidTest>();
            var goldenLiquid = DeserializeResource<GoldenLiquid>("DotLiquid.Tests.Embedded.golden_liquid.json");

            // Iterate through the tests
            foreach (var testGroup in goldenLiquid.TestGroups)
            {
                if (Rules.SkippedGroups.Contains(testGroup.Name))
                    continue;

                foreach (var test in testGroup.Tests)
                {
                    test.GroupName = testGroup.Name;
                    var uniqueName = test.UniqueName;
                    if (Rules.AlternateTestExpectations.ContainsKey(uniqueName))
                        test.Want = Rules.AlternateTestExpectations[uniqueName];

                    if (Rules.FailingTests.Contains(uniqueName) != passing)
                        tests.Add(test);
                }
            }

            return tests;
        }

        public static IEnumerable<GoldenLiquidTest> GoldenTestsPassing => GetGoldenTests(passing: true);
        public static IEnumerable<GoldenLiquidTest> GoldenTestsFailing => GetGoldenTests(passing: false);

        private static T DeserializeResource<T>(string resourceName)
        {
            // Load the JSON content
#if NETCOREAPP1_0
            var assembly = typeof(GoldenLiquidTests).GetTypeInfo().Assembly;
#else
            var assembly = Assembly.GetExecutingAssembly();
#endif

            var jsonContent = string.Empty;
            using (Stream stream = assembly.GetManifestResourceStream(resourceName))
            using (StreamReader reader = new StreamReader(stream))
            {
                jsonContent = reader.ReadToEnd();
            }

            // Deserialize the JSON content
            return JsonConvert.DeserializeObject<T>(jsonContent);
        }
        #endregion

        [Test]
        [TestCaseSource(nameof(GoldenTestsPassing))]
        public void ExecuteGoldenLiquidTests(GoldenLiquidTest test)
        {
            // Create a new Hash object to represent the context
            var context = new Hash();
            foreach (var pair in test.Context)
            {
                context[pair.Key] = pair.Value;
            }

            var syntax = SyntaxCompatibility.DotLiquid22a;
            var parameters = new RenderParameters(CultureInfo.CurrentCulture)
            {
                SyntaxCompatibilityLevel = syntax,
                LocalVariables = context,
                ErrorsOutputMode = test.Error ? ErrorsOutputMode.Rethrow : ErrorsOutputMode.Display
            };

            Helper.LockTemplateStaticVars(Template.NamingConvention, () =>
            {
                Liquid.UseRubyDateFormat = true;
                if (test.Partials?.Count > 0)
                {
                    Template.FileSystem = new DictionaryFileSystem(test.Partials);
                }

                // If the test should produce an error, assert that it does
                if (test.Error)
                {
                    Assert.Catch<LiquidException>(() => Template.Parse(test.Template, syntax).Render(parameters), test.UniqueName);
                }
                else
                {
                    Assert.That(Template.Parse(test.Template, syntax).Render(parameters).Replace("\r\n", "\n"), Is.EqualTo(test.Want), test.UniqueName);
                }
            });
        }

        [Test]
        [TestCaseSource(nameof(GoldenTestsFailing))]
        public void ExecuteGoldenLiquidFailingTests(GoldenLiquidTest test)
        {
            Assert.Throws<AssertionException>(() => ExecuteGoldenLiquidTests(test), test.UniqueName);
        }

    }
}
