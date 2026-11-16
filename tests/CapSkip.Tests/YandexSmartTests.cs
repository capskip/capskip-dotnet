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
    /// Mock <see cref="ApiClient"/> answering as CapSkip's res.php does for a Yandex
    /// SmartCaptcha token: <c>{status, request: token}</c> with json=1,
    /// <c>OK|token</c> without, unless the test hands it a different reply.
    /// </summary>
    public sealed class MockYandexApiClient : ApiClient
    {
        /// <summary>
        /// Shaped like a real token, base64 of <c>t=...;i=...;D=...</c>, but carrying
        /// a documentation address: a real one embeds the solver's public IP.
        /// </summary>
        public const string Token =
            "dD0xNzk0NDE5NTg3O2k9MjAzLjAuMTEzLjI1O0Q9QjQ1RjJBN0MxRTkwRDM2ODU0QzA1M0M3QTFF"
            + "MkIzQzRENUU2RjcwODE5MkEzQjRDNUQ2RTdGODA5MUEyQjNDNEQ1RTZGNzA4MTt1PTE3OTQ0MTk1"
            + "ODcxMjM0NTY3ODk7aD01ZjBjMmE5ZTYxZDg0YjM3YTBjMWU5YjJkNmY0YTg3Mw==";

        private readonly string? _jsonReply;

        /// <summary>The fields sent to <c>in.php</c>, or null when nothing was sent.</summary>
        public IDictionary<string, object?>? Incomings { get; private set; }

        /// <summary>The query of the last <c>res.php</c> poll.</summary>
        public IDictionary<string, object?>? Polled { get; private set; }

        /// <param name="jsonReply">The json=1 reply to give, or null for the usual one.</param>
        public MockYandexApiClient(string? jsonReply = null)
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

    public class YandexSmartTests
    {
        private const string Url = "https://smartcaptcha.yandexcloud.net/demo";
        private const string Sitekey = "ysc1_DAo8nFPdNCMHkAwYxIUJFxW5IIJd99BBfeeeee";
        private const string CallerUserAgent = "Mozilla/5.0 (caller)";

        private static CapSkipClient Make(out MockYandexApiClient client, string? jsonReply = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockYandexApiClient(jsonReply);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.YandexSmartAsync(Sitekey, Url, options);

        private static void AssertSent(MockYandexApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            Assert.NotNull(client.Incomings);
            MockSolver.AssertDictEqual(want, client.Incomings!);
        }

        private static Dictionary<string, object?> Basic(string sitekey = Sitekey) => new Dictionary<string, object?>
        {
            ["method"] = "yandex",
            ["sitekey"] = sitekey,
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
                ["userAgent"] = CallerUserAgent,
                ["proxy"] = new Dictionary<string, object?>
                {
                    ["type"] = "SOCKS5",
                    ["uri"] = "login:password@203.0.113.7:1080",
                },
                ["json"] = 1,
            });

            var want = Basic();
            want["useragent"] = CallerUserAgent;
            want["proxy"] = "login:password@203.0.113.7:1080";
            want["proxytype"] = "SOCKS5";
            want["json"] = 1;
            AssertSent(client, want);
        }

        [Fact]
        public async Task Pageurl_is_accepted_in_place_of_url()
        {
            var solver = Make(out var client);

            await solver.YandexSmartAsync(Sitekey, null!, new Dictionary<string, object?> { ["pageurl"] = Url });

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

        [Fact]
        public async Task Sitekey_is_trimmed()
        {
            var solver = Make(out var client);

            await solver.YandexSmartAsync("  " + Sitekey + "\n", Url);

            AssertSent(client, Basic());
        }

        /// <summary>Older client keys carry no ysc1_ prefix; the server takes them.</summary>
        [Theory]
        [InlineData("FEXfAbHQsToo97VidNVk3j4dC74nGW1DgdxjtNB9")]
        [InlineData("aaaaaaaaaaaaaaaa")]
        public async Task Sitekeys_without_the_ysc1_prefix_are_accepted(string sitekey)
        {
            var solver = Make(out var client);

            await solver.YandexSmartAsync(sitekey, Url);

            AssertSent(client, Basic(sitekey));
        }

        [Fact]
        public async Task The_longest_sitekey_is_accepted()
        {
            var solver = Make(out var client);
            var sitekey = new string('a', 120);

            await solver.YandexSmartAsync(sitekey, Url);

            Assert.Equal(sitekey, client.Incomings!["sitekey"]);
        }

        [Fact]
        public async Task Unset_and_empty_optionals_are_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = "", ["proxy"] = null });
            AssertSent(client, Basic());

            await Solve(solver, new Dictionary<string, object?> { ["useragent"] = null, ["proxy"] = "" });
            AssertSent(client, Basic());
        }

        // -- what is refused before it costs a round-trip -----------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Missing_sitekey_is_refused(string? sitekey)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.YandexSmartAsync(sitekey!, Url));
            Assert.Null(client.Incomings);
        }

        public static IEnumerable<object[]> MalformedSitekeys() => new[]
        {
            new object[] { "nope" },
            new object[] { new string('a', 15) },
            new object[] { new string('a', 121) },
            new object[] { "ysc1_has space in it" },
            new object[] { "ysc1_dotted.key.value" },
            new object[] { "ysc1_\" onload=\"x" },
        };

        [Theory]
        [MemberData(nameof(MalformedSitekeys))]
        public async Task Malformed_sitekey_is_refused(string sitekey)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.YandexSmartAsync(sitekey, Url));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task A_non_string_sitekey_is_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.SolveAsync(new Dictionary<string, object?>
            {
                ["method"] = "yandex",
                ["sitekey"] = 1234567890123456789L,
                ["pageurl"] = Url,
            }));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("smartcaptcha.yandexcloud.net/demo")]
        [InlineData("ftp://example.com/")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://")]
        public async Task Bad_pageurl_is_refused(string? url)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.YandexSmartAsync(Sitekey, url!));
            Assert.Null(client.Incomings);
        }

        /// <summary>Picture tasks (img_type) are not this method; action is not a SmartCaptcha field.</summary>
        [Theory]
        [InlineData("action", "login")]
        [InlineData("img_type", "smart_captcha")]
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

            Assert.Equal(MockYandexApiClient.Token, result.Code);
            Assert.Equal(MockYandexApiClient.Token, result.Token);
            Assert.Null(result.UserAgent);
            Assert.Null(result.RespKey);
            Assert.Null(result.PassToken);
        }

        [Fact]
        public async Task Solution_token_fills_an_empty_request()
        {
            var reply = JsonSerializer.Serialize(new Dictionary<string, object?>
            {
                ["status"] = 1,
                ["request"] = "",
                ["solution"] = new Dictionary<string, object?> { ["token"] = MockYandexApiClient.Token },
            });
            var solver = Make(out _, reply);

            var result = await Solve(solver);

            Assert.Equal(MockYandexApiClient.Token, result.Code);
            Assert.Equal(MockYandexApiClient.Token, result.Token);
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
        /// The widget runs in a real browser and may ask for several rounds, so it
        /// takes the longer timeout. The timeout never reaches the wire, so assert on
        /// the effect: a client whose reCAPTCHA budget has elapsed gives up, even
        /// though the default one has not.
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
            var client = new MockYandexApiClient();
            solver.ApiClient = client;

            var result = await solver.YandexSmartAsync(Sitekey, Url);

            AssertSent(client, Basic());
            Assert.Equal(MockYandexApiClient.Token, result.Code);
            Assert.Equal(MockYandexApiClient.Token, result.Token);
        }
    }
}
