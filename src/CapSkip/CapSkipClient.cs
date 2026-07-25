using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CapSkip
{
    /// <summary>
    /// Client for the CapSkip local captcha solver (image CAPTCHA, reCAPTCHA v2/v3,
    /// Cloudflare Turnstile, and GeeTest v3). Every solve method is asynchronous and
    /// returns a <see cref="SolveResult"/>.
    /// </summary>
    public class CapSkipClient
    {
        // First poll fires this soon after submitting (in seconds), then the interval
        // backs off (doubling) up to the configured PollingInterval ceiling. Keeps
        // latency low for fast local solves (e.g. image captchas) without hammering
        // on slow ones.
        internal const double InitialPollingInterval = 0.25;

        /// <summary>The installed SDK version.</summary>
        public static readonly string Version = "1.1.0";

        /// <summary>CapSkip API key sent with every request.</summary>
        public string ApiKey { get; }

        /// <summary>Seconds to poll an image captcha before timing out.</summary>
        public double DefaultTimeout { get; }

        /// <summary>Seconds to poll reCAPTCHA / Turnstile before timing out.</summary>
        public double RecaptchaTimeout { get; }

        /// <summary>Max seconds between polls; starts at 0.25 and backs off to this.</summary>
        public double PollingInterval { get; }

        /// <summary>The low-level HTTP client. Replaceable to support testing.</summary>
        public ApiClient ApiClient { get; set; }

        /// <summary>The base error type for all SDK failures (cross-SDK parity handle).</summary>
        public Type Exceptions { get; } = typeof(CapSkipError);

        /// <summary>Create a client for the CapSkip local captcha solver.</summary>
        /// <param name="apiKey">CapSkip API key (any string when key validation is disabled).</param>
        /// <param name="host">CapSkip host.</param>
        /// <param name="port">CapSkip port from the app settings.</param>
        /// <param name="defaultTimeout">Seconds to poll an image captcha before timing out.</param>
        /// <param name="recaptchaTimeout">Seconds to poll reCAPTCHA / Turnstile before timing out.</param>
        /// <param name="pollingInterval">Max seconds between polls (starts at 0.25 and backs off to this).</param>
        public CapSkipClient(
            string apiKey = "capskip",
            string host = "127.0.0.1",
            int port = 8080,
            double defaultTimeout = 120,
            double recaptchaTimeout = 300,
            double pollingInterval = 5)
        {
            ApiKey = apiKey;
            DefaultTimeout = defaultTimeout;
            RecaptchaTimeout = recaptchaTimeout;
            PollingInterval = pollingInterval;
            ApiClient = new ApiClient(host, port);
        }

        /// <summary>
        /// Solve an image captcha from a file path, remote URL, base64 string, or
        /// data-URI. Only <c>json</c> is accepted as an extra option.
        /// </summary>
        public async Task<SolveResult> NormalAsync(
            string file,
            IDictionary<string, object?>? options = null,
            CancellationToken cancellationToken = default)
        {
            var opts = Clone(options);
            var unsupported = opts.Keys
                .Where(key => key != "json")
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
            if (unsupported.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for image captcha: {ReprList(unsupported)}. "
                    + "Only json is supported besides the image input.");
            }

            var method = await GetMethodAsync(file, cancellationToken).ConfigureAwait(false);
            foreach (var kv in opts)
            {
                method[kv.Key] = kv.Value;
            }

            return await SolveAsync(method, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Solve reCAPTCHA v2/v3 (invisible, enterprise, proxy).</summary>
        public Task<SolveResult> RecaptchaAsync(
            string sitekey,
            string url,
            IDictionary<string, object?>? options = null,
            CancellationToken cancellationToken = default)
        {
            var opts = Clone(options);
            var version = Pop(opts, "version", "v2");
            var enterprise = Pop(opts, "enterprise", 0);

            var pars = new Dictionary<string, object?>
            {
                ["googlekey"] = sitekey,
                ["url"] = url,
                ["method"] = "userrecaptcha",
                ["enterprise"] = enterprise,
            };
            foreach (var kv in opts)
            {
                pars[kv.Key] = kv.Value;
            }

            if (Http.Stringify(version).ToLowerInvariant() == "v3")
            {
                pars["version"] = "v3";
            }

            var solveOptions = new Dictionary<string, object?> { ["timeout"] = RecaptchaTimeout };
            foreach (var kv in pars)
            {
                solveOptions[kv.Key] = kv.Value;
            }

            return SolveAsync(solveOptions, cancellationToken);
        }

        /// <summary>Solve Cloudflare Turnstile (widget or challenge page).</summary>
        public Task<SolveResult> TurnstileAsync(
            string sitekey,
            string url,
            IDictionary<string, object?>? options = null,
            CancellationToken cancellationToken = default)
        {
            var pars = new Dictionary<string, object?>
            {
                ["sitekey"] = sitekey,
                ["url"] = url,
            };
            foreach (var kv in Clone(options))
            {
                pars[kv.Key] = kv.Value;
            }

            pars["method"] = "turnstile";
            pars["poll_json"] = 1;

            return SolveAsync(pars, cancellationToken);
        }

        /// <summary>Solve a GeeTest v3 slider.</summary>
        /// <param name="gt">Static per-site GeeTest id.</param>
        /// <param name="challenge">
        /// Single-use challenge token. It expires in about a minute, so fetch a fresh
        /// <paramref name="gt"/>/<paramref name="challenge"/> pair immediately before
        /// calling this.
        /// </param>
        /// <param name="url">Full URL of the page the captcha appears on.</param>
        /// <param name="options">Optional <c>api_server</c> server-domain override and <c>proxy</c>.</param>
        /// <param name="cancellationToken">Cancels the submit and the polling loop.</param>
        /// <returns>
        /// A result whose <see cref="SolveResult.Code"/> is the raw JSON answer, plus the
        /// parsed <see cref="SolveResult.Challenge"/>, <see cref="SolveResult.Validate"/>,
        /// and <see cref="SolveResult.Seccode"/> fields to post back to the target site.
        /// </returns>
        public async Task<SolveResult> GeetestAsync(
            string gt,
            string challenge,
            string url,
            IDictionary<string, object?>? options = null,
            CancellationToken cancellationToken = default)
        {
            // Like reCAPTCHA, this is a real browser solve (load, slide, verify) and can
            // retry internally, so it gets the longer of the two timeouts unless the
            // caller asked for a specific one.
            var pars = new Dictionary<string, object?>
            {
                ["timeout"] = RecaptchaTimeout,
                ["gt"] = gt,
                ["challenge"] = challenge,
                ["url"] = url,
            };
            foreach (var kv in Clone(options))
            {
                pars[kv.Key] = kv.Value;
            }

            pars["method"] = "geetest";
            pars["poll_json"] = 1;

            var result = await SolveAsync(pars, cancellationToken).ConfigureAwait(false);
            return ResponseParsing.ApplyGeetestSolution(result);
        }

        /// <summary>Submit then poll to completion. Used by the higher-level solve methods.</summary>
        public async Task<SolveResult> SolveAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
        {
            var opts = Clone(options);
            var timeout = ToDouble(Pop(opts, "timeout", 0.0));
            var pollingInterval = ToDouble(Pop(opts, "polling_interval", 0.0));
            var pollJson = ToInt(Pop(opts, "poll_json", 0));

            var captchaId = await SendAsync(opts, cancellationToken).ConfigureAwait(false);
            var result = new SolveResult { CaptchaId = captchaId };
            var solveTimeout = timeout != 0 ? timeout : DefaultTimeout;
            var interval = pollingInterval != 0 ? pollingInterval : PollingInterval;
            var polled = await WaitResultAsync(captchaId, solveTimeout, interval, pollJson, cancellationToken)
                .ConfigureAwait(false);
            return ResponseParsing.ApplyPollResult(result, polled);
        }

        /// <summary>Poll until solved or the timeout (seconds) elapses.</summary>
        public async Task<object> WaitResultAsync(
            string id,
            double timeout,
            double pollingInterval,
            int json = 0,
            CancellationToken cancellationToken = default)
        {
            var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(timeout);
            var interval = Math.Min(InitialPollingInterval, pollingInterval);
            while (DateTime.UtcNow < deadline)
            {
                try
                {
                    return await GetResultAsync(id, json, cancellationToken).ConfigureAwait(false);
                }
                catch (NetworkException)
                {
                    await Task.Delay(TimeSpan.FromSeconds(interval), cancellationToken).ConfigureAwait(false);
                    interval = NextPollInterval(interval, pollingInterval);
                }
            }

            throw new TimeoutException($"timeout {timeout.ToString(CultureInfo.InvariantCulture)} exceeded");
        }

        /// <summary>Resolve an image input into the <c>in.php</c> method/body/file fields.</summary>
        public async Task<IDictionary<string, object?>> GetMethodAsync(
            string file,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(file))
            {
                throw new ValidationException("File required");
            }

            if (file.StartsWith("data:", StringComparison.Ordinal))
            {
                var comma = file.IndexOf(',');
                var body = comma >= 0 ? file.Substring(comma + 1) : file;
                return new Dictionary<string, object?> { ["method"] = "base64", ["body"] = body };
            }

            if (file.IndexOf('.') < 0 && file.Length > 50)
            {
                return new Dictionary<string, object?> { ["method"] = "base64", ["body"] = file };
            }

            if (file.StartsWith("http", StringComparison.Ordinal))
            {
                var resp = await Http.GetAsync(file, null, cancellationToken).ConfigureAwait(false);
                if (resp.StatusCode != 200)
                {
                    throw new ValidationException($"File could not be downloaded from url: {file}");
                }

                return new Dictionary<string, object?>
                {
                    ["method"] = "base64",
                    ["body"] = Convert.ToBase64String(resp.Body),
                };
            }

            if (!File.Exists(file))
            {
                throw new ValidationException($"File not found: {file}");
            }

            return new Dictionary<string, object?> { ["method"] = "post", ["file"] = file };
        }

        /// <summary>Submit a captcha without polling; returns the captcha id.</summary>
        public async Task<string> SendAsync(
            IDictionary<string, object?> parameters,
            CancellationToken cancellationToken = default)
        {
            var withKey = Clone(parameters);
            withKey["key"] = ApiKey;
            var prepared = PrepareSendParams(withKey);

            object? files = null;
            if (prepared.TryGetValue("files", out var f))
            {
                files = f;
                prepared.Remove("files");
            }

            var options = new Dictionary<string, object?>(prepared)
            {
                ["files"] = files ?? new Dictionary<string, string>(),
            };

            var response = await ApiClient.InAsync(options, cancellationToken).ConfigureAwait(false);
            return ResponseParsing.ParseSubmitResponse(response);
        }

        /// <summary>Poll a result once; throws <see cref="NetworkException"/> while not ready.</summary>
        /// <returns>The token <see cref="string"/>, or a parsed object when <paramref name="json"/> is set.</returns>
        public async Task<object> GetResultAsync(
            string id,
            int json = 0,
            CancellationToken cancellationToken = default)
        {
            var query = new Dictionary<string, object?>
            {
                ["key"] = ApiKey,
                ["action"] = "get",
                ["id"] = id,
            };
            if (json != 0)
            {
                query["json"] = 1;
            }

            var response = await ApiClient.ResAsync(query, cancellationToken).ConfigureAwait(false);
            return ResponseParsing.ParsePollResponse(response, json != 0 ? 1 : 0);
        }

        private Dictionary<string, object?> PrepareSendParams(IDictionary<string, object?> parameters)
        {
            var method = parameters.TryGetValue("method", out var m) ? m as string : null;

            if (method == "post" || method == "base64")
            {
                return ApiParams.PrepareSubmitParams(parameters, "normal");
            }

            if (method == "userrecaptcha")
            {
                var version = parameters.TryGetValue("version", out var v) ? v as string ?? "v2" : "v2";
                return ApiParams.PrepareSubmitParams(parameters, "recaptcha", version);
            }

            if (method == "turnstile")
            {
                return ApiParams.PrepareSubmitParams(parameters, "turnstile");
            }

            if (method == "geetest")
            {
                return ApiParams.PrepareSubmitParams(parameters, "geetest");
            }

            return ApiParams.ApplyProxy(ApiParams.ApplyParamAliases(parameters));
        }

        internal static double NextPollInterval(double interval, double ceiling) => Math.Min(interval * 2, ceiling);

        private static Dictionary<string, object?> Clone(IDictionary<string, object?>? source) =>
            source is null ? new Dictionary<string, object?>() : new Dictionary<string, object?>(source);

        private static object? Pop(Dictionary<string, object?> dict, string key, object? fallback)
        {
            if (dict.TryGetValue(key, out var value))
            {
                dict.Remove(key);
                return value;
            }

            return fallback;
        }

        private static string ReprList(IEnumerable<string> values) =>
            "[" + string.Join(", ", values.Select(v => $"'{v}'")) + "]";

        private static double ToDouble(object? value)
        {
            switch (value)
            {
                case null:
                    return 0;
                case double d:
                    return d;
                case float f:
                    return f;
                case int i:
                    return i;
                case long l:
                    return l;
                case decimal m:
                    return (double)m;
                case string s when double.TryParse(s, NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return Convert.ToDouble(value, CultureInfo.InvariantCulture);
            }
        }

        private static int ToInt(object? value)
        {
            switch (value)
            {
                case null:
                    return 0;
                case int i:
                    return i;
                case long l:
                    return (int)l;
                case double d:
                    return (int)d;
                case bool b:
                    return b ? 1 : 0;
                case string s when int.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return 0;
            }
        }
    }
}
