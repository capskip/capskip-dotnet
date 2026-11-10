using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using CapSkip;
using Xunit;
using TimeoutException = CapSkip.TimeoutException;

namespace CapSkip.Tests
{
    public sealed class MockServerFixture : IDisposable
    {
        public MockServer Server { get; } = new MockServer();

        public void Dispose() => Server.Dispose();
    }

    /// <summary>End-to-end tests driving the real HTTP layer against a local mock server.</summary>
    public class IntegrationTests : IClassFixture<MockServerFixture>
    {
        private const string SiteKey = "6Le-wvkSVVABCPBMRTvw0Q4Muexq1bi0DJwx_mJ-";
        private const string TurnstileSiteKey = "0x4AAAAAAABUYP0XeMJF0xoy";
        private const string Url = "https://example.com";

        private readonly MockServer _server;
        private readonly string _base64;

        public IntegrationTests(MockServerFixture fixture)
        {
            _server = fixture.Server;
            _base64 = Convert.ToBase64String(MockServer.Png);
        }

        private CapSkipClient MakeSolver(
            string apiKey = "capskip",
            double defaultTimeout = 120,
            double recaptchaTimeout = 300) =>
            new CapSkipClient(
                apiKey: apiKey,
                host: _server.Host,
                port: _server.Port,
                defaultTimeout: defaultTimeout,
                recaptchaTimeout: recaptchaTimeout,
                pollingInterval: 1);

        [Fact]
        public async Task NormalLocalFileUpload()
        {
            var solver = MakeSolver();
            var file = Path.Combine(Path.GetTempPath(), $"capskip-{Guid.NewGuid():N}.png");
            File.WriteAllBytes(file, MockServer.Png);
            try
            {
                var result = await solver.NormalAsync(file);
                Assert.Equal(MockServer.Code, result.Code);
                Assert.False(string.IsNullOrEmpty(result.CaptchaId));
            }
            finally
            {
                File.Delete(file);
            }
        }

        [Fact]
        public async Task NormalBase64()
        {
            var solver = MakeSolver();
            Assert.Equal(MockServer.Code, (await solver.NormalAsync(_base64)).Code);
        }

        [Fact]
        public async Task NormalDataUri()
        {
            var solver = MakeSolver();
            Assert.Equal(MockServer.Code, (await solver.NormalAsync("data:image/png;base64," + _base64)).Code);
        }

        [Fact]
        public async Task NormalDownloadsFromUrl()
        {
            var solver = MakeSolver();
            var url = $"http://{_server.Host}:{_server.Port}/image.png";
            Assert.Equal(MockServer.Code, (await solver.NormalAsync(url)).Code);
        }

        [Fact]
        public async Task NormalJsonSubmit()
        {
            // json=1 makes in.php return a JSON submit response; the SDK must parse it.
            var solver = MakeSolver();
            var result = await solver.NormalAsync(_base64, new Dictionary<string, object?> { ["json"] = 1 });
            Assert.Equal(MockServer.Code, result.Code);
        }

        [Fact]
        public async Task RecaptchaV2()
        {
            var solver = MakeSolver();
            Assert.Equal(MockServer.Code, (await solver.RecaptchaAsync(SiteKey, Url)).Code);
        }

        [Fact]
        public async Task RecaptchaV2Invisible()
        {
            var solver = MakeSolver();
            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?> { ["invisible"] = 1 });
            Assert.Equal(MockServer.Code, result.Code);
        }

