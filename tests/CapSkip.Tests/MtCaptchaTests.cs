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
    /// Mock <see cref="ApiClient"/> answering as CapSkip's res.php does for MTCaptcha
    /// with json=1: <c>{status, request: token}</c> and nothing else, unless the
    /// test hands it a different reply.
    /// </summary>
    public sealed class MockMtCaptchaApiClient : ApiClient
    {
        /// <summary>
        /// The verified token MTCaptcha issues: <c>v1(</c>, comma-separated parts
        /// (the sitekey among them), a base64url payload, closed by <c>**)</c>.
        /// </summary>
        public const string Token =
            "v1(0f3c9e2a,7b1d4c88,MTPublic-KzqLY1cKH,5e0c2a9f61d84b37a0c1e9b2d6f4a873,"
            + "Qm9ndXNUb2tlbkZvclRlc3RzT25seV9fTm90QVJlYWxNVENhcHRjaGFUb2tlbi1BYkNkRWZHaA**)";

        private readonly string? _jsonReply;

        /// <summary>The fields sent to <c>in.php</c>, or null when nothing was sent.</summary>
        public IDictionary<string, object?>? Incomings { get; private set; }

        /// <summary>The query of the last <c>res.php</c> poll.</summary>
        public IDictionary<string, object?>? Polled { get; private set; }

        /// <param name="jsonReply">The json=1 reply to give, or null for the usual one.</param>
        public MockMtCaptchaApiClient(string? jsonReply = null)
            : base("mock", 0)
        {
            _jsonReply = jsonReply;
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
                return Task.FromResult(_jsonReply ?? JsonSerializer.Serialize(
                    new Dictionary<string, object?> { ["status"] = 1, ["request"] = Token }));
            }

            return Task.FromResult("OK|" + Token);
        }
    }

    public class MtCaptchaTests
    {
        private const string Url = "https://2captcha.com/demo/mtcaptcha";
        private const string Sitekey = "MTPublic-KzqLY1cKH";
        private const string CallerUserAgent = "Mozilla/5.0 (caller)";

        private static CapSkipClient Make(out MockMtCaptchaApiClient client, string? jsonReply = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockMtCaptchaApiClient(jsonReply);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.MtCaptchaAsync(Sitekey, Url, options);

        private static void AssertSent(MockMtCaptchaApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            Assert.NotNull(client.Incomings);
            MockSolver.AssertDictEqual(want, client.Incomings!);
        }

        private static Dictionary<string, object?> Basic() => new Dictionary<string, object?>
        {
            ["method"] = "mt_captcha",
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
        public async Task Every_option_is_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["action"] = "login",
                ["userAgent"] = CallerUserAgent,
                ["proxy"] = new Dictionary<string, object?>
                {
                    ["type"] = "HTTPS",
                    ["uri"] = "login:password@203.0.113.7:3128",
                },
                ["json"] = 1,
            });

            var want = Basic();
            want["action"] = "login";
            want["useragent"] = CallerUserAgent;
            want["proxy"] = "login:password@203.0.113.7:3128";
            want["proxytype"] = "HTTPS";
            want["json"] = 1;
            AssertSent(client, want);
        }

        [Fact]
        public async Task Pageurl_is_accepted_in_place_of_url()
        {
            var solver = Make(out var client);

            await solver.MtCaptchaAsync(Sitekey, null!, new Dictionary<string, object?> { ["pageurl"] = Url });

            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Useragent_aliases_are_sent_as_useragent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["user_agent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings!["useragent"]);
            Assert.False(client.Incomings.ContainsKey("user_agent"));

            await Solve(solver, new Dictionary<string, object?> { ["useragent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings!["useragent"]);
        }

        /// <summary>MTCaptcha's getchallenge query names the key sk=MTPublic-...; callers copy it.</summary>
        [Theory]
        [InlineData("sk=" + Sitekey)]
        [InlineData(" SK=" + Sitekey + " ")]
        [InlineData("Sk= " + Sitekey)]
        [InlineData("  " + Sitekey)]
        public async Task Sk_prefix_and_whitespace_are_stripped(string sitekey)
        {
            var solver = Make(out var client);

            await solver.MtCaptchaAsync(sitekey, Url);

            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Unset_and_empty_optionals_are_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["action"] = "",
                ["userAgent"] = null,
                ["proxy"] = null,
            });
            AssertSent(client, Basic());

            await Solve(solver, new Dictionary<string, object?> { ["useragent"] = "", ["action"] = null });
            AssertSent(client, Basic());
        }

        [Fact]
        public async Task A_bare_proxy_defaults_to_http()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["proxy"] = "203.0.113.7:8080" });

            Assert.Equal("203.0.113.7:8080", client.Incomings!["proxy"]);
            Assert.Equal("HTTP", client.Incomings["proxytype"]);
        }

        [Fact]
        public async Task The_mtcaptcha_spelling_of_the_method_is_validated_too()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.SolveAsync(new Dictionary<string, object?>
            {
                ["method"] = "mtcaptcha",
                ["sitekey"] = "KzqLY1cKH",
                ["pageurl"] = Url,
            }));
            Assert.Null(client.Incomings);
        }

        // -- what is refused before it costs a round-trip -----------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("sk=")]
        [InlineData(null)]
        public async Task Missing_sitekey_is_refused(string? sitekey)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.MtCaptchaAsync(sitekey!, Url));
            Assert.Null(client.Incomings);
        }

        public static IEnumerable<object[]> MalformedSitekeys() => new[]
        {
            new object[] { "KzqLY1cKH" },
            new object[] { "MTPublic-" },
            new object[] { "mtpublic-KzqLY1cKH" },
            new object[] { "MTPublic-Kzq LY1" },
            new object[] { "MTPublic-Kzq.LY1" },
            new object[] { "MTPrivate-KzqLY1cKH" },
            new object[] { "MTPublic-" + new string('a', 56) },
        };

        [Theory]
        [MemberData(nameof(MalformedSitekeys))]
        public async Task Malformed_sitekey_is_refused(string sitekey)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.MtCaptchaAsync(sitekey, Url));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task A_non_string_sitekey_is_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.SolveAsync(new Dictionary<string, object?>
            {
                ["method"] = "mt_captcha",
                ["sitekey"] = 12345,
                ["pageurl"] = Url,
            }));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task The_longest_sitekey_is_accepted()
        {
            var solver = Make(out var client);
            var sitekey = "MTPublic-" + new string('a', 55);

            await solver.MtCaptchaAsync(sitekey, Url);

            Assert.Equal(64, sitekey.Length);
            Assert.Equal(sitekey, client.Incomings!["sitekey"]);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("2captcha.com/demo/mtcaptcha")]
        [InlineData("ftp://example.com/")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://")]
        public async Task Bad_pageurl_is_refused(string? url)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.MtCaptchaAsync(Sitekey, url!));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("invisible", 1)]
        [InlineData("domain", "service.mtcaptcha.com")]
        [InlineData("data", "x")]
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
                    ["proxy"] = new Proxy("SOCKS4", "203.0.113.7:1080"),
                }));
            Assert.Null(client.Incomings);
        }

        // -- what comes back ----------------------------------------------

        [Fact]
        public async Task Token_is_exposed_as_code_and_token()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockMtCaptchaApiClient.Token, result.Code);
            Assert.Equal(MockMtCaptchaApiClient.Token, result.Token);
            Assert.Null(result.UserAgent);
            Assert.Null(result.RespKey);
        }

        [Fact]
        public async Task Solution_token_fills_an_empty_request()
        {
            var reply = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["status"] = 1,
                ["request"] = "",
                ["solution"] = new Dictionary<string, object?> { ["token"] = MockMtCaptchaApiClient.Token },
            });
            var solver = Make(out _, reply);

            var result = await Solve(solver);

            Assert.Equal(MockMtCaptchaApiClient.Token, result.Code);
            Assert.Equal(MockMtCaptchaApiClient.Token, result.Token);
        }

        [Fact]
        public async Task Polls_with_json()
        {
            var solver = Make(out var client);

            await Solve(solver);

            Assert.Equal(1, client.Polled!["json"]);
            Assert.Equal("get", client.Polled["action"]);
            Assert.Equal("123", client.Polled["id"]);
        }

        /// <summary>
        /// No browser runs, so MTCaptcha keeps the default timeout like ALTCHA and
        /// Capy. The timeout never reaches the wire, so assert on the effect: a client
        /// whose default budget has elapsed gives up, even though the much longer
        /// reCAPTCHA one has not.
        /// </summary>
        [Fact]
        public async Task Uses_the_default_timeout_not_the_recaptcha_one()
        {
            var solver = new CapSkipClient(
                apiKey: "API_KEY", defaultTimeout: 0.05, recaptchaTimeout: 600, pollingInterval: 0.01);
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
            var client = new MockMtCaptchaApiClient();
            solver.ApiClient = client;

            var result = await solver.MtCaptchaAsync(Sitekey, Url);

            AssertSent(client, Basic());
            Assert.Equal(MockMtCaptchaApiClient.Token, result.Code);
            Assert.Equal(MockMtCaptchaApiClient.Token, result.Token);
        }
    }
}
