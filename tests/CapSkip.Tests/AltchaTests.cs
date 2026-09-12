using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> returning a realistic ALTCHA answer — the base64
    /// token carried in <c>request</c>, alongside the createTask-shaped solution
    /// object CapSkip also emits.
    /// </summary>
    public sealed class MockAltchaApiClient : ApiClient
    {
        public const int Number = 9661;

        public static readonly Dictionary<string, object> ChallengeDoc = new Dictionary<string, object>
        {
            ["algorithm"] = "SHA-256",
            ["challenge"] = "3dd28253be6cc0c54d95f7f98c517e68",
            ["salt"] = "46d5b1c8871e5152d902ee3f?expires=1893456000",
            ["signature"] = "4b1cf0e0be0f4e5247e50b0f9a449830",
            ["maxnumber"] = 1000000,
        };

        public static readonly string ChallengeJson = JsonSerializer.Serialize(ChallengeDoc);

        /// <summary>Base64 of the solved challenge document, with the winning counter.</summary>
        public static readonly string Token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["algorithm"] = "SHA-256",
                ["challenge"] = "3dd28253be6cc0c54d95f7f98c517e68",
                ["number"] = Number,
                ["salt"] = "46d5b1c8871e5152d902ee3f?expires=1893456000",
                ["signature"] = "4b1cf0e0be0f4e5247e50b0f9a449830",
            })));

        private readonly string request;

        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        public MockAltchaApiClient(string? request = null)
            : base("mock", 0)
        {
            this.request = request ?? Token;
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
                var payload = JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["status"] = 1,
                    ["request"] = request,
                    ["solution"] = new Dictionary<string, object>
                    {
                        ["token"] = request,
                        ["number"] = Number,
                    },
                });
                return Task.FromResult(payload);
            }

            return Task.FromResult($"OK|{request}");
        }
    }

    /// <summary>Mock <see cref="ApiClient"/> whose poll returns exactly the JSON it was given.</summary>
    public sealed class RawAltchaApiClient : ApiClient
    {
        private readonly string payload;

        public RawAltchaApiClient(string payload)
            : base("mock", 0)
        {
            this.payload = payload;
        }

        public override Task<string> InAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
            => Task.FromResult("OK|123");

        public override Task<string> ResAsync(
            IDictionary<string, object?> query,
            CancellationToken cancellationToken = default)
            => Task.FromResult(payload);
    }

    public class AltchaTests
    {
        public const int V2Number = 47;

        /// <summary>
        /// A PoW v2 answer is shaped completely differently: no top-level
        /// <c>number</c>, and the counter sits at <c>solution.counter</c>. Captured
        /// from a real PBKDF2/SHA-256 deployment (captcha.seventy9.co.uk), the
        /// scheme altcha.org documents today.
        /// </summary>
        public static readonly string V2Token = Convert.ToBase64String(
            Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["challenge"] = new Dictionary<string, object>
                {
                    ["parameters"] = new Dictionary<string, object>
                    {
                        ["algorithm"] = "PBKDF2/SHA-256",
                        ["cost"] = 50000,
                        ["expiresAt"] = 1789224090,
                        ["keyLength"] = 32,
                        ["keyPrefix"] = "00",
                        ["nonce"] = "634c4f591fd086beb40d67312b85808a",
                        ["salt"] = "511e1c75edbf295278c9bfb68191053c",
                    },
                    ["signature"] = "9197e4a35ebff399d669e747c7c5e6ab079b30fe3437df268dc7caf34cf9e281",
                },
                ["solution"] = new Dictionary<string, object>
                {
                    ["counter"] = V2Number,
                    ["derivedKey"] = "0099db7cb36864d8875ff8305c9a3d2649b1f72cb774de1c",
                },
            })));

        private static CapSkipClient MakeRaw(string payload)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            solver.ApiClient = new RawAltchaApiClient(payload);
            return solver;
        }

        private static Task<SolveResult> SolveRaw(CapSkipClient solver) => solver.AltchaAsync(
            Url, new Dictionary<string, object?> { ["challenge_url"] = ChallengeUrl });

        [Fact]
        public async Task ExposesTheCounterForAProofOfWorkV2Answer()
        {
            // A v2 token carries no top-level `number` — the counter is at
            // `solution.counter`, and the server reports it as `solution.number`
            // in the poll payload. Reading only the token's own `number` silently
            // drops it for every PBKDF2 site, the scheme ALTCHA recommends.
            var solver = MakeRaw(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["status"] = 1,
                ["request"] = V2Token,
                ["solution"] = new Dictionary<string, object>
                {
                    ["token"] = V2Token,
                    ["number"] = V2Number,
                },
            }));

            var result = await SolveRaw(solver);

            Assert.Equal(V2Token, result.Token);
            Assert.Equal(V2Number, result.Number);
        }

        [Fact]
        public async Task RecoversAV2CounterFromTheTokenWithoutASolution()
        {
            var solver = MakeRaw(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["status"] = 1,
                ["request"] = V2Token,
            }));

            var result = await SolveRaw(solver);

            Assert.Equal(V2Number, result.Number);
        }

        [Fact]
        public async Task TrustsTheServerCounterOverAnUnreadableToken()
        {
            // If the two ever disagree, the server worked the answer out and the
            // decode is only an inference from it.
            var solver = MakeRaw(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["status"] = 1,
                ["request"] = "not-base64-json",
                ["solution"] = new Dictionary<string, object>
                {
                    ["token"] = "not-base64-json",
                    ["number"] = 512,
                },
            }));

            var result = await SolveRaw(solver);

            Assert.Equal(512, result.Number);
        }

        private const string Url = "https://mysite.com/signup";
        private const string ChallengeUrl = "https://mysite.com/captcha/api/altcha/challenge";

        private static (CapSkipClient Solver, MockAltchaApiClient Api) Make(string? request = null)
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            var api = new MockAltchaApiClient(request);
            solver.ApiClient = api;
            return (solver, api);
        }

        private static void AssertSent(MockAltchaApiClient api, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            MockSolver.AssertDictEqual(want, api.Incomings);
        }

        [Fact]
        public async Task Basic()
        {
            var (solver, api) = Make();

            var result = await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_url"] = ChallengeUrl,
            });
            Assert.Equal("123", result.CaptchaId);
        }

        [Fact]
        public async Task ChallengeJsonString()
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_json"] = MockAltchaApiClient.ChallengeJson,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_json"] = MockAltchaApiClient.ChallengeJson,
            });
        }

        [Fact]
        public async Task ChallengeJsonAcceptsADictionary()
        {
            // The form body can only carry a string, so a document passed as a
            // dictionary has to be serialized rather than ToString()'d into
            // "System.Collections.Generic.Dictionary`2[...]".
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_json"] = MockAltchaApiClient.ChallengeDoc,
            });

            var sent = Assert.IsType<string>(api.Incomings["challenge_json"]);
            using var doc = JsonDocument.Parse(sent);
            Assert.Equal(
                "3dd28253be6cc0c54d95f7f98c517e68",
                doc.RootElement.GetProperty("challenge").GetString());
        }

        [Fact]
        public async Task CamelCaseAliases()
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challengeUrl"] = ChallengeUrl,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_url"] = ChallengeUrl,
            });
        }

        [Fact]
        public async Task ChallengeJsonCamelCaseAlias()
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challengeJSON"] = MockAltchaApiClient.ChallengeJson,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_json"] = MockAltchaApiClient.ChallengeJson,
            });
        }

        [Fact]
        public async Task BothChallengeParamsAreAllowed()
        {
            // CapSkip is deliberately more permissive than 2Captcha here: sending
            // both is not an error, the inline document simply wins.
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
                ["challenge_json"] = MockAltchaApiClient.ChallengeJson,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_url"] = ChallengeUrl,
                ["challenge_json"] = MockAltchaApiClient.ChallengeJson,
            });
        }

        [Fact]
        public async Task NullChallengeParamIsDropped()
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
                ["challenge_json"] = null,
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_url"] = ChallengeUrl,
            });
        }

        [Fact]
        public async Task Proxy()
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
                ["proxy"] = new Proxy("HTTP", "1.2.3.4:3128"),
            });

            AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "altcha",
                ["pageurl"] = Url,
                ["challenge_url"] = ChallengeUrl,
                ["proxy"] = "1.2.3.4:3128",
                ["proxytype"] = "HTTP",
            });
        }

        [Fact]
        public async Task ExposesTokenAndNumber()
        {
            var (solver, _) = Make();

            var result = await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
            });

            Assert.Equal(MockAltchaApiClient.Token, result.Code);
            Assert.Equal(MockAltchaApiClient.Token, result.Token);
            Assert.Equal(MockAltchaApiClient.Number, result.Number);
        }

        [Fact]
        public async Task UndecodableAnswerIsLeftAlone()
        {
            // No `solution` object either — a server returning something that is
            // not a token has no counter to report, so nothing to fall back on.
            var solver = MakeRaw(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["status"] = 1,
                ["request"] = "not-base64-json",
            }));

            var result = await SolveRaw(solver);

            Assert.Equal("not-base64-json", result.Code);
            Assert.Null(result.Number);
        }

        [Fact]
        public async Task UsesTheDefaultTimeoutNotTheRecaptchaOne()
        {
            // ALTCHA is CPU proof-of-work measured in milliseconds, not a browser
            // solve, so it must not inherit reCAPTCHA's much longer budget. The
            // timeout never reaches the wire, so assert on the effect: a client
            // whose default timeout has elapsed gives up, even though the much
            // longer reCAPTCHA budget has not.
            var solver = new CapSkipClient(
                apiKey: "API_KEY", defaultTimeout: 0.05, recaptchaTimeout: 600, pollingInterval: 0.01);
            solver.ApiClient = new NeverReadyApiClient();

            await Assert.ThrowsAsync<TimeoutException>(() => solver.AltchaAsync(
                Url, new Dictionary<string, object?> { ["challenge_url"] = ChallengeUrl }));
        }

        [Fact]
        public async Task MissingUrlThrows()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(
                string.Empty, new Dictionary<string, object?> { ["challenge_url"] = ChallengeUrl }));
        }

        [Fact]
        public async Task MissingBothChallengeParamsThrows()
        {
            // CapSkip answers ERROR_BAD_PARAMETERS; fail locally instead of paying
            // for the round-trip.
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(Url));
        }

        [Fact]
        public async Task EmptyChallengeParamsThrow()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(
                Url,
                new Dictionary<string, object?>
                {
                    ["challenge_url"] = string.Empty,
                    ["challenge_json"] = string.Empty,
                }));
        }

        [Fact]
        public async Task UnsupportedParameterThrows()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(
                Url,
                new Dictionary<string, object?>
                {
                    ["challenge_url"] = ChallengeUrl,
                    ["sitekey"] = "not-an-altcha-param",
                }));
        }

        [Theory]
        [InlineData("HTTP")]
        [InlineData("HTTPS")]
        [InlineData("SOCKS5")]
        [InlineData("SOCKS5H")]
        [InlineData("socks5h")]
        public async Task AcceptedProxyTypes(string proxytype)
        {
            var (solver, api) = Make();

            await solver.AltchaAsync(Url, new Dictionary<string, object?>
            {
                ["challenge_url"] = ChallengeUrl,
                ["proxy"] = new Proxy(proxytype, "1.2.3.4:3128"),
            });

            Assert.Equal(proxytype, api.Incomings["proxytype"]);
        }

        [Fact]
        public async Task Socks4IsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(
                Url,
                new Dictionary<string, object?>
                {
                    ["challenge_url"] = ChallengeUrl,
                    ["proxy"] = new Proxy("SOCKS4", "1.2.3.4:3128"),
                }));
        }

        [Fact]
        public async Task UnknownProxyTypeIsRejected()
        {
            var (solver, _) = Make();

            await Assert.ThrowsAsync<ValidationException>(() => solver.AltchaAsync(
                Url,
                new Dictionary<string, object?>
                {
                    ["challenge_url"] = ChallengeUrl,
                    ["proxy"] = "1.2.3.4:3128",
                    ["proxytype"] = "FTP",
                }));
        }
    }

    /// <summary>Never returns a solution, so a solve can only end in a timeout.</summary>
    public sealed class NeverReadyApiClient : ApiClient
    {
        public NeverReadyApiClient()
            : base("mock", 0)
        {
        }

        public override Task<string> InAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
            => Task.FromResult("OK|123");

        public override Task<string> ResAsync(
            IDictionary<string, object?> query,
            CancellationToken cancellationToken = default)
        {
            // ALTCHA polls with json=1, so answer in the shape that mode expects.
            if (query.TryGetValue("json", out var json) && (Equals(json, 1) || Equals(json, "1")))
            {
                return Task.FromResult("{\"status\":0,\"request\":\"CAPCHA_NOT_READY\"}");
            }

            return Task.FromResult("CAPCHA_NOT_READY");
        }
    }
}