        [Fact]
        public async Task RecaptchaV2Enterprise()
        {
            var solver = MakeSolver();
            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?> { ["enterprise"] = 1 });
            Assert.Equal(MockServer.Code, result.Code);
        }

        [Fact]
        public async Task RecaptchaV3()
        {
            var solver = MakeSolver();
            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["version"] = "v3",
                ["action"] = "submit",
                ["score"] = 0.7,
            });
            Assert.Equal(MockServer.Code, result.Code);
        }

        [Fact]
        public async Task RecaptchaProxy()
        {
            var solver = MakeSolver();
            var result = await solver.RecaptchaAsync(SiteKey, Url, new Dictionary<string, object?>
            {
                ["proxy"] = new Proxy("HTTPS", "user:pass@1.2.3.4:3128"),
            });
            Assert.Equal(MockServer.Code, result.Code);
        }

        [Fact]
        public async Task Turnstile()
        {
            var solver = MakeSolver();
            var result = await solver.TurnstileAsync(TurnstileSiteKey, Url);
            Assert.Equal(MockServer.Code, result.Code);
            Assert.Equal(MockServer.UserAgent, result.UserAgent);
        }

        [Fact]
        public async Task TurnstileChallengePage()
        {
            var solver = MakeSolver();
            var result = await solver.TurnstileAsync(TurnstileSiteKey, Url, new Dictionary<string, object?>
            {
                ["action"] = "managed",
                ["data"] = "cdata",
                ["pagedata"] = "chlpd",
            });
            Assert.Equal(MockServer.Code, result.Code);
            Assert.Equal(MockServer.UserAgent, result.UserAgent);
        }

        [Fact]
        public async Task PollingRetriesThenSolves()
        {
            var solver = MakeSolver();
            Assert.Equal(MockServer.Code, (await solver.RecaptchaAsync(SiteKey, Url + "/slow")).Code);
        }

        [Fact]
        public async Task PollingSurvivesEmptyResponses()
        {
            // Regression: CapSkip returns an empty body before a result is ready; the
            // SDK must keep polling instead of raising "cannot recognize response".
            var solver = MakeSolver();
            Assert.Equal(MockServer.Code, (await solver.RecaptchaAsync(SiteKey, Url + "/empty")).Code);
        }

        [Fact]
        public async Task ManualSendAndGetResult()
        {
            var solver = MakeSolver();
            var cid = await solver.SendAsync(new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
            });
            Assert.False(string.IsNullOrEmpty(cid));
            Assert.Equal(MockServer.Code, await solver.GetResultAsync(cid));
        }

        [Fact]
        public async Task TimeoutThrows()
        {
            var solver = MakeSolver(recaptchaTimeout: 2);
            await Assert.ThrowsAsync<TimeoutException>(() => solver.RecaptchaAsync(SiteKey, Url + "/never"));
        }

        [Fact]
        public async Task BadApiKeyThrows()
        {
            var solver = MakeSolver(apiKey: "badkey");
            await Assert.ThrowsAsync<ApiException>(() => solver.RecaptchaAsync(SiteKey, Url));
        }

        [Fact]
        public async Task ConnectionRefusedThrows()
        {
            var solver = new CapSkipClient(host: "127.0.0.1", port: 1, defaultTimeout: 2, pollingInterval: 1);
            await Assert.ThrowsAsync<NetworkException>(() => solver.SendAsync(new Dictionary<string, object?>
            {
                ["method"] = "userrecaptcha",
                ["googlekey"] = SiteKey,
                ["pageurl"] = Url,
            }));
        }

        [Fact]
        public async Task LowLevelApiClient()
        {
            var client = new ApiClient(_server.Host, _server.Port);
            var response = await client.InAsync(new Dictionary<string, object?>
            {
                ["method"] = "turnstile",
                ["key"] = "capskip",
                ["sitekey"] = TurnstileSiteKey,
                ["pageurl"] = Url,
            });
            Assert.StartsWith("OK|", response);

            var polled = await client.ResAsync(new Dictionary<string, object?>
            {
                ["key"] = "capskip",
                ["action"] = "get",
                ["id"] = response.Substring(3),
                ["json"] = 1,
            });
            Assert.Contains(MockServer.Code, polled);
        }

        [Fact]
        public async Task AltchaWithChallengeUrl()
        {
            var solver = MakeSolver();

            var result = await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = "https://example.com/captcha/api/altcha/challenge",
            });

            Assert.Equal(MockServer.AltchaToken, result.Code);
            Assert.Equal(MockServer.AltchaToken, result.Token);
            Assert.Equal(MockServer.AltchaNumber, result.Number);
            Assert.NotEmpty(result.CaptchaId);
        }

        [Fact]
        public async Task AltchaWithInlineChallengeDictionary()
        {
            // A dictionary has to reach the server as JSON, not as its type name, or
            // the server answers ERROR_BAD_PARAMETERS.
            var solver = MakeSolver();

            var result = await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_json"] = new Dictionary<string, object>
                {
                    ["algorithm"] = "SHA-256",
                    ["challenge"] = "3dd28253be6cc0c54d95f7f98c517e68",
                    ["salt"] = "46d5b1c8871e5152d902ee3f?expires=1893456000",
                    ["signature"] = "4b1cf0e0be0f4e5247e50b0f9a449830",
                    ["maxnumber"] = 1000000,
                },
            });

            Assert.Equal(MockServer.AltchaNumber, result.Number);
        }

        [Fact]
        public async Task ConcurrentSolves()
        {
            var solver = MakeSolver();
            var results = await Task.WhenAll(
                solver.RecaptchaAsync(SiteKey, Url),
                solver.TurnstileAsync(TurnstileSiteKey, Url));
            Assert.All(results, r => Assert.Equal(MockServer.Code, r.Code));
        }

        // -- Capy ------------------------------------------------------------

        [Fact]
        public async Task Capy()
        {
            var solver = MakeSolver();

            var result = await solver.CapyAsync(MockServer.CapyCaptchaKey, Url);

            Assert.Equal(MockServer.CapyCaptchaKey, result.CaptchaKey);
            Assert.Equal(MockServer.CapyChallengeKey, result.ChallengeKey);
            Assert.Equal(MockServer.CapyAnswer, result.Answer);
            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
        }

        /// <summary>
        /// The answer is the drag path the widget would have recorded; the target
        /// site verifies it against the challenge it issued, so any edit breaks it.
        /// </summary>
        [Fact]
        public async Task CapyAnswerCrossesTheWireUnchanged()
        {
            var solver = MakeSolver();

            var result = await solver.CapyAsync(
                MockServer.CapyCaptchaKey,
                Url,
                new Dictionary<string, object?> { ["api_server"] = "https://jp.api.capy.me/" });

            Assert.Equal(MockServer.CapyAnswer, result.Answer);
        }

        [Fact]
        public async Task CapyAvatarIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.CapyAsync(
                MockServer.CapyCaptchaKey,
                Url,
                new Dictionary<string, object?> { ["version"] = "avatar" }));
        }

        // -- CaptchaFox -------------------------------------------------------

        [Fact]
        public async Task CaptchaFox()
        {
            var solver = MakeSolver();

            var result = await solver.CaptchaFoxAsync("sk_xtNxpk6fCdFbxh1_xJeGflSdCE9tn99G", Url);

            Assert.Equal(MockServer.CaptchaFoxToken, result.Code);
            Assert.Equal(MockServer.CaptchaFoxToken, result.Token);
            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
        }

        /// <summary>
        /// Not the one sent: CapSkip solves in its own browser, and the token has to
        /// be submitted under the UA that minted it.
        /// </summary>
        [Fact]
        public async Task CaptchaFoxReportsTheBrowserUserAgent()
        {
            const string callerUa = "Mozilla/5.0 (the caller own UA)";
            var solver = MakeSolver();

            var result = await solver.CaptchaFoxAsync(
                "sk_x", Url, new Dictionary<string, object?> { ["useragent"] = callerUa });

            Assert.Equal(MockServer.CaptchaFoxUserAgent, result.UserAgent);
            Assert.NotEqual(callerUa, result.UserAgent);
        }

        [Fact]
        public async Task CaptchaFoxWithoutASitekeyIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.CaptchaFoxAsync("", Url));
        }

        // -- Friendly Captcha --------------------------------------------------

        [Fact]
        public async Task FriendlyCaptcha()
        {
            var solver = MakeSolver();

            var result = await solver.FriendlyCaptchaAsync(
                "FCMGEMUD2M567T8G", Url, new Dictionary<string, object?> { ["version"] = "v1" });

            Assert.Equal(MockServer.FriendlyCaptchaToken, result.Code);
            Assert.Equal(MockServer.FriendlyCaptchaToken, result.Token);
            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
        }

        /// <summary>
        /// The token carries base64 padding and slashes; form encoding must
        /// round-trip them, or the target site rejects a token that looks fine.
        /// </summary>
        [Fact]
        public async Task FriendlyCaptchaTokenSurvivesTheWireVerbatim()
        {
            var solver = MakeSolver();

            var result = await solver.FriendlyCaptchaAsync(
                "FCMGEMUD2M567T8G",
                Url,
                new Dictionary<string, object?>
                {
                    ["module_script"] = "https://cdn.example.com/site.min.js",
                });

            Assert.Equal(MockServer.FriendlyCaptchaToken, result.Token);
            Assert.Contains("/", result.Token!, StringComparison.Ordinal);
            Assert.Contains("=", result.Token!, StringComparison.Ordinal);
        }

        [Fact]
        public async Task FriendlyCaptchaBadVersionIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.FriendlyCaptchaAsync(
                "FCMGEMUD2M567T8G",
                Url,
                new Dictionary<string, object?> { ["version"] = "v3" }));
        }

        // -- AWS WAF -----------------------------------------------------------

        private const string WafKey =
            "AQIDAHjcYuEXAMPLEghicBgQ/7bFaQZ+m5FKCMDnO+vTbNg96AHDh0IR5vgzHNceHYqZR+GOAAAAfjB8";

        private const string WafIv = "CgAFRDIjfgAAA5yY";

        private const string WafContext =
            "Rbp3r8fvbuhmrCqpUe8Mh1TLvs8OSKJZiSs2vCF4dt9rxVZI7xYJa2n5k/9r+XaXe7KUIu2I8nXTDHHkDvIOTDc2";

        private const string WafChallengeJs =
            "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/challenge.js";

        private const string WafJsapi =
            "https://5b7e0a19c3d8.us-east-1.captcha-sdk.awswaf.com/5b7e0a19c3d8/jsapi.js";

        [Fact]
        public async Task AmazonWafChallengePage()
        {
            var solver = MakeSolver();

            var result = await solver.AmazonWafAsync(WafKey, Url, new Dictionary<string, object?>
            {
                ["iv"] = WafIv,
                ["context"] = WafContext,
                ["challenge_script"] = WafChallengeJs,
            });

            Assert.Equal(MockServer.AmazonWafCaptchaVoucher, result.CaptchaVoucher);
            Assert.Equal(MockServer.AmazonWafExistingToken, result.ExistingToken);
            Assert.Equal(MockServer.AmazonWafSolutionJson, result.Code);
            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
        }

        [Fact]
        public async Task AmazonWafWidgetPage()
        {
            var solver = MakeSolver();

            var result = await solver.AmazonWafAsync(
                WafKey + "_0_1", Url, new Dictionary<string, object?> { ["jsapiScript"] = WafJsapi });

            Assert.Equal(MockServer.AmazonWafCaptchaVoucher, result.CaptchaVoucher);
            Assert.Equal(MockServer.AmazonWafExistingToken, result.ExistingToken);
        }

        /// <summary>
        /// The keys carry +, / and =, which a form body has to URL-encode; a key
        /// mangled on the way would be a different firewall configuration.
        /// </summary>
        [Fact]
        public async Task AmazonWafKeysWithPlusSlashEqualsCrossTheWire()
        {
            var solver = MakeSolver();

            var result = await solver.AmazonWafAsync(WafKey + "==", Url, new Dictionary<string, object?>
            {
                ["iv"] = WafIv,
                ["context"] = WafContext,
            });

            var sent = _server.Submitted(result.CaptchaId);
            Assert.Equal(WafKey + "==", sent["sitekey"]);
            Assert.Equal(WafContext, sent["context"]);
            Assert.Equal(MockServer.AmazonWafExistingToken, result.ExistingToken);
        }

        [Fact]
        public async Task AmazonWafLoneIvIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync(
                WafKey, Url, new Dictionary<string, object?> { ["iv"] = WafIv }));
        }

        [Fact]
        public async Task AmazonWafOffAwsScriptIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AmazonWafAsync(
                WafKey,
                Url,
                new Dictionary<string, object?> { ["jsapiScript"] = "https://evil.example/jsapi.js" }));
        }

        // -- DataDome ----------------------------------------------------------

        private const string DdPage = "https://www.example.com/";

        private const string DdCaptchaUrl =
            "https://geo.captcha-delivery.com/captcha/?initialCid=AHrlqAAAAAMA7KBvCr0n4cEAG330MA%3D%3D"
            + "&hash=D013AA612AB2224D03B2318D0F5B19&cid=KzE6_pZBBTHdCdvZkauT9VAshi0wS7~v5_M_MacEtdw8"
            + "&t=fe&referer=https%3A%2F%2Fwww.example.com%2F&s=45225&e=549255a0289eaf17af60822309ab8db4&dm=cd";

        private const string DdUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/156.0.0.1 Safari/537.36";

        private static string DdCookiePair => MockServer.DataDomeCookie.Split(';')[0];

        /// <summary>
        /// No proxy sent: CapSkip solved through its own DataDome list, so the result
        /// names that proxy and the browser's User-Agent.
        /// </summary>
        [Fact]
        public async Task DataDomeWithoutProxyReportsTheListProxy()
        {
            var solver = MakeSolver();

            var result = await solver.DataDomeAsync(DdCaptchaUrl, DdPage);

            Assert.Equal(MockServer.DataDomeCookie, result.Code);
            Assert.Equal(DdCookiePair, result.Cookie);
            Assert.Equal(MockServer.DataDomeBrowserUserAgent, result.UserAgent);
            Assert.Equal(MockServer.DataDomeListProxy, result.Proxy);
            Assert.Equal(DdCaptchaUrl, _server.Submitted(result.CaptchaId)["captcha_url"]);
        }

        /// <summary>
        /// A proxy sent with the task: no proxy comes back, and the User-Agent is the
        /// one sent — it crossed the wire intact, ~ ; ( ) and all.
        /// </summary>
        [Fact]
        public async Task DataDomeWithOwnProxyAndUserAgent()
        {
            var solver = MakeSolver();

            var result = await solver.DataDomeAsync(DdCaptchaUrl, DdPage, new Dictionary<string, object?>
            {
                ["userAgent"] = DdUserAgent,
                ["proxy"] = new Proxy("HTTP", "login:p%40ss@203.0.113.7:3128"),
            });

            Assert.Equal(DdCookiePair, result.Cookie);
            Assert.Equal(DdUserAgent, result.UserAgent);
            Assert.Null(result.Proxy);
            Assert.Equal("login:p%40ss@203.0.113.7:3128", _server.Submitted(result.CaptchaId)["proxy"]);
        }

        [Fact]
        public async Task DataDomeBannedBlockPageIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.DataDomeAsync(DdCaptchaUrl.Replace("t=fe", "t=bv"), DdPage));
        }

        // -- hCaptcha ----------------------------------------------------------

        private const string HcSiteKey = "a5f74b19-9e45-40e0-b45d-47ff91b7a6c2";
        private const string HcPage = "https://accounts.hcaptcha.com/demo";

        [Fact]
        public async Task HCaptcha()
        {
            var solver = MakeSolver();

            var result = await solver.HCaptchaAsync(HcSiteKey, HcPage);

            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
            Assert.Equal(MockServer.HCaptchaToken, result.Code);
            Assert.Equal(MockServer.HCaptchaToken, result.Token);
            Assert.Equal(MockServer.HCaptchaRespKey, result.RespKey);
            Assert.Equal(MockServer.HCaptchaUserAgent, result.UserAgent);
        }

        /// <summary>
        /// invisible=true has to cross the wire as 1: the mock, like CapSkip, refuses
        /// anything else. rqdata carries + and /, and the proxy password %40; all
        /// must survive the form encoding.
        /// </summary>
        [Fact]
        public async Task HCaptchaWithEveryOption()
        {
            const string rqdata = "Wk2qT7bX+fV9rL0pZ3mN8cJ1hYsD6uE4gA5oR2iK/0=";
            var solver = MakeSolver();

            var result = await solver.HCaptchaAsync(HcSiteKey, HcPage, new Dictionary<string, object?>
            {
                ["invisible"] = true,
                ["data"] = rqdata,
                ["userAgent"] = "Mozilla/5.0 (caller)",
                ["proxy"] = new Proxy("SOCKS5H", "login:p%40ss@203.0.113.7:1080"),
            });

            Assert.Equal(MockServer.HCaptchaToken, result.Token);
            Assert.Equal(MockServer.HCaptchaRespKey, result.RespKey);

            var sent = _server.Submitted(result.CaptchaId);
            Assert.Equal("1", sent["invisible"]);
            Assert.Equal(rqdata, sent["data"]);
            Assert.Equal("Mozilla/5.0 (caller)", sent["useragent"]);
            Assert.Equal("login:p%40ss@203.0.113.7:1080", sent["proxy"]);
            Assert.Equal("SOCKS5H", sent["proxytype"]);
        }

        [Fact]
        public async Task HCaptchaTypedInvisibleOverload()
        {
            var solver = MakeSolver();

            var result = await solver.HCaptchaAsync(HcSiteKey, HcPage, invisible: true);

            Assert.Equal(MockServer.HCaptchaToken, result.Token);
            Assert.Equal("1", _server.Submitted(result.CaptchaId)["invisible"]);
        }

        [Fact]
        public async Task HCaptchaBadPageUrlIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.HCaptchaAsync(HcSiteKey, "accounts.hcaptcha.com/demo"));
        }

        [Fact]
        public async Task HCaptchaThroughTheAsyncAlias()
        {
            var solver = new AsyncCapSkip(host: _server.Host, port: _server.Port, pollingInterval: 1);

            var result = await solver.HCaptchaAsync(
                HcSiteKey, HcPage, new Dictionary<string, object?> { ["invisible"] = 1 });

            Assert.Equal(MockServer.HCaptchaToken, result.Token);
            Assert.Equal(MockServer.HCaptchaRespKey, result.RespKey);
            Assert.Equal(MockServer.HCaptchaUserAgent, result.UserAgent);
        }

        // -- MTCaptcha ---------------------------------------------------------

        private const string MtSiteKey = "MTPublic-KzqLY1cKH";
        private const string MtPage = "https://2captcha.com/demo/mtcaptcha";

        [Fact]
        public async Task MtCaptcha()
        {
            var solver = MakeSolver();

            var result = await solver.MtCaptchaAsync(MtSiteKey, MtPage);

            Assert.False(string.IsNullOrEmpty(result.CaptchaId));
            Assert.Equal(MockServer.MtCaptchaToken, result.Code);
            Assert.Equal(MockServer.MtCaptchaToken, result.Token);
            Assert.Equal("mt_captcha", _server.Submitted(result.CaptchaId)["method"]);
        }

        /// <summary>
        /// The sk= prefix has to be stripped before it reaches the mock, which, like
        /// CapSkip, refuses a sitekey that fails the server's test. The proxy
        /// password's %40 must survive the form encoding.
        /// </summary>
        [Fact]
        public async Task MtCaptchaWithEveryOption()
        {
            var solver = MakeSolver();

            var result = await solver.MtCaptchaAsync("sk=" + MtSiteKey, MtPage, new Dictionary<string, object?>
            {
                ["action"] = "login",
                ["userAgent"] = "Mozilla/5.0 (caller)",
                ["proxy"] = new Proxy("SOCKS5H", "login:p%40ss@203.0.113.7:1080"),
            });

            Assert.Equal(MockServer.MtCaptchaToken, result.Token);

            var sent = _server.Submitted(result.CaptchaId);
            Assert.Equal(MtSiteKey, sent["sitekey"]);
            Assert.Equal(MtPage, sent["pageurl"]);
            Assert.Equal("login", sent["action"]);
            Assert.Equal("Mozilla/5.0 (caller)", sent["useragent"]);
            Assert.Equal("login:p%40ss@203.0.113.7:1080", sent["proxy"]);
            Assert.Equal("SOCKS5H", sent["proxytype"]);
        }

        [Fact]
        public async Task MtCaptchaBadSitekeyIsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(() => solver.MtCaptchaAsync("KzqLY1cKH", MtPage));
        }

        [Fact]
        public async Task MtCaptchaSocks4IsRefusedLocally()
        {
            var solver = MakeSolver();

            await Assert.ThrowsAsync<ValidationException>(
                () => solver.MtCaptchaAsync(MtSiteKey, MtPage, new Dictionary<string, object?>
                {
                    ["proxy"] = new Proxy("SOCKS4", "203.0.113.7:1080"),
                }));
        }

        [Fact]
        public async Task MtCaptchaThroughTheAsyncAlias()
        {
            var solver = new AsyncCapSkip(host: _server.Host, port: _server.Port, pollingInterval: 1);

            var result = await solver.MtCaptchaAsync(
                MtSiteKey, MtPage, new Dictionary<string, object?> { ["action"] = "login" });

            Assert.Equal(MockServer.MtCaptchaToken, result.Code);
            Assert.Equal(MockServer.MtCaptchaToken, result.Token);
        }
    }
}
