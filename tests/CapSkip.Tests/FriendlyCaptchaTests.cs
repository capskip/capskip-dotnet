using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>Mock <see cref="ApiClient"/> returning a realistic Friendly Captcha answer.</summary>
    public sealed class MockFriendlyCaptchaApiClient : ApiClient
    {
        /// <summary>A v1 token: four dot-separated parts, a few hundred characters.</summary>
        public const string V1Token =
            "c62c4da36bbaf7f253873035832709ef."
            + "aqwpWwdbzRWKY/UQAQwwpgAAAAAAAAAAM7hBvJOzqjc=."
            + "AAAAAArcCQABAAAAxv8QAAIAAACKYRgA.AgAB";

        private readonly string token;

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        public MockFriendlyCaptchaApiClient(string? token = null)
            : base("mock", 0)
        {
            this.token = token ?? V1Token;
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
                    "{\"status\":1,\"request\":\"" + token + "\","
                    + "\"solution\":{\"token\":\"" + token + "\"}}");
            }

            return Task.FromResult("OK|" + token);
        }
    }

    public class FriendlyCaptchaTests
    {
        private const string Url = "https://mysite.com/signup";
        private const string Sitekey = "FCMGEMUD2M567T8G";

        private const string V1ModuleScript =
            "https://cdn.example.com/friendly-challenge@0.9.19/widget.module.min.js";

        private const string V2ModuleScript =
            "https://cdn.example.com/@friendlycaptcha/sdk@0.1.6/site.min.js";

        private static CapSkipClient Make(out MockFriendlyCaptchaApiClient client, string? token = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockFriendlyCaptchaApiClient(token);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.FriendlyCaptchaAsync(Sitekey, Url, options);

        // -- what goes out ------------------------------------------------

        [Fact]
        public async Task Submits_the_documented_parameters()
        {
            var solver = Make(out var client);

            var result = await Solve(solver);

            Assert.Equal("friendly_captcha", client.Incomings["method"]);
            Assert.Equal(Sitekey, client.Incomings["sitekey"]);
            Assert.Equal(Url, client.Incomings["pageurl"]);
            Assert.Equal("123", result.CaptchaId);
        }

        /// <summary>
        /// The server accepts friendlycaptcha too, but friendly_captcha is what the
        /// parameter table documents and what 2Captcha's own SDKs send.
        /// </summary>
        [Fact]
        public async Task Uses_the_documented_underscore_method_spelling()
        {
            var solver = Make(out var client);

            await Solve(solver);

            Assert.Equal("friendly_captcha", client.Incomings["method"]);
        }

        [Fact]
        public async Task Forwards_an_explicit_version()
        {
            var solver = Make(out var client);

            foreach (var version in new[] { "v1", "v2" })
            {
                await Solve(solver, new Dictionary<string, object?> { ["version"] = version });
                Assert.Equal(version, client.Incomings["version"]);
            }
        }

        /// <summary>The server takes a bare 1 or 2 as well as v1/v2.</summary>
        [Fact]
        public async Task Accepts_a_bare_digit_version()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["version"] = 2 });

            Assert.Equal(2, client.Incomings["version"]);
        }

        /// <summary>
        /// The script URL is the most reliable version signal there is: it is the
        /// build the site actually loads.
        /// </summary>
        [Fact]
        public async Task Forwards_the_widget_script_urls()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["module_script"] = V2ModuleScript });
            Assert.Equal(V2ModuleScript, client.Incomings["module_script"]);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["moduleScript"] = V1ModuleScript,
                ["nomoduleScript"] = "https://cdn.example.com/widget.min.js",
            });
            Assert.Equal(V1ModuleScript, client.Incomings["module_script"]);
            Assert.Equal("https://cdn.example.com/widget.min.js", client.Incomings["nomodule_script"]);
        }

        /// <summary>
        /// Both tenants mint a token for the same sitekey, so the wrong one is only
        /// caught by the target site's own verification.
        /// </summary>
        [Fact]
        public async Task Forwards_the_eu_tenant()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["api_server"] = "eu" });

            Assert.Equal("eu", client.Incomings["api_server"]);
        }

        [Fact]
        public async Task Does_not_send_an_unset_optional()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?>
            {
                ["version"] = null,
                ["module_script"] = null,
                ["api_server"] = null,
            });

            Assert.False(client.Incomings.ContainsKey("version"));
            Assert.False(client.Incomings.ContainsKey("module_script"));
            Assert.False(client.Incomings.ContainsKey("api_server"));
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

        // -- what is refused before it costs a round-trip -----------------

        /// <summary>
        /// Solving the wrong version returns a well-formed token the site rejects, so
        /// a typo must not reach the server as a silent default.
        /// </summary>
        [Fact]
        public async Task Refuses_an_unknown_version_locally()
        {
            var solver = Make(out _);

            var error = await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["version"] = "v3" }));

            Assert.Contains("v1", error.Message, StringComparison.Ordinal);
        }

        [Fact]
        public async Task Refuses_a_missing_sitekey_or_pageurl()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(() => solver.FriendlyCaptchaAsync("", Url));
            await Assert.ThrowsAsync<ValidationException>(() => solver.FriendlyCaptchaAsync(Sitekey, ""));
        }

        [Fact]
        public async Task Refuses_an_unknown_parameter()
        {
            var solver = Make(out _);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["challenge_url"] = "https://x" }));
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

            Assert.Equal(MockFriendlyCaptchaApiClient.V1Token, result.Token);
            Assert.Equal(MockFriendlyCaptchaApiClient.V1Token, result.Code);
        }

        /// <summary>
        /// A v1 token's dot-separated parts include base64 padding and slashes;
        /// nothing may trim or re-encode them.
        /// </summary>
        [Fact]
        public async Task Passes_the_token_through_verbatim()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(4, result.Token!.Split('.').Length);
            Assert.Contains("/", result.Token, StringComparison.Ordinal);
            Assert.Contains("=", result.Token, StringComparison.Ordinal);
        }

        /// <summary>A v2 token is a single opaque string of roughly six kilobytes.</summary>
        [Fact]
        public async Task Carries_a_large_v2_token_intact()
        {
            var v2Token = "AQQA." + new string('a', 6000);
            var solver = Make(out _, token: v2Token);

            var result = await Solve(solver, new Dictionary<string, object?> { ["version"] = "v2" });

            Assert.Equal(v2Token, result.Token);
            Assert.Equal(v2Token.Length, result.Token!.Length);
        }
    }
}
