using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> returning a realistic CaptchaFox answer — a
    /// token plus the User-Agent the browser minted it under.
    /// </summary>
    public sealed class MockCaptchaFoxApiClient : ApiClient
    {
        public const string Token =
            "177f50c25b845601e5c779cdb51b040d523e8ab69efb4d5b343e28df07d05076";

        /// <summary>
        /// The UA the browser actually minted the token under. CapSkip never applies
        /// the caller's own, so these two must stay distinguishable in the tests.
        /// </summary>
        public const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

        private readonly string? userAgent;

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        public MockCaptchaFoxApiClient(string? userAgent = BrowserUserAgent)
            : base("mock", 0)
        {
            this.userAgent = userAgent;
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
                var ua = userAgent is null
                    ? string.Empty
                    : $"\"userAgent\":\"{userAgent}\",";

                return Task.FromResult(
                    "{\"status\":1,\"request\":\"" + Token + "\"," + ua
                    + "\"solution\":{\"token\":\"" + Token + "\"}}");
            }

            return Task.FromResult("OK|" + Token);
        }
    }

    public class CaptchaFoxTests
    {
        private const string Url = "https://mysite.com/page/with/captchafox";
        private const string Sitekey = "sk_xtNxpk6fCdFbxh1_xJeGflSdCE9tn99G";
        private const string CallerUserAgent = "Mozilla/5.0 (the caller's own UA)";

        private const string MamApiServer =
            "https://s.uicdn.com/mampkg/@mamdev/core.frontend.libs.captchafox/";

        private static CapSkipClient Make(out MockCaptchaFoxApiClient client, string? userAgent = MockCaptchaFoxApiClient.BrowserUserAgent)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockCaptchaFoxApiClient(userAgent);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.CaptchaFoxAsync(Sitekey, Url, options);

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_the_documented_parameters()
        {
            var solver = Make(out var client);

            var result = await Solve(solver);

            Assert.Equal("captchafox", client.Incomings["method"]);
            Assert.Equal(Sitekey, client.Incomings["sitekey"]);
            Assert.Equal(Url, client.Incomings["pageurl"]);
            Assert.Equal("123", result.CaptchaId);
        }

        /// <summary>
        /// The widget source decides the token format: the MAM package returns a
        /// MAM_ prefixed token, and sending the wrong one fails silently at the
        /// target site rather than erroring here.
        /// </summary>
        [Fact]
        public async Task Forwards_the_mam_widget_source()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["api_server"] = MamApiServer });
            Assert.Equal(MamApiServer, client.Incomings["api_server"]);

            await Solve(solver, new Dictionary<string, object?> { ["apiServer"] = MamApiServer });
            Assert.Equal(MamApiServer, client.Incomings["api_server"]);
        }

        [Fact]
        public async Task Forwards_useragent_and_its_camelCase_alias()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["useragent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings["useragent"]);

            await Solve(solver, new Dictionary<string, object?> { ["userAgent"] = CallerUserAgent });
            Assert.Equal(CallerUserAgent, client.Incomings["useragent"]);
            Assert.False(client.Incomings.ContainsKey("userAgent"));
        }

        [Fact]
        public async Task Does_not_send_an_unset_optional()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["api_server"] = null,
                ["useragent"] = null,
            });

            Assert.False(client.Incomings.ContainsKey("api_server"));
            Assert.False(client.Incomings.ContainsKey("useragent"));
        }

        [Fact]
        public async Task Splits_a_proxy_into_proxy_and_proxytype()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy { Type = "HTTP", Uri = "login:password@1.2.3.4:8080" },
            });

            Assert.Equal("login:password@1.2.3.4:8080", client.Incomings["proxy"]);
            Assert.Equal("HTTP", client.Incomings["proxytype"]);
        }

        // -- what is refused before it costs a round-trip -----------------

        [Fact]
        public async Task Refuses_a_missing_sitekey_or_pageurl()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => solver.CaptchaFoxAsync("", Url));
            await Assert.ThrowsAsync<ValidationException>(() => solver.CaptchaFoxAsync(Sitekey, ""));
        }

        [Fact]
        public async Task Refuses_an_unknown_parameter()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["version"] = "v2" }));
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
        public async Task Exposes_the_token()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockCaptchaFoxApiClient.Token, result.Token);
            Assert.Equal(MockCaptchaFoxApiClient.Token, result.Code);
        }

        /// <summary>
        /// Not the one the caller sent — CapSkip solves in its own browser, and the
        /// token has to be submitted under the UA that produced it.
        /// </summary>
        [Fact]
        public async Task Reports_the_user_agent_that_minted_the_token()
        {
            var solver = Make(out _);

            var result = await Solve(
                solver, new Dictionary<string, object?> { ["useragent"] = CallerUserAgent });

            Assert.Equal(MockCaptchaFoxApiClient.BrowserUserAgent, result.UserAgent);
            Assert.NotEqual(CallerUserAgent, result.UserAgent);
        }

        /// <summary>A UA the solve did not actually use must never be invented.</summary>
        [Fact]
        public async Task Omits_the_user_agent_when_the_solve_reported_none()
        {
            var solver = Make(out _, userAgent: null);

            var result = await Solve(solver);

            Assert.Null(result.UserAgent);
            Assert.Equal(MockCaptchaFoxApiClient.Token, result.Token);
        }
    }
}
