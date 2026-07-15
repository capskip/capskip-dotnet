using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>
    /// Mock <see cref="ApiClient"/> that records the params it was sent and returns
    /// canned responses, mirroring <c>tests/abstract.py</c> from the Python SDK.
    /// </summary>
    public sealed class MockApiClient : ApiClient
    {
        public const string CaptchaId = "123";
        public const string Code = "abcd";

        /// <summary>The fields sent to <c>in.php</c> (excluding the files map).</summary>
        public IDictionary<string, object?> Incomings { get; private set; } = new Dictionary<string, object?>();

        /// <summary>The files map sent to <c>in.php</c>.</summary>
        public IDictionary<string, string> IncomingFiles { get; private set; } = new Dictionary<string, string>();

        public MockApiClient()
            : base("mock", 0)
        {
        }

        public override Task<string> InAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
        {
            var fields = new Dictionary<string, object?>(options);
            IDictionary<string, string> files = new Dictionary<string, string>();
            if (fields.TryGetValue("files", out var filesValue))
            {
                fields.Remove("files");
                if (filesValue is IDictionary<string, string> typed)
                {
                    files = typed;
                }
            }

            IncomingFiles = files;
            Incomings = fields;
            return Task.FromResult($"OK|{CaptchaId}");
        }

        public override Task<string> ResAsync(
            IDictionary<string, object?> query,
            CancellationToken cancellationToken = default)
        {
            if (query.TryGetValue("json", out var json) && (Equals(json, 1) || Equals(json, "1")))
            {
                return Task.FromResult(
                    $"{{\"status\":1,\"request\":\"{Code}\",\"useragent\":\"TestAgent/1.0\"}}");
            }

            return Task.FromResult($"OK|{Code}");
        }
    }

    /// <summary>Shared helpers for the unit tests that use <see cref="MockApiClient"/>.</summary>
    internal static class MockSolver
    {
        public static (CapSkipClient Solver, MockApiClient Api) Make()
        {
            var solver = new CapSkipClient(apiKey: "API_KEY", pollingInterval: 1);
            var api = new MockApiClient();
            solver.ApiClient = api;
            return (solver, api);
        }

        public static void AssertSent(MockApiClient api, IDictionary<string, object?> expected)
        {
            var want = new Dictionary<string, object?>(expected) { ["key"] = "API_KEY" };
            AssertDictEqual(want, api.Incomings);
        }

        public static void AssertResult(SolveResult result)
        {
            Assert.Equal(MockApiClient.CaptchaId, result.CaptchaId);
            Assert.Equal(MockApiClient.Code, result.Code);
        }

        /// <summary>Compare two field maps, treating numeric values by value (int/long/double).</summary>
        public static void AssertDictEqual(IDictionary<string, object?> expected, IDictionary<string, object?> actual)
        {
            var expectedKeys = expected.Keys.OrderBy(k => k, System.StringComparer.Ordinal);
            var actualKeys = actual.Keys.OrderBy(k => k, System.StringComparer.Ordinal);
            Assert.Equal(expectedKeys, actualKeys);

            foreach (var key in expected.Keys)
            {
                Assert.True(
                    ValueEquals(expected[key], actual[key]),
                    $"Field '{key}' differs: expected '{expected[key]}', got '{actual[key]}'");
            }
        }

        private static bool ValueEquals(object? a, object? b)
        {
            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            if (IsNumeric(a) && IsNumeric(b))
            {
                return System.Convert.ToDouble(a, System.Globalization.CultureInfo.InvariantCulture)
                       == System.Convert.ToDouble(b, System.Globalization.CultureInfo.InvariantCulture);
            }

            return a.Equals(b);
        }

        private static bool IsNumeric(object value) =>
            value is sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal;
    }
}
