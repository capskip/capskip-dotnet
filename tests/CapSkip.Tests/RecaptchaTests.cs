using System.Collections.Generic;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    public class RecaptchaTests
    {
        private const string SiteKey = "6Le-wvkSVVABCPBMRTvw0Q4Muexq1bi0DJwx_mJ-";
        private const string Url = "https://mysite.com/page/with/recaptcha";

        [Fact]
        public async Task V2()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["invisible"] = 1,
                ["datas"] = "Crb7VsRAQaBqoaQQtHQQ",
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
                ["invisible"] = 1,
                ["enterprise"] = 0,
                ["data-s"] = "Crb7VsRAQaBqoaQQtHQQ",
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task V2RejectsV3Action()
        {
            var (solver, _) = MockSolver.Make();
            await Assert.ThrowsAnyAsync<CapSkipError>(() =>
                solver.RecaptchaAsync(SiteKey, "https://example.com", new Dictionary<string, object?>
                {
                    ["action"] = "verify",
                }));
        }

        [Fact]
        public async Task V2Enterprise()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["enterprise"] = 1,
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
                ["enterprise"] = 1,
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task V3()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["action"] = "verify",
                ["version"] = "v3",
                ["score"] = 0.7,
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
                ["enterprise"] = 0,
                ["action"] = "verify",
                ["version"] = "v3",
                ["min_score"] = 0.7,
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task Proxy()
        {
            var (solver, api) = MockSolver.Make();

            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy("HTTPS", "login:password@1.2.3.4:3128"),
            });

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
                ["enterprise"] = 0,
                ["proxy"] = "login:password@1.2.3.4:3128",
                ["proxytype"] = "HTTPS",
            });
            MockSolver.AssertResult(result);
        }
    }
}
