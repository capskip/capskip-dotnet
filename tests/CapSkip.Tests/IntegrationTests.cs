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
        public async Task ConcurrentSolves()
        {
            var solver = MakeSolver();
            var results = await Task.WhenAll(
                solver.RecaptchaAsync(SiteKey, Url),
                solver.TurnstileAsync(TurnstileSiteKey, Url));
            Assert.All(results, r => Assert.Equal(MockServer.Code, r.Code));
        }
    }
}
