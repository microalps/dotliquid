using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace DotLiquid.Website.Controllers
{
    public class TryOnlineController : Controller
    {
        private readonly LiquidRenderingOptions _options;

        public TryOnlineController(IOptions<LiquidRenderingOptions> options)
        {
            _options = options?.Value ?? new LiquidRenderingOptions();
        }

        public ActionResult Index()
        {
            const string templateCode = @"&lt;p&gt;{{ user.name | upcase }} has to do:&lt;/p&gt;

&lt;ul&gt;
{% for item in user.tasks -%}
  &lt;li&gt;{{ item.name }}&lt;/li&gt;
{% endfor -%}
&lt;/ul&gt;";

            string result = LiquifyInternal(templateCode);

            ViewData["TemplateCode"] = templateCode;
            ViewData["Result"] = result;

            return View();
        }

        [HttpPost]
        public ActionResult Liquify(string templateCode)
        {
            string result = LiquifyInternal(templateCode);

            return new ContentResult
            {
                Content = result
            };
        }

        private string LiquifyInternal(string templateCode)
        {
            Template template = Template.Parse(templateCode);

            // Templates are submitted by anonymous users, so guard against excessively
            // expensive templates (e.g. nested loops over large ranges) by both limiting the
            // maximum number of loop iterations and cancelling rendering after a fixed timeout,
            // instead of letting it run unbounded. A TimeoutSeconds value of 0 means unlimited.
            var timeout = _options.TimeoutSeconds > 0
                ? TimeSpan.FromSeconds(_options.TimeoutSeconds)
                : Timeout.InfiniteTimeSpan;
            using (var cancellationTokenSource = new CancellationTokenSource(timeout))
            {
                var context = new Context(
                    environments: new List<Hash>
                    {
                        Hash.FromAnonymousObject(new
                        {
                            user = new User
                            {
                                Name = "Tim Jones",
                                Tasks = new List<Task>
                                {
                                    new Task { Name = "Documentation" },
                                    new Task { Name = "Code comments" }
                                }
                            }
                        })
                    },
                    outerScope: new Hash(),
                    registers: new Hash(),
                    errorsOutputMode: ErrorsOutputMode.Display,
                    maxIterations: _options.MaxIterations,
                    formatProvider: CultureInfo.InvariantCulture,
                    cancellationToken: cancellationTokenSource.Token);

                return template.Render(RenderParameters.FromContext(context, CultureInfo.InvariantCulture));
            }
        }
    }

    public class User : Drop
    {
        public string Name { get; set; }
        public List<Task> Tasks { get; set; }
    }

    public class Task : Drop
    {
        public string Name { get; set; }
    }
}
