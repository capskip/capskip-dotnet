using System.Collections.Generic;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    public class TurnstileTests
    {
        private const string SiteKey = "0x4AAAAAAABUYP0XeMJF0xoy";
        private const string Url = "https://mysite.com/page/with/turnstile";

        [Fact]
        public async Task Basic()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.TurnstileAsync(SiteKey, Url);

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "turnstile",
                ["sitekey"] = SiteKey,
                ["pageurl"] = Url,
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task ChallengePage()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.TurnstileAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["action"] = "managed",
                ["data"] = "cdata_value",
                ["pagedata"] = "chlpagedata_value",
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "turnstile",
                ["sitekey"] = SiteKey,
                ["pageurl"] = Url,
                ["action"] = "managed",
                ["data"] = "cdata_value",
                ["pagedata"] = "chlpagedata_value",
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task Proxy()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.TurnstileAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["proxy"] = new Dictionary<string, object?> { ["type"] = "HTTP", ["uri"] = "1.2.3.4:3128" },
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "turnstile",
                ["sitekey"] = SiteKey,
                ["pageurl"] = Url,
                ["proxy"] = "1.2.3.4:3128",
                ["proxytype"] = "HTTP",
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task ReturnsUserAgent()
        {
            var (solver, _) = MockSolver.Make();

            var result = await solver.TurnstileAsync(SiteKey, Url);

            Assert.Equal(MockApiClient.Code, result.Code);
            Assert.Equal("TestAgent/1.0", result.UserAgent);
        }
    }
}
