using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CapSkip
{
    /// <summary>
    /// CapSkip API parameter handling — aliases, proxy expansion, and strict
    /// per-captcha validation (see https://capskip.com/api-docs/). Only the
    /// parameters CapSkip documents for a given captcha type are accepted.
    /// </summary>
    internal static class ApiParams
    {
        internal static readonly HashSet<string> NormalSubmit = new HashSet<string>
        {
            "method", "body", "json", "file",
        };

        internal static readonly HashSet<string> RecaptchaV2Submit = new HashSet<string>
        {
            "method", "googlekey", "pageurl", "enterprise", "invisible", "data-s", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> RecaptchaV3Submit = new HashSet<string>
        {
            "method", "version", "googlekey", "pageurl", "enterprise", "action", "min_score",
            "json", "proxy", "proxytype",
        };

        internal static readonly HashSet<string> TurnstileSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "action", "data", "pagedata", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> GeetestSubmit = new HashSet<string>
        {
            "method", "gt", "challenge", "pageurl", "api_server", "json",
            "proxy", "proxytype",
        };

        /// <summary>
        /// The only values CapSkip maps to a proxy scheme; it answers
        /// ERROR_BAD_PARAMETERS for anything else, SOCKS4 included. Matched
        /// case-insensitively, as the server does.
        /// </summary>
        internal static readonly string[] ProxyTypes = { "HTTP", "HTTPS", "SOCKS5", "SOCKS5H" };

        private static readonly IReadOnlyList<KeyValuePair<string, string>> ParamAliases =
            new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("url", "pageurl"),
                new KeyValuePair<string, string>("score", "min_score"),
                new KeyValuePair<string, string>("minScore", "min_score"),
                new KeyValuePair<string, string>("datas", "data-s"),
                new KeyValuePair<string, string>("data_s", "data-s"),
                new KeyValuePair<string, string>("apiServer", "api_server"),
                new KeyValuePair<string, string>("api_subdomain", "api_server"),
            };

        /// <summary>Map friendly parameter names (e.g. <c>url</c>) to their API names (<c>pageurl</c>).</summary>
        internal static Dictionary<string, object?> ApplyParamAliases(IDictionary<string, object?> parameters)
        {
            var outParams = new Dictionary<string, object?>(parameters);
            foreach (var alias in ParamAliases)
            {
                if (!outParams.TryGetValue(alias.Key, out var value))
                {
                    continue;
                }

                if (outParams.TryGetValue(alias.Value, out var existing) && !ValuesEqual(value, existing))
                {
                    throw new ValidationException(
                        $"Conflicting parameters: '{alias.Key}' and '{alias.Value}'");
                }

                outParams[alias.Value] = value;
                outParams.Remove(alias.Key);
            }

            return outParams;
        }

        /// <summary>Expand a <c>proxy</c> value into the <c>proxy</c> + <c>proxytype</c> API fields.</summary>
        internal static Dictionary<string, object?> ApplyProxy(IDictionary<string, object?> parameters)
        {
            var outParams = new Dictionary<string, object?>(parameters);
            outParams.TryGetValue("proxy", out var proxy);
            outParams.Remove("proxy");

            if (IsEmptyProxy(proxy))
            {
                return outParams;
            }

            switch (proxy)
            {
                case Proxy typed:
                    outParams["proxy"] = typed.Uri;
                    outParams["proxytype"] = typed.Type;
                    break;

                case IDictionary<string, object?> dict:
                    if (!dict.ContainsKey("uri") || !dict.ContainsKey("type"))
                    {
                        throw new ValidationException("proxy dict must contain 'type' and 'uri' keys");
                    }

                    outParams["proxy"] = dict["uri"];
                    outParams["proxytype"] = dict["type"];
                    break;

                default:
                    outParams["proxy"] = proxy;
                    if (!outParams.ContainsKey("proxytype"))
                    {
                        outParams["proxytype"] = "HTTP";
                    }

                    break;
            }

            return outParams;
        }

        /// <summary>Prepare and validate a submit payload for the given captcha type.</summary>
        internal static Dictionary<string, object?> PrepareSubmitParams(
            IDictionary<string, object?> parameters,
            string captchaType,
            string version = "v2")
        {
            var prepared = ApplyProxy(ApplyParamAliases(parameters));

            switch (captchaType)
            {
                case "normal":
                    ValidateNormalSubmit(prepared);
                    break;
                case "recaptcha":
                    ValidateRecaptchaSubmit(prepared, version);
                    break;
                case "turnstile":
                    ValidateTurnstileSubmit(prepared);
                    break;
                case "geetest":
                    ValidateGeetestSubmit(prepared);
                    break;
            }

            // Skipped for "normal", which rejects proxy outright with a clearer message.
            if (captchaType != "normal")
            {
                ValidateProxyType(prepared);
            }

            return prepared;
        }

        private static void ValidateProxyType(IDictionary<string, object?> parameters)
        {
            if (!parameters.TryGetValue("proxytype", out var proxytype) || proxytype is null)
            {
                return;
            }

            var text = Convert.ToString(proxytype, CultureInfo.InvariantCulture);
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            if (Array.IndexOf(ProxyTypes, text!.ToUpperInvariant()) < 0)
            {
                throw new ValidationException(
                    $"Unsupported proxytype '{text}'. "
                    + $"CapSkip accepts: {string.Join(", ", ProxyTypes)}.");
            }
        }

        private static void ValidateNormalSubmit(IDictionary<string, object?> parameters)
        {
            var unknown = UnknownKeys(parameters, NormalSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for image captcha: {ReprList(unknown)}. "
                    + "CapSkip only supports: method, file/body, json.");
            }

            if (parameters.ContainsKey("proxy") || parameters.ContainsKey("proxytype"))
            {
                throw new ValidationException(
                    "Proxy is not supported for image captcha. "
                    + "Use proxy only with reCAPTCHA or Turnstile.");
            }
        }

        private static void ValidateRecaptchaSubmit(IDictionary<string, object?> parameters, string version)
        {
            var normalized = (version ?? "v2").ToLowerInvariant();
            HashSet<string> allowed;

            if (normalized == "v3")
            {
                allowed = RecaptchaV3Submit;
                if (parameters.TryGetValue("invisible", out var invisible) && IsTruthy(invisible))
                {
                    throw new ValidationException("invisible is only supported for reCAPTCHA v2.");
                }
            }
            else
            {
                allowed = RecaptchaV2Submit;
                if (parameters.TryGetValue("version", out var v) && Equals(v, "v3"))
                {
                    throw new ValidationException("Use version='v3' for reCAPTCHA v3.");
                }

                foreach (var key in new[] { "action", "min_score" })
                {
                    if (parameters.ContainsKey(key))
                    {
                        throw new ValidationException($"'{key}' is only supported for reCAPTCHA v3.");
                    }
                }
            }

            var unknown = UnknownKeys(parameters, allowed);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for reCAPTCHA {normalized}: {ReprList(unknown)}.");
            }
        }

        private static void ValidateTurnstileSubmit(IDictionary<string, object?> parameters)
        {
            var unknown = UnknownKeys(parameters, TurnstileSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for Turnstile: {ReprList(unknown)}.");
            }
        }

        private static void ValidateGeetestSubmit(IDictionary<string, object?> parameters)
        {
            // All three are documented as required. gt is static per site, challenge is
            // single-use and expires in about a minute; without them CapSkip answers
            // ERROR_BAD_PARAMETERS, and without pageurl ERROR_PAGEURL. Fail locally so a
            // missing value does not cost a round-trip.
            foreach (var key in new[] { "gt", "challenge", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for GeeTest v3.");
                }
            }

            var unknown = UnknownKeys(parameters, GeetestSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for GeeTest: {ReprList(unknown)}.");
            }
        }

        private static List<string> UnknownKeys(IDictionary<string, object?> parameters, HashSet<string> allowed)
        {
            return parameters.Keys
                .Where(key => !allowed.Contains(key) && key != "key" && key != "file" && key != "files")
                .OrderBy(key => key, StringComparer.Ordinal)
                .ToList();
        }

        /// <summary>Format a list the way Python's <c>sorted(...)</c> repr does, for message parity.</summary>
        private static string ReprList(IEnumerable<string> values)
        {
            return "[" + string.Join(", ", values.Select(v => $"'{v}'")) + "]";
        }

        private static bool IsEmptyProxy(object? proxy)
        {
            switch (proxy)
            {
                case null:
                    return true;
                case string s:
                    return s.Length == 0;
                case bool b:
                    return !b;
                case IDictionary<string, object?> dict:
                    return dict.Count == 0;
                case IDictionary<string, string> stringDict:
                    return stringDict.Count == 0;
                default:
                    return IsZeroNumber(proxy);
            }
        }

        private static bool IsZeroNumber(object value)
        {
            switch (value)
            {
                case sbyte n: return n == 0;
                case byte n: return n == 0;
                case short n: return n == 0;
                case ushort n: return n == 0;
                case int n: return n == 0;
                case uint n: return n == 0;
                case long n: return n == 0;
                case ulong n: return n == 0;
                case float n: return n == 0;
                case double n: return n == 0;
                case decimal n: return n == 0;
                default: return false;
            }
        }

        private static bool IsTruthy(object? value)
        {
            switch (value)
            {
                case null:
                    return false;
                case bool b:
                    return b;
                case string s:
                    return s.Length > 0;
                default:
                    return !IsZeroNumber(value);
            }
        }

        private static bool ValuesEqual(object? a, object? b)
        {
            if (a is null || b is null)
            {
                return ReferenceEquals(a, b);
            }

            return a.Equals(b);
        }
    }
}
