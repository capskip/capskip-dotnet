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
    /// Mock <see cref="ApiClient"/> answering as CapSkip does for a DataDome task:
    /// the cookie as <c>request</c>, the User-Agent it was earned with, and the
    /// proxy its own list supplied. No createTask-shaped <c>solution</c>.
    /// </summary>
    public sealed class MockDataDomeApiClient : ApiClient
    {
        public const string UserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36";

        /// <summary>The cookie exactly as DataDome set it, attributes included.</summary>
        public const string Cookie =
            "datadome=FAPPpli4jkT1MZGtgF8ez7mjSz8MrJ9fGRNE51Gj79cq9fXp5xn~F1P5APWjqvJojf4U9dq0R81Xaj2hpYtjle95mkg; "
            + "Max-Age=31536000; Domain=.example.com; Path=/; Secure; SameSite=Lax";

        public const string CookiePair =
            "datadome=FAPPpli4jkT1MZGtgF8ez7mjSz8MrJ9fGRNE51Gj79cq9fXp5xn~F1P5APWjqvJojf4U9dq0R81Xaj2hpYtjle95mkg";

        public const string ListProxy = "203.0.113.10:8080";

        private readonly string? _proxy;

        /// <summary>The fields sent to <c>in.php</c>, or null when nothing was sent.</summary>
        public IDictionary<string, object?>? Incomings { get; private set; }

        /// <summary>The query of the last <c>res.php</c> poll.</summary>
        public IDictionary<string, object?>? Polled { get; private set; }

        /// <param name="proxy">The proxy the reply names, or null for a reply with none.</param>
        public MockDataDomeApiClient(string? proxy = ListProxy)
            : base("mock", 0)
        {
            _proxy = proxy;
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
                    ["request"] = Cookie,
                    ["userAgent"] = UserAgent,
                };
                if (_proxy != null)
                {
                    reply["proxy"] = _proxy;
                }

                return Task.FromResult(JsonSerializer.Serialize(reply));
            }

            return Task.FromResult("OK|" + Cookie);
        }
    }

    public class DataDomeTests
    {
        private const string PageUrl = "https://www.example.com/";

        private const string CaptchaUrl =
            "https://geo.captcha-delivery.com/captcha/?initialCid=AHrlqAAAAAMA7KBvCr0n4cEAG330MA%3D%3D"
            + "&hash=D013AA612AB2224D03B2318D0F5B19&cid=KzE6_pZBBTHdCdvZkauT9VAshi0wS7~v5_M_MacEtdw8"
            + "&t=fe&referer=https%3A%2F%2Fwww.example.com%2F&s=45225&e=549255a0289eaf17af60822309ab8db4&dm=cd";

        private const string InterstitialUrl =
            "https://geo.captcha-delivery.com/interstitial/?initialCid=AHrlqAAAAAMA7KBvCr0n4cEAG330MA%3D%3D"
            + "&hash=D013AA612AB2224D03B2318D0F5B19&cid=KzE6_pZBBTHdCdvZkauT9VAshi0wS7~v5_M_MacEtdw8"
            + "&referer=https%3A%2F%2Fwww.example.com%2F&s=45225&b=1612345&dm=cd";

        private const string UserAgent = MockDataDomeApiClient.UserAgent;

        private static CapSkipClient Make(out MockDataDomeApiClient client, string? proxy = MockDataDomeApiClient.ListProxy)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockDataDomeApiClient(proxy);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver,
            IDictionary<string, object?>? options = null,
            string captchaUrl = CaptchaUrl,
            string pageUrl = PageUrl)
        {
            return solver.DataDomeAsync(captchaUrl, pageUrl, options);
        }

        private static void AssertSent(MockDataDomeApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            Assert.NotNull(client.Incomings);
            MockSolver.AssertDictEqual(want, client.Incomings!);
        }

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_the_minimal_task()
        {
            var solver = Make(out var client);

            var result = await Solve(solver);

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "datadome",
                ["captcha_url"] = CaptchaUrl,
                ["pageurl"] = PageUrl,
            });
            Assert.Equal("123", result.CaptchaId);

            // Polled as JSON, so userAgent and proxy come back with the cookie.
            Assert.Equal(1, client.Polled!["json"]);
        }

        /// <summary>
        /// 2Captcha's order is (captcha_url, pageurl, userAgent, proxy): the two
        /// required values positionally, the optional ones in options.
        /// </summary>
        [Fact]
        public async Task Follows_the_2captcha_argument_order()
        {
            var solver = Make(out var client);

            await solver.DataDomeAsync(CaptchaUrl, PageUrl, new Dictionary<string, object?>
            {
                ["userAgent"] = UserAgent,
                ["proxy"] = new Dictionary<string, object?> { ["type"] = "HTTP", ["uri"] = "login:password@1.2.3.4:8080" },
            });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "datadome",
                ["captcha_url"] = CaptchaUrl,
                ["pageurl"] = PageUrl,
                ["useragent"] = UserAgent,
                ["proxy"] = "login:password@1.2.3.4:8080",
                ["proxytype"] = "HTTP",
            });
        }

        [Fact]
        public async Task Url_stands_in_for_the_page_url()
        {
            var solver = Make(out var client);

            await solver.DataDomeAsync(CaptchaUrl, null!, new Dictionary<string, object?> { ["url"] = PageUrl });

            Assert.Equal(PageUrl, client.Incomings!["pageurl"]);
        }

        [Theory]
        [InlineData("captchaUrl")]
        [InlineData("captchaURL")]
        public async Task Accepts_the_camel_case_captcha_url(string alias)
        {
            var solver = Make(out var client);

            await solver.DataDomeAsync(null!, PageUrl, new Dictionary<string, object?> { [alias] = CaptchaUrl });

            Assert.Equal(CaptchaUrl, client.Incomings!["captcha_url"]);
        }

        [Fact]
        public async Task Submits_the_interstitial_device_check()
        {
            var solver = Make(out var client);

            await Solve(solver, captchaUrl: InterstitialUrl);

            Assert.Equal(InterstitialUrl, client.Incomings!["captcha_url"]);
        }

        /// <summary>
        /// An iframe src copied out of the page source carries &amp;amp;; the server
        /// decodes it, so it is not refused here either, and it goes out unchanged.
        /// </summary>
        [Fact]
        public async Task Accepts_an_html_escaped_iframe_src_and_sends_it_unchanged()
        {
            var solver = Make(out var client);
            var escaped = CaptchaUrl.Replace("&", "&amp;");

            await Solve(solver, captchaUrl: escaped);

            Assert.Equal(escaped, client.Incomings!["captcha_url"]);
        }

        [Fact]
        public async Task Accepts_a_scheme_relative_captcha_url()
        {
            var solver = Make(out var client);
            var relative = CaptchaUrl.Substring("https:".Length);

            await Solve(solver, captchaUrl: relative);

            Assert.Equal(relative, client.Incomings!["captcha_url"]);
        }

        [Fact]
        public async Task A_string_proxy_defaults_to_http()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["proxy"] = "1.2.3.4:8080" });

            Assert.Equal("1.2.3.4:8080", client.Incomings!["proxy"]);
            Assert.Equal("HTTP", client.Incomings!["proxytype"]);
        }

        [Fact]
        public async Task Accepts_a_socks5h_proxy()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy { Type = "SOCKS5H", Uri = "1.2.3.4:1080" },
            });

            Assert.Equal("SOCKS5H", client.Incomings!["proxytype"]);
        }

        [Fact]
        public async Task Empty_optionals_mean_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = "", ["proxy"] = "" });

            AssertSent(client, new Dictionary<string, object?>
            {
                ["method"] = "datadome",
                ["captcha_url"] = CaptchaUrl,
                ["pageurl"] = PageUrl,
            });
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

        // -- what comes back ----------------------------------------------

        [Fact]
        public async Task Result_carries_cookie_user_agent_and_list_proxy()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockDataDomeApiClient.Cookie, result.Code);
            Assert.Equal(MockDataDomeApiClient.CookiePair, result.Cookie);
            Assert.Equal(UserAgent, result.UserAgent);
            Assert.Equal(MockDataDomeApiClient.ListProxy, result.Proxy);
        }

        [Fact]
        public async Task No_proxy_when_capskip_used_none()
        {
            var solver = Make(out _, proxy: null);

            var result = await Solve(solver);

            Assert.Null(result.Proxy);
            Assert.Equal(MockDataDomeApiClient.CookiePair, result.Cookie);
        }

        /// <summary>An answer that was an object, not a string, is left alone.</summary>
        [Fact]
        public void Leaves_a_non_string_answer_alone()
        {
            var result = ResponseParsing.ApplyDataDomeSolution(
                new SolveResult { CaptchaId = "1", Code = "{\"x\":1}" });

            Assert.Null(result.Cookie);
            Assert.Equal("{\"x\":1}", result.Code);
        }

        [Fact]
        public void Leaves_an_empty_answer_alone()
        {
            var result = ResponseParsing.ApplyDataDomeSolution(new SolveResult { CaptchaId = "1", Code = "" });

            Assert.Null(result.Cookie);
        }

        [Fact]
        public void Expands_a_bare_cookie()
        {
            var result = ResponseParsing.ApplyDataDomeSolution(
                new SolveResult { CaptchaId = "1", Code = "datadome=abc" });

            Assert.Equal("datadome=abc", result.Cookie);
        }

        /// <summary>
        /// The proxy is carried from any poll reply that names one, not only on a
        /// DataDome solve; an empty one is not carried.
        /// </summary>
        [Fact]
        public void Carries_a_non_empty_proxy_from_any_poll()
        {
            var withProxy = ResponseParsing.ApplyPollResult(
                new SolveResult(),
                new Dictionary<string, object?> { ["request"] = "tok", ["proxy"] = "1.2.3.4:80" });
            var emptyProxy = ResponseParsing.ApplyPollResult(
                new SolveResult(),
                new Dictionary<string, object?> { ["request"] = "tok", ["proxy"] = "" });

            Assert.Equal("1.2.3.4:80", withProxy.Proxy);
            Assert.Null(emptyProxy.Proxy);
        }

        // -- refused locally, as the server would refuse them -------------

        private static async Task<string> AssertRefused(
            IDictionary<string, object?>? options = null,
            string captchaUrl = CaptchaUrl,
            string pageUrl = PageUrl)
        {
            var solver = Make(out var client);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, options, captchaUrl, pageUrl));

            Assert.Null(client.Incomings);
            return error.Message;
        }

        [Fact]
        public async Task Refuses_a_missing_captcha_url()
        {
            await AssertRefused(captchaUrl: null!);
            await AssertRefused(captchaUrl: "");
        }

        [Fact]
        public async Task Refuses_a_missing_page_url()
        {
            await AssertRefused(pageUrl: null!);
            await AssertRefused(pageUrl: "");
        }

        [Fact]
        public async Task Refuses_a_captcha_url_off_datadome()
        {
            await AssertRefused(captchaUrl: CaptchaUrl.Replace("captcha-delivery.com", "evil.example"));
        }

        [Fact]
        public async Task Refuses_a_lookalike_host()
        {
            await AssertRefused(captchaUrl: CaptchaUrl.Replace("geo.captcha-delivery.com", "evilcaptcha-delivery.com"));
        }

        [Fact]
        public async Task Refuses_a_captcha_url_over_http()
        {
            await AssertRefused(captchaUrl: CaptchaUrl.Replace("https://", "http://"));
        }

        [Fact]
        public async Task Refuses_another_path()
        {
            await AssertRefused(captchaUrl: CaptchaUrl.Replace("/captcha/", "/other/"));
        }

        [Fact]
        public async Task Refuses_a_captcha_url_without_the_challenge_ids()
        {
            await AssertRefused(captchaUrl: "https://geo.captcha-delivery.com/captcha/?initialCid=A&hash=B");
        }

        [Fact]
        public async Task Refuses_a_banned_block_page()
        {
            var message = await AssertRefused(captchaUrl: CaptchaUrl.Replace("t=fe", "t=bv"));

            Assert.Contains("t=bv", message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Refuses_a_page_url_that_is_not_http()
        {
            await AssertRefused(pageUrl: "ftp://www.example.com/");
        }

        [Fact]
        public async Task Refuses_a_page_url_on_the_challenge_host()
        {
            await AssertRefused(pageUrl: "https://geo.captcha-delivery.com/");
        }

        [Theory]
        [InlineData("Mozilla/5.0 (Windows NT 10.0; Win64; x64; rv:130.0) Gecko/20100101 Firefox/130.0")]
        [InlineData("Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36")]
        [InlineData(UserAgent + " Edg/156.0.0.0")]
        [InlineData("Mozilla/5.0 (Linux; Android 10; K) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Mobile Safari/537.36")]
        public async Task Refuses_a_user_agent_that_is_not_stock_windows_chrome(string userAgent)
        {
            var message = await AssertRefused(new Dictionary<string, object?> { ["userAgent"] = userAgent });

            Assert.Contains("stock Chrome for Windows", message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Refuses_socks4()
        {
            await AssertRefused(new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy { Type = "SOCKS4", Uri = "1.2.3.4:1080" },
            });
        }

        [Fact]
        public async Task Refuses_an_unknown_parameter()
        {
            await AssertRefused(new Dictionary<string, object?> { ["sitekey"] = "nope" });
        }
    }
}
