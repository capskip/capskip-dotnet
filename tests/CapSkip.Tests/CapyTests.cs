using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> returning a realistic Capy answer — an object
    /// rather than a token, carried in <c>request</c> and in the createTask-shaped
    /// solution object both.
    /// </summary>
    public sealed class MockCapyApiClient : ApiClient
    {
        public const string CaptchaKey = "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v";
        public const string ChallengeKey = "BalY2gJaI8uA2SGVOZhqBQ3V0CYSNNGP";
        public const string Answer = "0xax8ex0xax84x0xkx7qx0x18x76x0x1ix6sx0x26x68x0x2gx5kx0x34x50x";

        /// <summary>
        /// The three values the Capy widget would have written into the target form,
        /// plus respKey for shape-compatibility with 2Captcha's documented response.
        /// </summary>
        public static readonly Dictionary<string, object> Solution = new Dictionary<string, object>
        {
            ["captchakey"] = CaptchaKey,
            ["challengekey"] = ChallengeKey,
            ["answer"] = Answer,
            ["respKey"] = "",
        };

        public static readonly string SolutionJson = JsonSerializer.Serialize(Solution);

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        public MockCapyApiClient()
            : base("mock", 0)
        {
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
                return Task.FromResult(
                    "{\"status\":1,\"request\":" + SolutionJson + ",\"solution\":" + SolutionJson + "}");
            }

            return Task.FromResult("OK|" + SolutionJson);
        }
    }

    public class CapyTests
    {
        private const string Url = "https://mysite.com/login";
        private const string CaptchaKey = MockCapyApiClient.CaptchaKey;

        private static CapSkipClient Make(out MockCapyApiClient client)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockCapyApiClient();
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.CapyAsync(CaptchaKey, Url, options);

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_the_documented_parameters()
        {
            var solver = Make(out var client);

            var result = await Solve(solver);

            Assert.Equal("capy", client.Incomings["method"]);
            Assert.Equal(CaptchaKey, client.Incomings["captchakey"]);
            Assert.Equal(Url, client.Incomings["pageurl"]);
            Assert.Equal("API_KEY", client.Incomings["key"]);
            Assert.Equal("123", result.CaptchaId);
        }

        /// <summary>
        /// The SDK argument is the site key, matching every other method; the wire
        /// name is <c>captchakey</c>, which is what the API documents.
        /// </summary>
        [Fact]
        public async Task Sends_its_sitekey_argument_as_captchakey()
        {
            var solver = Make(out var client);

            await Solve(solver);

            Assert.Equal(CaptchaKey, client.Incomings["captchakey"]);
            Assert.False(client.Incomings.ContainsKey("sitekey"));
        }

        [Fact]
        public async Task Forwards_api_server_and_its_camelCase_alias()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["api_server"] = "https://jp.api.capy.me/" });
            Assert.Equal("https://jp.api.capy.me/", client.Incomings["api_server"]);

            await Solve(solver, new Dictionary<string, object?> { ["apiServer"] = "https://jp.api.capy.me/" });
            Assert.Equal("https://jp.api.capy.me/", client.Incomings["api_server"]);
        }

        /// <summary>
        /// The server reads both spellings, so the SDK settles on one rather than
        /// passing through whichever the caller happened to use.
        /// </summary>
        [Fact]
        public async Task Lowercases_userAgent_to_the_documented_spelling()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = "Mozilla/5.0" });

            Assert.Equal("Mozilla/5.0", client.Incomings["useragent"]);
            Assert.False(client.Incomings.ContainsKey("userAgent"));
        }

        [Fact]
        public async Task Accepts_an_explicit_puzzle_version()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["version"] = "puzzle" });

            Assert.Equal("puzzle", client.Incomings["version"]);
        }

        /// <summary>
        /// A form body can only carry strings, so a null default would otherwise
        /// reach the server stringified.
        /// </summary>
        [Fact]
        public async Task Does_not_send_an_unset_optional()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["api_server"] = null,
                ["version"] = null,
            });

            Assert.False(client.Incomings.ContainsKey("api_server"));
            Assert.False(client.Incomings.ContainsKey("version"));
        }

        [Fact]
        public async Task Splits_a_proxy_into_proxy_and_proxytype()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy { Type = "SOCKS5", Uri = "login:pass@1.2.3.4:8080" },
            });

            Assert.Equal("login:pass@1.2.3.4:8080", client.Incomings["proxy"]);
            Assert.Equal("SOCKS5", client.Incomings["proxytype"]);
        }

        // -- what is refused before it costs a round-trip -----------------

        /// <summary>
        /// CapSkip solves the puzzle family only. Returning a puzzle answer for an
        /// avatar request would bill for a solve the target site rejects.
        /// </summary>
        [Fact]
        public async Task Refuses_the_avatar_version_locally()
        {
            var solver = Make(out _);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["version"] = "avatar" }));

            Assert.Contains("puzzle", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Refuses_an_unknown_version()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["version"] = "slider" }));
        }

        [Fact]
        public async Task Refuses_a_missing_captchakey_or_pageurl()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => solver.CapyAsync("", Url));
            await Assert.ThrowsAsync<ValidationException>(() => solver.CapyAsync(CaptchaKey, ""));
        }

        [Fact]
        public async Task Refuses_an_unknown_parameter()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["challenge"] = "x" }));
        }

        [Fact]
        public async Task Refuses_socks4_rather_than_silently_going_direct()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?>
                {
                    ["proxy"] = new Proxy { Type = "SOCKS4", Uri = "1.2.3.4:1080" },
                }));
        }

        // -- what comes back ----------------------------------------------

        [Fact]
        public async Task Expands_the_answer_into_the_three_form_fields()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockCapyApiClient.CaptchaKey, result.CaptchaKey);
            Assert.Equal(MockCapyApiClient.ChallengeKey, result.ChallengeKey);
            Assert.Equal(MockCapyApiClient.Answer, result.Answer);
            Assert.Equal("", result.RespKey);
        }

        /// <summary>
        /// The answer is the drag path the widget would have recorded, and the target
        /// site verifies it against the challenge it issued.
        /// </summary>
        [Fact]
        public async Task Passes_the_answer_through_unchanged()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockCapyApiClient.Answer, result.Answer);
            Assert.DoesNotContain(" ", result.Answer!, StringComparison.Ordinal);
        }

        /// <summary>
        /// Code is typed as a string, so the object answer is re-serialized rather
        /// than ToString()'d into its type name — it has to round-trip as JSON.
        /// </summary>
        [Fact]
        public async Task Keeps_the_raw_answer_as_json_in_code()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            using var parsed = JsonDocument.Parse(result.Code);
            Assert.Equal(
                MockCapyApiClient.Answer,
                parsed.RootElement.GetProperty("answer").GetString());
            Assert.Equal(
                MockCapyApiClient.ChallengeKey,
                parsed.RootElement.GetProperty("challengekey").GetString());
        }

        /// <summary>
        /// A reply the SDK cannot read must reach the caller as the server sent it,
        /// rather than being masked by a parse failure.
        /// </summary>
        [Fact]
        public void Leaves_an_unparseable_answer_untouched()
        {
            var result = ResponseParsing.ApplyCapySolution(
                new SolveResult { CaptchaId = "123", Code = "not-json-at-all" });

            Assert.Equal("not-json-at-all", result.Code);
            Assert.Null(result.Answer);
        }
    }
}
