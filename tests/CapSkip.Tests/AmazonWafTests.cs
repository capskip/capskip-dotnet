using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;
using TimeoutException = CapSkip.TimeoutException;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> returning a realistic AWS WAF answer — an object
    /// rather than a token. json=1 puts it straight into <c>request</c>, with no
    /// createTask-shaped <c>solution</c>; plain text sends it as one line of JSON
    /// after <c>OK|</c>.
    /// </summary>
    public sealed class MockAmazonWafApiClient : ApiClient
    {
        public const string CaptchaVoucher = "eyJ2b3VjaGVyIjoiRVhBTVBMRSJ9.9QwZcEXAMPLEvoucherJk4LmN0pQ";
        public const string ExistingToken = "7d4c2a91-EXAMPLE-4f0b-9e6d-3a1b5c8e2f70:EQoAeXAMPLEtokenAAAA:Wm9wc2E=";

        /// <summary>
        /// What CapSkip hands back: not a token, but the pair AWS WAF trades for the
        /// aws-waf-token cookie on its own voucher endpoint.
        /// </summary>
        public static readonly string SolutionJson =
            "{\"captcha_voucher\":\"" + CaptchaVoucher + "\",\"existing_token\":\"" + ExistingToken + "\"}";

        private readonly string? _request;

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        /// <param name="request">
        /// Overrides the raw JSON of <c>request</c> in a json=1 reply, to test an
        /// answer the SDK cannot read.
        /// </param>
        public MockAmazonWafApiClient(string? request = null)
            : base("mock", 0)
        {
            _request = request;
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
                return Task.FromResult("{\"status\":1,\"request\":" + (_request ?? SolutionJson) + "}");
            }

            return Task.FromResult("OK|" + SolutionJson);
        }
    }

    public class AmazonWafTests
    {
        private const string Url = "https://www.example.com/";
        private const string Sitekey =
            "AQIDAHjcYuEXAMPLEghicBgQ/7bFaQZ+m5FKCMDnO+vTbNg96AHDh0IR5vgzHNceHYqZR+GOAAAAfjB8";
        private const string Iv = "CgAFRDIjfgAAA5yY";
        private const string Context =
            "Rbp3r8fvbuhmrCqpUe8Mh1TLvs8OSKJZiSs2vCF4dt9rxVZI7xYJa2n5k/9r+XaXe7KUIu2I8nXTDHHkDvIOTDc2";
        private const string ChallengeJs =
            "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/challenge.js";
        private const string CaptchaJs =
            "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/captcha.js";
        private const string WidgetKey = "Xr9kEXAMPLE2bWQx1vTq8LmZ0aYc4dHs7nFp3gJt6uKw5eRi9AbCdEfGh=_0_1";
        private const string JsapiJs =
            "https://5b7e0a19c3d8.us-east-1.captcha-sdk.awswaf.com/5b7e0a19c3d8/jsapi.js";

        private static CapSkipClient Make(out MockAmazonWafApiClient client, string? request = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockAmazonWafApiClient(request);
            solver.ApiClient = client;
            return solver;
        }

        /// <summary>A challenge-page solve: sitekey, iv and context, plus any extra options.</summary>
        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? extra = null)
        {
            var options = new Dictionary<string, object?> { ["iv"] = Iv, ["context"] = Context };
            if (extra != null)
            {
                foreach (var kv in extra)
                {
                    options[kv.Key] = kv.Value;
                }
            }

            return solver.AmazonWafAsync(Sitekey, Url, options);
        }

        private static void AssertSent(MockAmazonWafApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            MockSolver.AssertDictEqual(want, client.Incomings);
        }

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_a_challenge_page()
        {
            var solver = Make(out var client);

            var result = await Solve(solver, new Dictionary<string, object?>
            {
                ["challenge_script"] = ChallengeJs,
                ["captcha_script"] = CaptchaJs,
            });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "amazon_waf",
                ["sitekey"] = Sitekey,
                ["iv"] = Iv,
                ["context"] = Context,
                ["pageurl"] = Url,
                ["challenge_script"] = ChallengeJs,
                ["captcha_script"] = CaptchaJs,
            });
            Assert.Equal("123", result.CaptchaId);
        }

        /// <summary>
        /// Left out, CapSkip reads them from pageurl at solve time — and an unset one
        /// must not reach the server at all.
        /// </summary>
        [Fact]
        public async Task Iv_and_context_may_be_left_out()
        {
            var solver = Make(out var client);

            await solver.AmazonWafAsync(Sitekey, Url, new Dictionary<string, object?>
            {
                ["iv"] = null,
                ["context"] = null,
            });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "amazon_waf",
                ["sitekey"] = Sitekey,
                ["pageurl"] = Url,
            });
        }

        /// <summary>The server treats "" exactly like an absent value.</summary>
        [Fact]
        public async Task Empty_optionals_mean_not_sent()
        {
            var solver = Make(out var client);

            await solver.AmazonWafAsync(Sitekey, Url, new Dictionary<string, object?>
            {
                ["iv"] = "",
                ["context"] = "",
                ["challenge_script"] = "",
                ["jsapiScript"] = "",
            });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "amazon_waf",
                ["sitekey"] = Sitekey,
                ["pageurl"] = Url,
            });
        }

        [Fact]
        public async Task Submits_a_widget_page()
        {
            var solver = Make(out var client);

            await solver.AmazonWafAsync(
                WidgetKey, Url, new Dictionary<string, object?> { ["jsapiScript"] = JsapiJs });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "amazon_waf",
                ["sitekey"] = WidgetKey,
                ["pageurl"] = Url,
                ["jsapiScript"] = JsapiJs,
            });
        }

        /// <summary>
        /// createTask callers arrive with challengeScript / captchaScript, and
        /// jsapi_script is the snake-case spelling of the documented jsapiScript.
        /// </summary>
        [Fact]
        public async Task Accepts_the_script_aliases()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["challengeScript"] = ChallengeJs,
                ["captchaScript"] = CaptchaJs,
            });
            Assert.Equal(ChallengeJs, client.Incomings["challenge_script"]);
            Assert.Equal(CaptchaJs, client.Incomings["captcha_script"]);
            Assert.False(client.Incomings.ContainsKey("challengeScript"));
            Assert.False(client.Incomings.ContainsKey("captchaScript"));

            await solver.AmazonWafAsync(
                WidgetKey, Url, new Dictionary<string, object?> { ["jsapi_script"] = JsapiJs });
            Assert.Equal(JsapiJs, client.Incomings["jsapiScript"]);
            Assert.False(client.Incomings.ContainsKey("jsapi_script"));
        }

        [Fact]
        public async Task Refuses_an_alias_that_conflicts_with_the_wire_name()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => Solve(solver, new Dictionary<string, object?>
            {
                ["challengeScript"] = ChallengeJs,
                ["challenge_script"] = ChallengeJs + "?v=2",
            }));
        }

        [Fact]
        public async Task Splits_a_proxy_into_proxy_and_proxytype()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy { Type = "SOCKS5H", Uri = "login:pass@1.2.3.4:8080" },
            });

            Assert.Equal("login:pass@1.2.3.4:8080", client.Incomings["proxy"]);
            Assert.Equal("SOCKS5H", client.Incomings["proxytype"]);
        }

        /// <summary>
        /// A browser mints the token the voucher belongs to, so the solve gets
        /// RecaptchaTimeout like the other browser-backed methods. The timeout never
        /// reaches the wire, so assert on the effect: a client whose reCAPTCHA budget
        /// has elapsed gives up, even though the much longer default has not.
        /// </summary>
        [Fact]
        public async Task Uses_the_recaptcha_timeout()
        {
            var solver = new CapSkipClient(
                apiKey: "API_KEY", defaultTimeout: 600, recaptchaTimeout: 0.05, pollingInterval: 0.01);
            solver.ApiClient = new NeverReadyApiClient();

            var solve = Assert.ThrowsAsync<TimeoutException>(() => Solve(solver));
            var finished = await Task.WhenAny(solve, Task.Delay(TimeSpan.FromSeconds(30)));

            Assert.Same(solve, finished);
            await solve;
        }

        [Fact]
        public async Task An_explicit_timeout_wins()
        {
            var solver = new CapSkipClient(
                apiKey: "API_KEY", defaultTimeout: 600, recaptchaTimeout: 600, pollingInterval: 0.01);
            solver.ApiClient = new NeverReadyApiClient();

            var solve = Assert.ThrowsAsync<TimeoutException>(
                () => Solve(solver, new Dictionary<string, object?> { ["timeout"] = 0.05 }));
            var finished = await Task.WhenAny(solve, Task.Delay(TimeSpan.FromSeconds(30)));

            Assert.Same(solve, finished);
            await solve;
        }

        // -- what is refused before it costs a round-trip -----------------

        [Fact]
        public async Task Refuses_a_missing_sitekey_or_pageurl()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync("", Url));
            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync(Sitekey, ""));
            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync(Sitekey, null!));
        }

        [Theory]
        [InlineData("ftp://example.com/")]
        [InlineData("example.com")]
        [InlineData("/relative/path")]
        public async Task Refuses_a_pageurl_that_is_not_http(string url)
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync(Sitekey, url));
        }

        [Fact]
        public async Task Refuses_iv_without_context()
        {
            var solver = Make(out _);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["context"] = null }));

            Assert.Contains("context", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Refuses_context_without_iv()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["iv"] = "" }));
        }

        public static IEnumerable<object[]> OffAwsScripts()
        {
            var bad = new[]
            {
                "https://evil.example.com/abc/challenge.js",
                "http://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/challenge.js",
                "https://awswaf.com.evil.example/3f2a9c7d41b0/challenge.js",
                "https://user:pw@3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/challenge.js",
                "https://3f2a9c7d41b0.edge.sdk.awswaf.com:8443/3f2a9c7d41b0/challenge.js",
                "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/other.js",
                "https://3f2a9c7d41b0.edge.sdk.awswaf.com/challenge.js",
                "not a url",
            };

            foreach (var url in bad)
            {
                foreach (var key in new[] { "challenge_script", "captcha_script", "jsapiScript" })
                {
                    yield return new object[] { key, url };
                }
            }
        }

        /// <summary>
        /// CapSkip POSTs the solution to the host the script names, so it refuses any
        /// script that is not on awswaf.com.
        /// </summary>
        [Theory]
        [MemberData(nameof(OffAwsScripts))]
        public async Task Refuses_a_script_that_is_not_on_awswaf(string key, string url)
        {
            var solver = Make(out _);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { [key] = url }));

            Assert.Contains("awswaf.com", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Accepts_a_script_with_a_query_string()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["challenge_script"] = ChallengeJs + "?v=2" });

            Assert.Equal(ChallengeJs + "?v=2", client.Incomings["challenge_script"]);
        }

        [Fact]
        public async Task Refuses_an_unknown_parameter()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["gt"] = "x" }));
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
        public async Task Expands_the_answer_into_the_pair()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockAmazonWafApiClient.CaptchaVoucher, result.CaptchaVoucher);
            Assert.Equal(MockAmazonWafApiClient.ExistingToken, result.ExistingToken);
        }

        /// <summary>
        /// Code is typed as a string, so the object answer is re-serialized rather
        /// than ToString()'d into its type name — it has to round-trip as JSON, and
        /// carry the same text a plain-text poll delivers after OK|.
        /// </summary>
        [Fact]
        public async Task Keeps_the_raw_answer_as_json_in_code()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockAmazonWafApiClient.SolutionJson, result.Code);
            using var parsed = JsonDocument.Parse(result.Code);
            Assert.Equal(
                MockAmazonWafApiClient.ExistingToken,
                parsed.RootElement.GetProperty("existing_token").GetString());
        }

        /// <summary>
        /// AWS WAF tokens are base64, so a + must survive re-serialization as itself
        /// rather than as +.
        /// </summary>
        [Fact]
        public async Task Keeps_a_plus_in_the_raw_answer_as_it_is()
        {
            const string json = "{\"captcha_voucher\":\"a+b/c=\",\"existing_token\":\"d+e:f+g:h==\"}";
            var solver = Make(out _, request: json);

            var result = await Solve(solver);

            Assert.Equal(json, result.Code);
            Assert.Equal("a+b/c=", result.CaptchaVoucher);
            Assert.Equal("d+e:f+g:h==", result.ExistingToken);
        }

        /// <summary>Plain-text res.php sends the same object as one line after OK|.</summary>
        [Fact]
        public void Expands_a_json_string_answer_too()
        {
            var result = ResponseParsing.ApplyAmazonWafSolution(
                new SolveResult { CaptchaId = "123", Code = MockAmazonWafApiClient.SolutionJson });

            Assert.Equal(MockAmazonWafApiClient.CaptchaVoucher, result.CaptchaVoucher);
            Assert.Equal(MockAmazonWafApiClient.ExistingToken, result.ExistingToken);
        }

        /// <summary>
        /// A reply the SDK cannot read must reach the caller as the server sent it,
        /// rather than being masked by a parse failure.
        /// </summary>
        [Fact]
        public async Task Leaves_an_unparseable_answer_untouched()
        {
            var solver = Make(out _, request: "\"not-json-at-all\"");

            var result = await Solve(solver);

            Assert.Equal("not-json-at-all", result.Code);
            Assert.Null(result.CaptchaVoucher);
            Assert.Null(result.ExistingToken);
        }

        [Fact]
        public void Does_not_touch_the_capy_fields()
        {
            var result = ResponseParsing.ApplyAmazonWafSolution(
                new SolveResult { CaptchaId = "123", Code = MockAmazonWafApiClient.SolutionJson });

            Assert.Null(result.CaptchaKey);
            Assert.Null(result.Answer);
            Assert.Null(result.Token);
        }

        // -- the script rule on its own -----------------------------------

        [Theory]
        [InlineData(ChallengeJs)]
        [InlineData(CaptchaJs)]
        [InlineData(JsapiJs)]
        [InlineData("https://3f2a9c7d41b0.edge.sdk.awswaf.com:443/3f2a9c7d41b0/challenge.js")]
        [InlineData("https://ABC.EDGE.SDK.AWSWAF.COM/a_b-c/challenge.js?x=1")]
        public void Script_rule_accepts_genuine_aws_waf_scripts(string url)
        {
            Assert.True(ApiParams.IsAmazonWafScriptUrl(url));
        }

        [Theory]
        [InlineData("https://awswaf.com/abc/challenge.js")]
        [InlineData("https://x.awswaf.com/a.b/challenge.js")]
        [InlineData("https://x.awswaf.com/abc/Challenge.js")]
        [InlineData("")]
        public void Script_rule_refuses_near_misses(string url)
        {
            Assert.False(ApiParams.IsAmazonWafScriptUrl(url));
        }
    }
}
