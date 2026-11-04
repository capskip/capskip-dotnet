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
    /// Mock <see cref="ApiClient"/> answering as CapSkip's res.php does for hCaptcha
    /// with json=1: <c>{status, request: token, respKey, useragent}</c>. No
    /// createTask-shaped <c>solution</c>.
    /// </summary>
    public sealed class MockHCaptchaApiClient : ApiClient
    {
        public const string Token = "P1_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.hadwYXNza2V5xQZ2f3Kq8nV2";

        public const string RespKey = "E0_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjA09wB1";

        /// <summary>
        /// The UA the browser earned the token with, as CapSkip reports it. Kept
        /// apart from the caller's own so a test can tell which one came back.
        /// </summary>
        public const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36";

        private readonly string? _respKey;
        private readonly string? _userAgent;

        /// <summary>The fields sent to <c>in.php</c>, or null when nothing was sent.</summary>
        public IDictionary<string, object?>? Incomings { get; private set; }

        /// <summary>The query of the last <c>res.php</c> poll.</summary>
        public IDictionary<string, object?>? Polled { get; private set; }

        /// <param name="respKey">The respKey the reply carries, or null for a reply with none.</param>
        /// <param name="userAgent">The UA the reply carries, or null for a reply with none.</param>
        public MockHCaptchaApiClient(string? respKey = RespKey, string? userAgent = BrowserUserAgent)
            : base("mock", 0)
        {
            _respKey = respKey;
            _userAgent = userAgent;
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
            Polled = new Dictionary<string, object?>(query);
            if (query.TryGetValue("json", out var json) && (Equals(json, 1) || Equals(json, "1")))
            {
                var reply = new Dictionary<string, object?>
                {
                    ["status"] = 1,
                    ["request"] = Token,
                };
                if (_respKey != null)
                {
                    reply["respKey"] = _respKey;
                }

                if (_userAgent != null)
                {
                    reply["useragent"] = _userAgent;
                }

                return Task.FromResult(JsonSerializer.Serialize(reply));
            }

            return Task.FromResult("OK|" + Token);
        }
    }

    public class HCaptchaTests
    {
        private const string Url = "https://accounts.hcaptcha.com/demo";
        private const string Sitekey = "a5f74b19-9e45-40e0-b45d-47ff91b7a6c2";
        private const string Rqdata = "Wk2qT7bX+fV9rL0pZ3mN8cJ1hYsD6uE4gA5oR2iK/0=";
        private const string CallerUserAgent = "Mozilla/5.0 (the caller own UA)";

        private static CapSkipClient Make(
            out MockHCaptchaApiClient client,
            string? respKey = MockHCaptchaApiClient.RespKey,
            string? userAgent = MockHCaptchaApiClient.BrowserUserAgent)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockHCaptchaApiClient(respKey, userAgent);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.HCaptchaAsync(Sitekey, Url, options);

        private static void AssertSent(MockHCaptchaApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            Assert.NotNull(client.Incomings);
            MockSolver.AssertDictEqual(want, client.Incomings!);
        }

        private static Dictionary<string, object?> Basic() => new Dictionary<string, object?>
        {
            ["method"] = "hcaptcha",
            ["sitekey"] = Sitekey,
            ["pageurl"] = Url,
        };

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_the_basic_task()
        {
            var solver = Make(out var client);

            var result = await Solve(solver);

            AssertSent(client, Basic());
            Assert.Equal("123", result.CaptchaId);
        }

        [Fact]
        public async Task Pageurl_is_accepted_in_place_of_url()
        {
            var solver = Make(out var client);

            await solver.HCaptchaAsync(Sitekey, null!, new Dictionary<string, object?> { ["pageurl"] = Url });

            Assert.Equal(Url, client.Incomings!["pageurl"]);
        }

        public static IEnumerable<object[]> InvisibleOn() => new[]
        {
            new object[] { 1 }, new object[] { 1L }, new object[] { true },
            new object[] { "1" }, new object[] { "true" }, new object[] { "TRUE" }, new object[] { "True" },
        };

        [Theory]
        [MemberData(nameof(InvisibleOn))]
        public async Task Invisible_on_is_sent_as_1(object value)
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["invisible"] = value });

            Assert.Equal(1, client.Incomings!["invisible"]);
        }

        public static IEnumerable<object[]> InvisibleOff() => new[]
        {
            new object[] { 0 }, new object[] { false }, new object[] { "0" }, new object[] { "false" },
            new object[] { "False" },
        };

        /// <summary>0 is the server's default; a bool false must not reach it as "false" or "".</summary>
        [Theory]
        [MemberData(nameof(InvisibleOff))]
        public async Task Invisible_off_is_not_sent(object value)
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["invisible"] = value });

            AssertSent(client, Basic());
        }

        public static IEnumerable<object[]> InvisibleBad() => new[]
        {
            new object[] { 2 }, new object[] { -1 }, new object[] { "yes" }, new object[] { "invisible" },
            new object[] { 1.5 },
        };

        [Theory]
        [MemberData(nameof(InvisibleBad))]
        public async Task Bad_invisible_is_refused(object value)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["invisible"] = value }));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task Typed_invisible_overload_sends_1_or_nothing()
        {
            var solver = Make(out var client);

            await solver.HCaptchaAsync(Sitekey, Url, invisible: true);
            Assert.Equal(1, client.Incomings!["invisible"]);

            await solver.HCaptchaAsync(Sitekey, Url, invisible: false);
            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Typed_invisible_overload_refuses_a_conflicting_option()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.HCaptchaAsync(
                    Sitekey, Url, true, new Dictionary<string, object?> { ["invisible"] = 0 }));
            Assert.Null(client.Incomings);

            // Agreeing values are fine.
            await solver.HCaptchaAsync(Sitekey, Url, true, new Dictionary<string, object?> { ["invisible"] = "1" });
            Assert.Equal(1, client.Incomings!["invisible"]);
        }

        [Fact]
        public async Task Enterprise_rqdata_is_sent_as_data()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["data"] = Rqdata });

            Assert.Equal(Rqdata, client.Incomings!["data"]);
        }

        [Fact]
        public async Task Useragent_is_forwarded()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["useragent"] = CallerUserAgent });

            Assert.Equal(CallerUserAgent, client.Incomings!["useragent"]);
        }

        [Fact]
        public async Task CamelCase_and_snake_case_useragent_aliases()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings!["useragent"]);
            Assert.False(client.Incomings.ContainsKey("userAgent"));

            await Solve(solver, new Dictionary<string, object?> { ["user_agent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings!["useragent"]);
            Assert.False(client.Incomings.ContainsKey("user_agent"));
        }

        [Fact]
        public async Task Unset_optionals_are_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["invisible"] = null,
                ["data"] = null,
                ["userAgent"] = null,
                ["proxy"] = null,
            });

            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Empty_optionals_are_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["data"] = "",
                ["userAgent"] = "",
                ["invisible"] = "",
            });

            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Proxy_object_is_split_into_proxy_and_proxytype()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["proxy"] = new Dictionary<string, object?> { ["type"] = "SOCKS5", ["uri"] = "login:password@1.2.3.4:1080" },
            });

            var want = Basic();
            want["proxy"] = "login:password@1.2.3.4:1080";
            want["proxytype"] = "SOCKS5";
            AssertSent(client, want);
        }

        [Fact]
        public async Task A_bare_proxy_defaults_to_http()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["proxy"] = "1.2.3.4:8080" });

            Assert.Equal("1.2.3.4:8080", client.Incomings!["proxy"]);
            Assert.Equal("HTTP", client.Incomings["proxytype"]);
        }

        [Fact]
        public async Task Everything_together()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["invisible"] = 1,
                ["data"] = Rqdata,
                ["userAgent"] = CallerUserAgent,
                ["proxy"] = new Proxy("HTTPS", "1.2.3.4:8080"),
                ["json"] = 1,
            });

            var want = Basic();
            want["invisible"] = 1;
            want["data"] = Rqdata;
            want["useragent"] = CallerUserAgent;
            want["proxy"] = "1.2.3.4:8080";
            want["proxytype"] = "HTTPS";
            want["json"] = 1;
            AssertSent(client, want);
        }

        // -- what is refused before it costs a round-trip -----------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Missing_sitekey_is_refused(string? sitekey)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.HCaptchaAsync(sitekey!, Url));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Missing_pageurl_is_refused(string? url)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.HCaptchaAsync(Sitekey, url!));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("accounts.hcaptcha.com/demo")]
        [InlineData("ftp://example.com/")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://")]
        public async Task Non_http_pageurl_is_refused(string url)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.HCaptchaAsync(Sitekey, url));
            Assert.Null(client.Incomings);
        }

        /// <summary>2Captcha's <c>domain</c> is not part of CapSkip's hCaptcha API.</summary>
        [Theory]
        [InlineData("domain", "js.hcaptcha.com")]
        [InlineData("enterprise", 1)]
        public async Task Unknown_parameter_is_refused(string name, object value)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { [name] = value }));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task Socks4_is_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?>
                {
                    ["proxy"] = new Proxy("SOCKS4", "1.2.3.4:1080"),
                }));
            Assert.Null(client.Incomings);
        }

        // -- what comes back ----------------------------------------------

        [Fact]
        public async Task Token_is_exposed_as_token_and_code()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockHCaptchaApiClient.Token, result.Token);
            Assert.Equal(MockHCaptchaApiClient.Token, result.Code);
        }

        [Fact]
        public async Task RespKey_is_exposed()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockHCaptchaApiClient.RespKey, result.RespKey);
        }

        [Fact]
        public async Task An_empty_respKey_stays_empty()
        {
            var solver = Make(out _, respKey: "");

            var result = await Solve(solver);

            Assert.Equal("", result.RespKey);
            Assert.Equal(MockHCaptchaApiClient.Token, result.Token);
        }

        [Fact]
        public async Task RespKey_is_empty_not_null_when_the_reply_has_none()
        {
            var solver = Make(out _, respKey: null);

            var result = await Solve(solver);

            Assert.NotNull(result.RespKey);
            Assert.Equal("", result.RespKey);
        }

        [Fact]
        public async Task Result_carries_the_user_agent_that_earned_the_token()
        {
            var solver = Make(out _);

            var result = await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = CallerUserAgent });

            Assert.Equal(MockHCaptchaApiClient.BrowserUserAgent, result.UserAgent);
        }

        [Fact]
        public async Task User_agent_is_null_when_the_solve_reported_none()
        {
            var solver = Make(out _, userAgent: null);

            var result = await Solve(solver);

            Assert.Null(result.UserAgent);
        }

        [Fact]
        public async Task Polls_with_json()
        {
            var solver = Make(out var client);

            await Solve(solver);

            Assert.Equal(1, client.Polled!["json"]);
        }

        [Fact]
        public async Task ToString_shows_token_and_respKey()
        {
            var solver = Make(out _);

            var text = (await Solve(solver)).ToString();

            Assert.Contains(MockHCaptchaApiClient.Token, text);
            Assert.Contains("RespKey = " + MockHCaptchaApiClient.RespKey, text);
        }

        /// <summary>
        /// A real browser session, so the solve gets RecaptchaTimeout. The timeout
        /// never reaches the wire, so assert on the effect: a client whose reCAPTCHA
        /// budget has elapsed gives up, even though the much longer default has not.
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

        [Fact]
        public async Task Works_through_the_async_alias()
        {
            var solver = new AsyncCapSkip(apiKey: "API_KEY", pollingInterval: 1);
            var client = new MockHCaptchaApiClient();
            solver.ApiClient = client;

            var result = await solver.HCaptchaAsync(Sitekey, Url);

            AssertSent(client, Basic());
            Assert.Equal(MockHCaptchaApiClient.Token, result.Token);
            Assert.Equal(MockHCaptchaApiClient.RespKey, result.RespKey);
            Assert.Equal(MockHCaptchaApiClient.BrowserUserAgent, result.UserAgent);
        }
    }
}
