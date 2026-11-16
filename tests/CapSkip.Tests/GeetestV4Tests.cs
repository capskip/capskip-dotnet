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
    /// Mock <see cref="ApiClient"/> answering as CapSkip's res.php does for GeeTest
    /// v4: <c>request</c> is the five values as a JSON string, after <c>OK|</c> or
    /// inside the json=1 envelope, where the server writes the inner quotes as
    /// <c>"</c>.
    /// </summary>
    public sealed class MockGeetestV4ApiClient : ApiClient
    {
        public const string CaptchaIdValue = "54088bb07d2df3c46b79f80300b0abbe";
        public const string LotNumber = "7c1a4f0e9b2d48a6b3e5f7d9c1a3b5e7";
        public const string PassToken = "2f6c0e4a8b1d5f9e3c7a0b4d8e2f6a1c5b9d3e7f0a4c8b2e6d1f5a9c3e7b0d4f8";
        public const string GenTime = "1793001600";
        public const string CaptchaOutput =
            "TM-rSt-9GZ_6s_5-dyAo3ZzQxZ3nhs8agozV1dXHM52rdKikcSbx6m9Z_vx1YtexMiouzC-83byvGlGzNTrsCk6EaDdzrugpzIevLISL7vRvwymvNEQ-NUfQM_PoUHAgWIs1w==";

        /// <summary>The answer as the server writes it: compact JSON, in this order.</summary>
        public static readonly string Code =
            "{\"captcha_id\":\"" + CaptchaIdValue + "\",\"lot_number\":\"" + LotNumber
            + "\",\"pass_token\":\"" + PassToken + "\",\"gen_time\":\"" + GenTime
            + "\",\"captcha_output\":\"" + CaptchaOutput + "\"}";

        private readonly string _code;
        private readonly string? _rawJson;

        /// <summary>The fields sent to <c>in.php</c>, or null when nothing was sent.</summary>
        public IDictionary<string, object?>? Incomings { get; private set; }

        /// <summary>The query of the last <c>res.php</c> poll.</summary>
        public IDictionary<string, object?>? Polled { get; private set; }

        /// <param name="code">The answer to put in <c>request</c>, or null for the usual one.</param>
        /// <param name="rawJson">The whole json=1 reply to give instead, or null.</param>
        public MockGeetestV4ApiClient(string? code = null, string? rawJson = null)
            : base("mock", 0)
        {
            _code = code ?? Code;
            _rawJson = rawJson;
        }

        /// <summary>The json=1 envelope with <c>request</c> as a string and its quotes as <c>"</c>.</summary>
        public static string Envelope(string code)
        {
            var body = "{\"status\":1,\"request\":\"" + code.Replace("\"", "\\u0022") + "\"}";
            return body;
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
                return Task.FromResult(_rawJson ?? Envelope(_code));
            }

            return Task.FromResult("OK|" + _code);
        }
    }

    public class GeetestV4Tests
    {
        private const string Url = "https://gt4.geetest.com/";
        private const string CaptchaId = MockGeetestV4ApiClient.CaptchaIdValue;

        private static CapSkipClient Make(
            out MockGeetestV4ApiClient client, string? code = null, string? rawJson = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            client = new MockGeetestV4ApiClient(code, rawJson);
            solver.ApiClient = client;
            return solver;
        }

        private static Task<SolveResult> Solve(
            CapSkipClient solver, IDictionary<string, object?>? options = null)
            => solver.GeetestV4Async(CaptchaId, Url, options);

        private static void AssertSent(MockGeetestV4ApiClient client, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            Assert.NotNull(client.Incomings);
            MockSolver.AssertDictEqual(want, client.Incomings!);
        }

        private static Dictionary<string, object?> Basic() => new Dictionary<string, object?>
        {
            ["method"] = "geetest_v4",
            ["captcha_id"] = CaptchaId,
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
                ["risk_type"] = "slide",
                ["proxy"] = new Dictionary<string, object?>
                {
                    ["type"] = "HTTPS",
                    ["uri"] = "login:password@203.0.113.7:3128",
                },
            });

            var want = Basic();
            want["risk_type"] = "slide";
            want["proxy"] = "login:password@203.0.113.7:3128";
            want["proxytype"] = "HTTPS";
            AssertSent(client, want);
        }

        /// <summary>The API documents riskType and also reads captchaId; both are accepted.</summary>
        [Fact]
        public async Task Camel_case_aliases_are_sent_under_the_canonical_names()
        {
            var solver = Make(out var client);

            await solver.GeetestV4Async(null!, Url, new Dictionary<string, object?>
            {
                ["captchaId"] = CaptchaId,
                ["riskType"] = "icon",
            });

            var want = Basic();
            want["risk_type"] = "icon";
            AssertSent(client, want);
        }

        [Fact]
        public async Task Pageurl_is_accepted_in_place_of_url()
        {
            var solver = Make(out var client);

            await solver.GeetestV4Async(CaptchaId, null!, new Dictionary<string, object?> { ["pageurl"] = Url });

            AssertSent(client, Basic());
        }

        [Fact]
        public async Task Captcha_id_and_risk_type_are_trimmed()
        {
            var solver = Make(out var client);

            await solver.GeetestV4Async("  " + CaptchaId + " ", Url, new Dictionary<string, object?>
            {
                ["risk_type"] = " slide\n",
            });

            var want = Basic();
            want["risk_type"] = "slide";
            AssertSent(client, want);
        }

        [Fact]
        public async Task Unset_and_empty_optionals_are_not_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["risk_type"] = "", ["proxy"] = null });
            AssertSent(client, Basic());

            await Solve(solver, new Dictionary<string, object?> { ["riskType"] = "   ", ["proxy"] = "" });
            AssertSent(client, Basic());
        }

        /// <summary>risk_type is free text: GeeTest adds types, so none is refused.</summary>
        [Fact]
        public async Task Any_risk_type_is_sent()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["risk_type"] = "some_future_type" });

            Assert.Equal("some_future_type", client.Incomings!["risk_type"]);
        }

        [Fact]
        public async Task A_bare_proxy_defaults_to_http()
        {
            var solver = Make(out var client);

            await Solve(solver, new Dictionary<string, object?> { ["proxy"] = "203.0.113.7:3128" });

            Assert.Equal("203.0.113.7:3128", client.Incomings!["proxy"]);
            Assert.Equal("HTTP", client.Incomings["proxytype"]);
        }

        // -- what is refused before it costs a round-trip -----------------

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        public async Task Missing_captcha_id_is_refused(string? captchaId)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.GeetestV4Async(captchaId!, Url));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task A_non_string_captcha_id_is_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.SolveAsync(new Dictionary<string, object?>
            {
                ["method"] = "geetest_v4",
                ["captcha_id"] = 12345,
                ["pageurl"] = Url,
            }));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData(null)]
        [InlineData("gt4.geetest.com/")]
        [InlineData("ftp://example.com/")]
        [InlineData("javascript:alert(1)")]
        [InlineData("https://")]
        public async Task Bad_pageurl_is_refused(string? url)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(() => solver.GeetestV4Async(CaptchaId, url!));
            Assert.Null(client.Incomings);
        }

        [Theory]
        [InlineData("gt", "abc")]
        [InlineData("challenge", "abc")]
        [InlineData("api_server", "api.geetest.com")]
        public async Task V3_parameters_are_refused(string name, string value)
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { [name] = value }));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task Unknown_parameter_is_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["userAgent"] = "Mozilla/5.0" }));
            Assert.Null(client.Incomings);
        }

        [Fact]
        public async Task Conflicting_aliases_are_refused()
        {
            var solver = Make(out var client);

            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?> { ["captchaId"] = "another-captcha-id" }));
            await Assert.ThrowsAsync<ValidationException>(
                () => Solve(solver, new Dictionary<string, object?>
                {
                    ["risk_type"] = "slide",
                    ["riskType"] = "icon",
                }));
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
        public async Task The_five_values_are_expanded()
        {
            var solver = Make(out _);

            var result = await Solve(solver);

            Assert.Equal(MockGeetestV4ApiClient.Code, result.Code);
            Assert.Equal(CaptchaId, result.GeetestCaptchaId);
            Assert.Equal(MockGeetestV4ApiClient.LotNumber, result.LotNumber);
            Assert.Equal(MockGeetestV4ApiClient.PassToken, result.PassToken);
            Assert.Equal(MockGeetestV4ApiClient.GenTime, result.GenTime);
            Assert.Equal(MockGeetestV4ApiClient.CaptchaOutput, result.CaptchaOutput);
            Assert.Null(result.IsOffline);
            Assert.Equal("123", result.CaptchaId);
            Assert.Null(result.Token);
            Assert.Null(result.Challenge);
        }

        [Fact]
        public void The_mock_writes_inner_quotes_as_u0022()
        {
            var envelope = MockGeetestV4ApiClient.Envelope(MockGeetestV4ApiClient.Code);

            Assert.Contains("\\u0022captcha_id\\u0022", envelope);
            Assert.DoesNotContain("\\\"", envelope);
        }

        [Fact]
        public async Task A_plain_text_answer_is_expanded()
        {
            var solver = Make(out var client);

            var result = await solver.SolveAsync(new Dictionary<string, object?>
            {
                ["method"] = "geetest_v4",
                ["captcha_id"] = CaptchaId,
                ["url"] = Url,
            });
            Assert.False(client.Polled!.ContainsKey("json"));
            Assert.Equal(MockGeetestV4ApiClient.Code, result.Code);

            ResponseParsing.ApplyGeetestV4Solution(result);
            Assert.Equal(MockGeetestV4ApiClient.PassToken, result.PassToken);
            Assert.Equal(MockGeetestV4ApiClient.CaptchaOutput, result.CaptchaOutput);
        }

        [Fact]
        public async Task An_offline_pass_is_reported()
        {
            var code = MockGeetestV4ApiClient.Code.TrimEnd('}') + ",\"isOffline\":true}";
            var solver = Make(out _, code);

            var result = await Solve(solver);

            Assert.True(result.IsOffline);
            Assert.Equal(MockGeetestV4ApiClient.LotNumber, result.LotNumber);
            Assert.Equal(code, result.Code);
        }

        /// <summary>Should the server ever put the object itself in request, it still parses.</summary>
        [Fact]
        public async Task An_object_request_is_expanded()
        {
            var rawJson = "{\"status\":1,\"request\":" + MockGeetestV4ApiClient.Code + "}";
            var solver = Make(out _, rawJson: rawJson);

            var result = await Solve(solver);

            Assert.Equal(MockGeetestV4ApiClient.CaptchaOutput, result.CaptchaOutput);
            Assert.Equal(MockGeetestV4ApiClient.GenTime, result.GenTime);
            using var doc = JsonDocument.Parse(result.Code);
            Assert.Equal(MockGeetestV4ApiClient.PassToken, doc.RootElement.GetProperty("pass_token").GetString());
        }

        [Fact]
        public async Task An_unparsable_answer_is_left_untouched()
        {
            var solver = Make(out _, "not-json");

            var result = await Solve(solver);

            Assert.Equal("not-json", result.Code);
            Assert.Null(result.LotNumber);
            Assert.Null(result.PassToken);
            Assert.Null(result.IsOffline);
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
        /// A real browser solve, like GeeTest v3, so it takes the longer timeout. The
        /// timeout never reaches the wire, so assert on the effect: a client whose
        /// reCAPTCHA budget has elapsed gives up, even though the default one has not.
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
            var client = new MockGeetestV4ApiClient();
            solver.ApiClient = client;

            var result = await solver.GeetestV4Async(CaptchaId, Url);

            AssertSent(client, Basic());
            Assert.Equal(MockGeetestV4ApiClient.PassToken, result.PassToken);
        }

        [Fact]
        public void ToString_shows_the_five_values()
        {
            var result = new SolveResult { CaptchaId = "123", Code = MockGeetestV4ApiClient.Code };
            ResponseParsing.ApplyGeetestV4Solution(result);

            var text = result.ToString();

            Assert.Contains("LotNumber = " + MockGeetestV4ApiClient.LotNumber, text);
            Assert.Contains("PassToken = " + MockGeetestV4ApiClient.PassToken, text);
        }
    }
}
