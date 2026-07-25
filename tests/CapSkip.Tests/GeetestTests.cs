using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> returning a realistic GeeTest v3 answer — a JSON
    /// string carried in <c>request</c>.
    /// </summary>
    public sealed class MockGeetestApiClient : ApiClient
    {
        public const string GtChallenge = "7cf6a8b1a2c34d5e6f7089abcdef0123";
        public const string GtValidate = "9b1f4a2c8e7d6b5a4938271605f4e3d2";
        public const string GtSeccode = "9b1f4a2c8e7d6b5a4938271605f4e3d2|jordan";

        public static readonly string SolutionJson = JsonSerializer.Serialize(
            new Dictionary<string, string>
            {
                ["geetest_challenge"] = GtChallenge,
                ["geetest_validate"] = GtValidate,
                ["geetest_seccode"] = GtSeccode,
            });

        private readonly string request;

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        public MockGeetestApiClient(string? request = null)
            : base("mock", 0)
        {
            this.request = request ?? SolutionJson;
        }

        public override Task<string> InAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
        {
            var fields = new Dictionary<string, object?>(options);
            fields.Remove("files");
            Incomings = fields;
            return Task.FromResult("OK|123");
        }

        public override Task<string> ResAsync(
            IDictionary<string, object?> query,
            CancellationToken cancellationToken = default)
        {
            if (query.TryGetValue("json", out var json) && (Equals(json, 1) || Equals(json, "1")))
            {
                var payload = JsonSerializer.Serialize(
                    new Dictionary<string, object> { ["status"] = 1, ["request"] = request });
                return Task.FromResult(payload);
            }

            return Task.FromResult($"OK|{request}");
        }
    }

    public class GeetestTests
    {
        private const string Gt = "81388ea1fc187e0c335c0a8907ff2625";
        private const string Challenge = "7cf6a8b1a2c34d5e6f7089abcdef0123";
        private const string Url = "https://mysite.com/page/with/geetest";

        private static (CapSkipClient Solver, MockGeetestApiClient Api) Make(string? request = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            var api = new MockGeetestApiClient(request);
            solver.ApiClient = api;
            return (solver, api);
        }

        private static void AssertSent(MockGeetestApiClient api, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            MockSolver.AssertDictEqual(want, api.Incomings);
        }

        [Fact]
        public async Task Basic()
        {
            var (solver, api) = Make();

            var result = await solver.GeetestAsync(Gt, Challenge, Url);

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "geetest",
                ["gt"] = Gt,
                ["challenge"] = Challenge,
                ["pageurl"] = Url,
            });
            Assert.Equal("123", result.CaptchaId);
        }

        [Fact]
        public async Task ApiServer()
        {
            var (solver, api) = Make();

            await solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
            {
                ["api_server"] = "api-na.geetest.com",
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "geetest",
                ["gt"] = Gt,
                ["challenge"] = Challenge,
                ["pageurl"] = Url,
                ["api_server"] = "api-na.geetest.com",
            });
        }

        [Fact]
        public async Task ApiServerCamelCaseAlias()
        {
            var (solver, api) = Make();

            await solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
            {
                ["apiServer"] = "api-na.geetest.com",
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "geetest",
                ["gt"] = Gt,
                ["challenge"] = Challenge,
                ["pageurl"] = Url,
                ["api_server"] = "api-na.geetest.com",
            });
        }

        [Fact]
        public async Task Proxy()
        {
            var (solver, api) = Make();

            await solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
            {
                ["proxy"] = new Dictionary<string, object?> { ["type"] = "HTTP", ["uri"] = "1.2.3.4:3128" },
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "geetest",
                ["gt"] = Gt,
                ["challenge"] = Challenge,
                ["pageurl"] = Url,
                ["proxy"] = "1.2.3.4:3128",
                ["proxytype"] = "HTTP",
            });
        }

        [Fact]
        public async Task KeepsRawJsonInCode()
        {
            // Kept verbatim so callers can forward it to code written against
            // another solver's API.
            var (solver, _) = Make();

            var result = await solver.GeetestAsync(Gt, Challenge, Url);

            Assert.Equal(MockGeetestApiClient.SolutionJson, result.Code);
        }

        [Fact]
        public async Task ExpandsSolutionFields()
        {
            var (solver, _) = Make();

            var result = await solver.GeetestAsync(Gt, Challenge, Url);

            Assert.Equal(MockGeetestApiClient.GtChallenge, result.Challenge);
            Assert.Equal(MockGeetestApiClient.GtValidate, result.Validate);
            Assert.Equal(MockGeetestApiClient.GtSeccode, result.Seccode);
        }

        [Fact]
        public async Task NonJsonCodeIsLeftAlone()
        {
            var (solver, _) = Make("not-json");

            var result = await solver.GeetestAsync(Gt, Challenge, Url);

            Assert.Equal("not-json", result.Code);
            Assert.Null(result.Validate);
        }

        [Fact]
        public async Task MissingChallengeIsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync(Gt, "", Url));
        }

        [Fact]
        public async Task MissingGtIsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync("", Challenge, Url));
        }

        [Fact]
        public async Task MissingPageurlIsRejected()
        {
            // pageurl is documented as required; fail locally rather than paying a
            // round-trip for ERROR_PAGEURL.
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync(Gt, Challenge, ""));
        }

        [Fact]
        public async Task UnsupportedParameterIsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
                {
                    ["sitekey"] = "not-a-geetest-param",
                }));
        }

        [Theory]
        [InlineData("HTTP")]
        [InlineData("HTTPS")]
        [InlineData("SOCKS5")]
        [InlineData("SOCKS5H")]
        [InlineData("socks5h")]
        public async Task AcceptedProxyTypes(string proxyType)
        {
            var (solver, api) = Make();

            await solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
            {
                ["proxy"] = new Dictionary<string, object?> { ["type"] = proxyType, ["uri"] = "1.2.3.4:3128" },
            });

            Assert.Equal(proxyType, api.Incomings["proxytype"]);
        }

        [Fact]
        public async Task Socks4IsRejected()
        {
            // CapSkip maps only HTTP/HTTPS/SOCKS5/SOCKS5H and answers
            // ERROR_BAD_PARAMETERS for SOCKS4, so fail before the round-trip.
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
                {
                    ["proxy"] = new Dictionary<string, object?> { ["type"] = "SOCKS4", ["uri"] = "1.2.3.4:3128" },
                }));
        }

        [Fact]
        public async Task UnknownProxyTypeIsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.GeetestAsync(Gt, Challenge, Url, new Dictionary<string, object?>
                {
                    ["proxy"] = "1.2.3.4:3128",
                    ["proxytype"] = "FTP",
                }));
        }
    }
}
