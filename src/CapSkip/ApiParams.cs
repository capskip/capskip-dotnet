using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

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

        internal static readonly HashSet<string> AltchaSubmit = new HashSet<string>
        {
            "method", "pageurl", "challenge_url", "challenge_json", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> CapySubmit = new HashSet<string>
        {
            "method", "captchakey", "pageurl", "api_server", "version", "useragent", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> CaptchaFoxSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "api_server", "useragent", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> FriendlyCaptchaSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "version", "module_script", "nomodule_script",
            "api_server", "useragent", "json", "proxy", "proxytype",
        };

        internal static readonly HashSet<string> AmazonWafSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "iv", "context", "challenge_script",
            "captcha_script", "jsapiScript", "json", "proxy", "proxytype",
        };

        /// <summary>
        /// The script parameters of an AWS WAF task. CapSkip POSTs the solution to
        /// the host these name, so it refuses any that is not a genuine AWS WAF one.
        /// </summary>
        internal static readonly string[] AmazonWafScripts = { "challenge_script", "captcha_script", "jsapiScript" };

        /// <summary>The three script files an AWS WAF integration loads.</summary>
        private static readonly string[] AmazonWafScriptFiles = { "challenge.js", "captcha.js", "jsapi.js" };

        /// <summary>
        /// The server's own test for the directory part of a script path: one or
        /// more <c>[A-Za-z0-9_-]</c> segments.
        /// </summary>
        private static readonly Regex AmazonWafScriptDirectory =
            new Regex("^(/[A-Za-z0-9_-]+)+$", RegexOptions.CultureInvariant);

        internal static readonly HashSet<string> HCaptchaSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "invisible", "data", "useragent", "json",
            "proxy", "proxytype",
        };

        internal static readonly HashSet<string> MtCaptchaSubmit = new HashSet<string>
        {
            "method", "sitekey", "pageurl", "action", "useragent", "json",
            "proxy", "proxytype",
        };

        /// <summary>
        /// The server's sitekey test: <c>MTPublic-</c> then 1-55 letters, digits,
        /// <c>-</c> or <c>_</c> (64 characters at most). Anything else is answered
        /// ERROR_BAD_PARAMETERS.
        /// </summary>
        private static readonly Regex MtCaptchaSitekey =
            new Regex("^MTPublic-[A-Za-z0-9_-]{1,55}$", RegexOptions.CultureInvariant);

        internal static readonly HashSet<string> DataDomeSubmit = new HashSet<string>
        {
            "method", "captcha_url", "pageurl", "useragent", "json", "proxy", "proxytype",
        };

        /// <summary>
        /// Every DataDome challenge is served from this domain or a subdomain of it
        /// (geo.captcha-delivery.com in practice). CapSkip refuses any other host.
        /// </summary>
        internal const string DataDomeChallengeDomain = "captcha-delivery.com";

        /// <summary>
        /// The two challenge pages: <c>/captcha/</c> draws the slider,
        /// <c>/interstitial/</c> runs the device check. Any other path renders
        /// nothing to solve.
        /// </summary>
        internal static readonly string[] DataDomeChallengePaths = { "/captcha", "/interstitial" };

        /// <summary>A Chrome version as Chrome writes it in its User-Agent: four dotted numbers.</summary>
        private static readonly Regex ChromeVersion =
            new Regex(@"\bChrome/(\d+)\.\d+\.\d+\.\d+\b", RegexOptions.CultureInvariant);

        /// <summary>
        /// Chrome-derived browsers and non-desktop builds. A User-Agent carrying any
        /// of these is not stock Windows Chrome, so a cookie earned under it would be
        /// refused on replay; the server answers ERROR_WRONG_USERAGENT for each.
        /// </summary>
        private static readonly string[] NotStockWindowsChrome =
        {
            "Edg/", "EdgA/", "OPR/", "YaBrowser/", "SamsungBrowser/", "Vivaldi/",
            "HeadlessChrome/", "CriOS/", "Mobile", "Android",
        };

        /// <summary>
        /// CapSkip solves the puzzle family only. <c>avatar</c> is a different
        /// challenge behind a different endpoint; the server refuses it at submit
        /// time rather than answering it with a puzzle answer, which would bill
        /// for a solve the target site rejects.
        /// </summary>
        internal static readonly string[] CapyVersions = { "PUZZLE" };

        /// <summary>Both spellings the server accepts, and the bare digits it also takes.</summary>
        internal static readonly string[] FriendlyCaptchaVersions = { "V1", "V2", "1", "2" };

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
                new KeyValuePair<string, string>("challengeUrl", "challenge_url"),
                new KeyValuePair<string, string>("challengeURL", "challenge_url"),
                new KeyValuePair<string, string>("challengeJson", "challenge_json"),
                new KeyValuePair<string, string>("challengeJSON", "challenge_json"),

                // The server reads userAgent and useragent interchangeably on every
                // method that takes one, so the SDK settles on the lowercase spelling
                // its parameter tables document and accepts the camelCase one callers
                // arrive with.
                new KeyValuePair<string, string>("userAgent", "useragent"),
                new KeyValuePair<string, string>("user_agent", "useragent"),
                new KeyValuePair<string, string>("captchaKey", "captchakey"),
                new KeyValuePair<string, string>("moduleScript", "module_script"),
                new KeyValuePair<string, string>("nomoduleScript", "nomodule_script"),
                new KeyValuePair<string, string>("noModuleScript", "nomodule_script"),

                // AWS WAF: the API documents challenge_script / captcha_script in
                // snake case and jsapiScript in camel case, as 2Captcha does. Accept
                // the other spelling of each, which is what createTask-style callers
                // arrive with.
                new KeyValuePair<string, string>("challengeScript", "challenge_script"),
                new KeyValuePair<string, string>("captchaScript", "captcha_script"),
                new KeyValuePair<string, string>("jsapi_script", "jsapiScript"),

                // DataDome: 2Captcha and the API document captcha_url; createTask-style
                // callers arrive with captchaUrl.
                new KeyValuePair<string, string>("captchaUrl", "captcha_url"),
                new KeyValuePair<string, string>("captchaURL", "captcha_url"),
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
                case "altcha":
                    prepared = NormalizeAltchaSubmit(prepared);
                    ValidateAltchaSubmit(prepared);
                    break;
                case "capy":
                    prepared = DropUnset(prepared);
                    ValidateCapySubmit(prepared);
                    break;
                case "captchafox":
                    prepared = DropUnset(prepared);
                    ValidateCaptchaFoxSubmit(prepared);
                    break;
                case "friendly_captcha":
                    prepared = DropUnset(prepared);
                    ValidateFriendlyCaptchaSubmit(prepared);
                    break;
                case "amazon_waf":
                    // An optional left as "" means "not sent" to the server, exactly
                    // like null, so it is dropped rather than tripping the iv/context
                    // pairing.
                    prepared = DropUnsetOrEmpty(prepared);
                    ValidateAmazonWafSubmit(prepared);
                    break;
                case "datadome":
                    prepared = DropUnsetOrEmpty(prepared);
                    ValidateDataDomeSubmit(prepared);
                    break;
                case "hcaptcha":
                    prepared = NormalizeHCaptchaSubmit(prepared);
                    ValidateHCaptchaSubmit(prepared);
                    break;
                case "mtcaptcha":
                    prepared = NormalizeMtCaptchaSubmit(prepared);
                    ValidateMtCaptchaSubmit(prepared);
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

        /// <summary>
        /// Drop unset challenge params and serialize an inline challenge document.
        /// </summary>
        /// <remarks>
        /// <c>AltchaAsync</c> is normally called with one of the two challenge
        /// parameters left null, and the form body can only carry a string — so a
        /// document passed as a dictionary is serialized rather than ToString()'d
        /// into its type name. Mirrors the server, which reads a JSON-body
        /// <c>null</c> as "not sent".
        /// </remarks>
        private static Dictionary<string, object?> NormalizeAltchaSubmit(
            IDictionary<string, object?> parameters)
        {
            var outParams = new Dictionary<string, object?>();
            foreach (var pair in parameters)
            {
                if (pair.Value != null)
                {
                    outParams[pair.Key] = pair.Value;
                }
            }

            if (outParams.TryGetValue("challenge_json", out var challenge)
                && challenge != null
                && !(challenge is string))
            {
                outParams["challenge_json"] = JsonSerializer.Serialize(challenge);
            }

            return outParams;
        }

        private static void ValidateAltchaSubmit(IDictionary<string, object?> parameters)
        {
            if (!parameters.TryGetValue("pageurl", out var pageurl) || !IsTruthy(pageurl))
            {
                throw new ValidationException("'pageurl' is required for ALTCHA.");
            }

            // CapSkip answers ERROR_BAD_PARAMETERS when neither is sent. Sending both
            // is deliberately allowed — the inline document simply wins, because
            // fetching would only re-obtain what the caller already supplied.
            var hasUrl = parameters.TryGetValue("challenge_url", out var url) && IsTruthy(url);
            var hasJson = parameters.TryGetValue("challenge_json", out var json) && IsTruthy(json);
            if (!hasUrl && !hasJson)
            {
                throw new ValidationException(
                    "ALTCHA needs a challenge: pass 'challenge_url' for CapSkip to fetch it, "
                    + "or 'challenge_json' with the challenge document itself.");
            }

            var unknown = UnknownKeys(parameters, AltchaSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for ALTCHA: {ReprList(unknown)}.");
            }
        }

        /// <summary>
        /// Drop parameters left null so an omitted optional is not sent at all.
        /// </summary>
        /// <remarks>
        /// The form body can only carry strings, so a null default would otherwise
        /// reach the server ToString()'d. Mirrors the server, which reads a
        /// JSON-body <c>null</c> as "not sent".
        /// </remarks>
        private static Dictionary<string, object?> DropUnset(IDictionary<string, object?> parameters)
        {
            var outParams = new Dictionary<string, object?>();
            foreach (var pair in parameters)
            {
                if (pair.Value != null)
                {
                    outParams[pair.Key] = pair.Value;
                }
            }

            return outParams;
        }

        private static void ValidateCapySubmit(IDictionary<string, object?> parameters)
        {
            // Both are documented as required; without captchakey the server answers
            // ERROR_BAD_PARAMETERS and without pageurl ERROR_PAGEURL. Fail locally so
            // a missing value does not cost a round-trip.
            foreach (var key in new[] { "captchakey", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for Capy.");
                }
            }

            if (parameters.TryGetValue("version", out var version) && version != null)
            {
                var text = Convert.ToString(version, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(text)
                    && Array.IndexOf(CapyVersions, text!.ToUpperInvariant()) < 0)
                {
                    throw new ValidationException(
                        $"Unsupported Capy version '{text}'. CapSkip solves the puzzle family "
                        + "only — 'avatar' is a different challenge behind a different endpoint, "
                        + "and the server refuses it rather than returning a puzzle answer the "
                        + "target site would reject.");
                }
            }

            var unknown = UnknownKeys(parameters, CapySubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for Capy: {ReprList(unknown)}.");
            }
        }

        private static void ValidateCaptchaFoxSubmit(IDictionary<string, object?> parameters)
        {
            foreach (var key in new[] { "sitekey", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for CaptchaFox.");
                }
            }

            var unknown = UnknownKeys(parameters, CaptchaFoxSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for CaptchaFox: {ReprList(unknown)}.");
            }
        }

        private static void ValidateFriendlyCaptchaSubmit(IDictionary<string, object?> parameters)
        {
            foreach (var key in new[] { "sitekey", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for Friendly Captcha.");
                }
            }

            if (parameters.TryGetValue("version", out var version) && version != null)
            {
                var text = Convert.ToString(version, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(text)
                    && Array.IndexOf(FriendlyCaptchaVersions, text!.ToUpperInvariant()) < 0)
                {
                    throw new ValidationException(
                        $"Unsupported Friendly Captcha version '{text}'. Use 'v1' or 'v2' "
                        + "(a bare 1 or 2 is accepted too). The two are different protocols "
                        + "sharing one sitekey namespace, so solving the wrong one returns a "
                        + "well-formed token the target site rejects.");
                }
            }

            var unknown = UnknownKeys(parameters, FriendlyCaptchaSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for Friendly Captcha: {ReprList(unknown)}.");
            }
        }

        /// <summary>Like <see cref="DropUnset"/>, but an empty string counts as unset too.</summary>
        private static Dictionary<string, object?> DropUnsetOrEmpty(IDictionary<string, object?> parameters)
        {
            var outParams = new Dictionary<string, object?>();
            foreach (var pair in parameters)
            {
                if (pair.Value != null && !(pair.Value is string text && text.Length == 0))
                {
                    outParams[pair.Key] = pair.Value;
                }
            }

            return outParams;
        }

        /// <summary>True for a script URL CapSkip will accept on an AWS WAF task.</summary>
        /// <remarks>
        /// Mirrors the server: an absolute https URL with no credentials and no
        /// explicit non-default port, on a host under <c>.awswaf.com</c>, whose last
        /// path segment is <c>challenge.js</c>, <c>captcha.js</c> or <c>jsapi.js</c>
        /// below one or more <c>[A-Za-z0-9_-]</c> directories. A query string is
        /// allowed. The server only applies the check once the task is queued, so
        /// failing here saves a submit that could never solve.
        /// </remarks>
        internal static bool IsAmazonWafScriptUrl(object? value)
        {
            var text = Convert.ToString(value, CultureInfo.InvariantCulture)?.Trim();
            if (string.IsNullOrEmpty(text) || !Uri.TryCreate(text, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(uri.UserInfo)
                || !uri.IsDefaultPort
                || !uri.Host.EndsWith(".awswaf.com", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var path = uri.AbsolutePath;
            var slash = path.LastIndexOf('/');
            if (slash < 0)
            {
                return false;
            }

            var fileName = path.Substring(slash + 1);
            var directory = path.Substring(0, slash);
            return Array.IndexOf(AmazonWafScriptFiles, fileName) >= 0
                && AmazonWafScriptDirectory.IsMatch(directory);
        }

        private static void ValidateAmazonWafSubmit(IDictionary<string, object?> parameters)
        {
            // Without sitekey the server answers ERROR_BAD_PARAMETERS and without
            // pageurl ERROR_PAGEURL. Fail locally so a missing value costs no round-trip.
            foreach (var key in new[] { "sitekey", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for Amazon WAF.");
                }
            }

            var pageurl = (Convert.ToString(parameters["pageurl"], CultureInfo.InvariantCulture) ?? string.Empty)
                .Trim();
            if (pageurl.IndexOf("://", StringComparison.Ordinal) < 0
                || !Uri.TryCreate(pageurl, UriKind.Absolute, out var pageUri)
                || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new ValidationException(
                    $"'pageurl' must be a full http or https URL for Amazon WAF, got '{pageurl}'.");
            }

            // iv and context come off the same challenge page and only work as a
            // pair; the server refuses one without the other with ERROR_BAD_PARAMETERS.
            var hasIv = parameters.TryGetValue("iv", out var iv) && IsTruthy(iv);
            var hasContext = parameters.TryGetValue("context", out var context) && IsTruthy(context);
            if (hasIv != hasContext)
            {
                throw new ValidationException(
                    "Amazon WAF 'iv' and 'context' go together: send both (a challenge "
                    + "page) or neither (a widget page, or let CapSkip read them from pageurl).");
            }

            foreach (var key in AmazonWafScripts)
            {
                if (parameters.TryGetValue(key, out var script) && IsTruthy(script)
                    && !IsAmazonWafScriptUrl(script))
                {
                    throw new ValidationException(
                        $"'{key}' must be the https URL of an AWS WAF script on an awswaf.com "
                        + $"host (challenge.js, captcha.js or jsapi.js), got '{script}'. "
                        + "CapSkip refuses any other script, because it is not an AWS WAF integration.");
                }
            }

            var unknown = UnknownKeys(parameters, AmazonWafSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for Amazon WAF: {ReprList(unknown)}.");
            }
        }

        private static bool IsDataDomeHost(string? host)
        {
            var lower = (host ?? string.Empty).ToLowerInvariant();
            return lower == DataDomeChallengeDomain || lower.EndsWith("." + DataDomeChallengeDomain, StringComparison.Ordinal);
        }

        /// <summary>
        /// The query of a DataDome challenge URL, or <see langword="null"/> if it is
        /// not one.
        /// </summary>
        /// <remarks>
        /// Mirrors the server: an https URL on captcha-delivery.com (or a
        /// subdomain), path <c>/captcha/</c> or <c>/interstitial/</c>, carrying
        /// <c>initialCid</c>, <c>hash</c> and <c>cid</c>. An iframe src copied
        /// HTML-escaped (<c>&amp;amp;</c>) and a scheme-relative <c>//geo...</c> URL
        /// are both read the way the server reads them.
        /// </remarks>
        internal static Dictionary<string, string>? ParseDataDomeCaptchaUrl(object? value)
        {
            var text = WebUtility.HtmlDecode(
                (Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty).Trim());
            if (text.StartsWith("//", StringComparison.Ordinal))
            {
                text = "https:" + text;
            }

            if (!text.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                || !Uri.TryCreate(text, UriKind.Absolute, out var uri)
                || uri.Scheme != Uri.UriSchemeHttps
                || !IsDataDomeHost(uri.Host))
            {
                return null;
            }

            var path = Uri.UnescapeDataString(uri.AbsolutePath).TrimEnd('/');
            if (!DataDomeChallengePaths.Any(p => string.Equals(p, path, StringComparison.OrdinalIgnoreCase)))
            {
                return null;
            }

            // Read the query off the text itself rather than Uri.Query, so it is
            // decoded exactly once, as a form-encoded query string.
            var queryText = string.Empty;
            var hash = text.IndexOf('#');
            var withoutFragment = hash < 0 ? text : text.Substring(0, hash);
            var question = withoutFragment.IndexOf('?');
            if (question >= 0)
            {
                queryText = withoutFragment.Substring(question + 1);
            }

            var query = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var pair in queryText.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                var equals = pair.IndexOf('=');
                var name = WebUtility.UrlDecode(equals < 0 ? pair : pair.Substring(0, equals));
                var item = equals < 0 ? string.Empty : WebUtility.UrlDecode(pair.Substring(equals + 1));

                // Blank values count as absent, and the first occurrence wins.
                if (item.Length > 0 && !query.ContainsKey(name))
                {
                    query[name] = item;
                }
            }

            foreach (var key in new[] { "initialCid", "hash", "cid" })
            {
                if (!query.ContainsKey(key))
                {
                    return null;
                }
            }

            return query;
        }

        /// <summary>
        /// True for a stock Chrome-for-Windows User-Agent, the only kind CapSkip
        /// takes on a DataDome task.
        /// </summary>
        /// <remarks>
        /// DataDome ties the cookie to the browser that earned it, and CapSkip
        /// solves in Chrome on Windows, so a cookie for any other User-Agent would
        /// be refused on replay. The server also requires the same Chrome major
        /// version as its own browser; that part can only be checked there.
        /// </remarks>
        internal static bool IsDataDomeUserAgent(object? value)
        {
            var ua = Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty;
            return ua.IndexOf("Windows NT ", StringComparison.Ordinal) >= 0
                && ChromeVersion.IsMatch(ua)
                && !NotStockWindowsChrome.Any(token => ua.IndexOf(token, StringComparison.Ordinal) >= 0);
        }

        private static void ValidateDataDomeSubmit(IDictionary<string, object?> parameters)
        {
            // Without captcha_url the server answers ERROR_BAD_PARAMETERS and without
            // pageurl ERROR_PAGEURL. Fail locally so a missing value costs no round-trip.
            foreach (var key in new[] { "captcha_url", "pageurl" })
            {
                if (!parameters.TryGetValue(key, out var value) || !IsTruthy(value))
                {
                    throw new ValidationException($"'{key}' is required for DataDome.");
                }
            }

            var query = ParseDataDomeCaptchaUrl(parameters["captcha_url"]);
            if (query is null)
            {
                throw new ValidationException(
                    "'captcha_url' must be the https address of a DataDome challenge on "
                    + "captcha-delivery.com, under /captcha/ or /interstitial/ and carrying "
                    + $"initialCid, hash and cid, got '{parameters["captcha_url"]}'. Take it from "
                    + "the block page's iframe src, or build it from the page's `var dd` object.");
            }

            // t=bv is DataDome saying the address that fetched the block page is
            // banned. Nothing sent from it can pass, and the server refuses the task
            // with ERROR_PROXY_BANNED, so say so before the round-trip.
            if (query.TryGetValue("t", out var t) && t == "bv")
            {
                throw new ValidationException(
                    "This DataDome block page bans the IP address that requested it "
                    + "(t=bv), so no solve from that address can pass. Change the address "
                    + "(proxy), request the page again and submit the new captcha_url.");
            }

            var pageurl = (Convert.ToString(parameters["pageurl"], CultureInfo.InvariantCulture) ?? string.Empty)
                .Trim();
            if (pageurl.IndexOf("://", StringComparison.Ordinal) < 0
                || !Uri.TryCreate(pageurl, UriKind.Absolute, out var pageUri)
                || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(pageUri.Host)
                || IsDataDomeHost(pageUri.Host))
            {
                throw new ValidationException(
                    "'pageurl' must be the full http or https URL of the page that "
                    + $"returned the DataDome block page, got '{pageurl}'.");
            }

            if (parameters.TryGetValue("useragent", out var userAgent) && IsTruthy(userAgent)
                && !IsDataDomeUserAgent(userAgent))
            {
                throw new ValidationException(
                    "DataDome 'userAgent' must be a stock Chrome for Windows User-Agent "
                    + "with the same major version as the browser CapSkip solves with, got "
                    + $"'{userAgent}'. The cookie only works with the User-Agent that "
                    + "earned it. Leave it out and CapSkip uses its own browser's, which "
                    + "the result reports as UserAgent.");
            }

            var unknown = UnknownKeys(parameters, DataDomeSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for DataDome: {ReprList(unknown)}.");
            }
        }

        /// <summary>
        /// Read an hCaptcha <c>invisible</c> value: <see langword="true"/> for on,
        /// <see langword="false"/> for off, <see langword="null"/> for anything else.
        /// </summary>
        /// <remarks>
        /// Accepts a bool, the integers 1 and 0, and the strings <c>"1"</c>,
        /// <c>"0"</c>, <c>"true"</c> and <c>"false"</c> in any case. The server reads
        /// only <c>"1"</c> or <c>"true"</c> as invisible, so anything else would
        /// silently mean a visible widget.
        /// </remarks>
        internal static bool? ParseHCaptchaInvisible(object? value)
        {
            switch (value)
            {
                case bool b:
                    return b;
                case sbyte _:
                case byte _:
                case short _:
                case ushort _:
                case int _:
                case uint _:
                case long _:
                case ulong _:
                    var number = Convert.ToDecimal(value, CultureInfo.InvariantCulture);
                    return number == 1 ? true : number == 0 ? false : (bool?)null;
                case string s:
                    switch (s.ToLowerInvariant())
                    {
                        case "1":
                        case "true":
                            return true;
                        case "0":
                        case "false":
                            return false;
                        default:
                            return null;
                    }

                default:
                    return null;
            }
        }

        /// <summary>
        /// Drop unset and empty optionals, and send <c>invisible</c> as the
        /// <c>1</c> the server reads. Off is the server's default, so it is not
        /// sent at all.
        /// </summary>
        private static Dictionary<string, object?> NormalizeHCaptchaSubmit(IDictionary<string, object?> parameters)
        {
            var prepared = DropUnsetOrEmpty(parameters);
            if (prepared.TryGetValue("invisible", out var raw))
            {
                var invisible = ParseHCaptchaInvisible(raw);
                if (invisible is null)
                {
                    throw new ValidationException(
                        $"invisible must be 0 or 1 (or a bool) for hCaptcha, not '{raw}'.");
                }

                if (invisible.Value)
                {
                    prepared["invisible"] = 1;
                }
                else
                {
                    prepared.Remove("invisible");
                }
            }

            return prepared;
        }

        private static void ValidateHCaptchaSubmit(IDictionary<string, object?> parameters)
        {
            // The server answers ERROR_RECAPTCHA_INVALID_SITEKEY for a missing sitekey
            // and ERROR_PAGEURL for a page URL that is missing or not http(s). Fail
            // locally so neither costs a round-trip.
            parameters.TryGetValue("sitekey", out var sitekey);
            if ((Convert.ToString(sitekey, CultureInfo.InvariantCulture) ?? string.Empty).Trim().Length == 0)
            {
                throw new ValidationException("'sitekey' is required for hCaptcha.");
            }

            parameters.TryGetValue("pageurl", out var pageurlValue);
            var pageurl = (Convert.ToString(pageurlValue, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
            if (pageurl.Length == 0)
            {
                throw new ValidationException("'pageurl' is required for hCaptcha.");
            }

            if (pageurl.IndexOf("://", StringComparison.Ordinal) < 0
                || !Uri.TryCreate(pageurl, UriKind.Absolute, out var pageUri)
                || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(pageUri.Host))
            {
                throw new ValidationException(
                    $"'pageurl' must be an absolute http or https URL for hCaptcha, got '{pageurl}'.");
            }

            var unknown = UnknownKeys(parameters, HCaptchaSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for hCaptcha: {ReprList(unknown)}.");
            }
        }

        /// <summary>
        /// Drop unset and empty optionals, and strip the sitekey the way the server
        /// does: surrounding whitespace and a leading <c>sk=</c> in any case.
        /// </summary>
        /// <remarks>
        /// MTCaptcha's own getchallenge query names the key <c>sk=MTPublic-...</c>,
        /// and callers copy it from there.
        /// </remarks>
        private static Dictionary<string, object?> NormalizeMtCaptchaSubmit(IDictionary<string, object?> parameters)
        {
            var prepared = DropUnsetOrEmpty(parameters);
            if (prepared.TryGetValue("sitekey", out var raw) && raw is string sitekey)
            {
                sitekey = sitekey.Trim();
                if (sitekey.StartsWith("sk=", StringComparison.OrdinalIgnoreCase))
                {
                    sitekey = sitekey.Substring(3).Trim();
                }

                prepared["sitekey"] = sitekey;
            }

            return prepared;
        }

        private static void ValidateMtCaptchaSubmit(IDictionary<string, object?> parameters)
        {
            // The server answers ERROR_BAD_PARAMETERS for a missing or malformed
            // sitekey and ERROR_PAGEURL for a page URL that is missing or not
            // http(s). Fail locally so neither costs a round-trip.
            parameters.TryGetValue("sitekey", out var sitekeyValue);
            var sitekey = Convert.ToString(sitekeyValue, CultureInfo.InvariantCulture) ?? string.Empty;
            if (sitekey.Trim().Length == 0)
            {
                throw new ValidationException("'sitekey' is required for MTCaptcha.");
            }

            if (!(sitekeyValue is string) || !MtCaptchaSitekey.IsMatch(sitekey))
            {
                throw new ValidationException(
                    $"'{sitekey}' is not an MTCaptcha sitekey. It starts with MTPublic- followed by "
                    + "letters, digits, '-' or '_' (64 characters at most).");
            }

            parameters.TryGetValue("pageurl", out var pageurlValue);
            var pageurl = (Convert.ToString(pageurlValue, CultureInfo.InvariantCulture) ?? string.Empty).Trim();
            if (pageurl.Length == 0)
            {
                throw new ValidationException("'pageurl' is required for MTCaptcha.");
            }

            if (pageurl.IndexOf("://", StringComparison.Ordinal) < 0
                || !Uri.TryCreate(pageurl, UriKind.Absolute, out var pageUri)
                || (pageUri.Scheme != Uri.UriSchemeHttp && pageUri.Scheme != Uri.UriSchemeHttps)
                || string.IsNullOrEmpty(pageUri.Host))
            {
                throw new ValidationException(
                    $"'pageurl' must be an absolute http or https URL for MTCaptcha, got '{pageurl}'.");
            }

            var unknown = UnknownKeys(parameters, MtCaptchaSubmit);
            if (unknown.Count > 0)
            {
                throw new ValidationException(
                    $"Unsupported parameters for MTCaptcha: {ReprList(unknown)}.");
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
