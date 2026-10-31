using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace CapSkip
{
    /// <summary>
    /// Parsing of CapSkip's <c>in.php</c> / <c>res.php</c> response bodies. Kept
    /// separate from the transport so it can be unit tested in isolation, exactly
    /// like the other CapSkip SDKs.
    /// </summary>
    internal static class ResponseParsing
    {
        /// <summary>
        /// How an object answer is written back into <see cref="SolveResult.Code"/>.
        /// Code is a .NET string, never embedded in HTML by the SDK, so only what
        /// JSON itself requires is escaped.
        /// </summary>
        private static readonly JsonSerializerOptions RawAnswerJson = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        /// <summary>
        /// Parse a <c>res.php</c> body. Returns the token <see cref="string"/> in plain
        /// mode, or the parsed object (as a <c>Dictionary&lt;string, object?&gt;</c>) in
        /// JSON mode. Throws <see cref="NetworkException"/> while the captcha is not
        /// ready (including an empty body).
        /// </summary>
        internal static object ParsePollResponse(string? response, int jsonMode = 0)
        {
            var text = (response ?? string.Empty).Trim();

            // CapSkip returns an empty body whenever no result is available yet: briefly
            // right after submit (before it starts reporting CAPCHA_NOT_READY), for an
            // unknown id, and after a solved token has already been read once. Treat it
            // like CAPCHA_NOT_READY so the caller keeps polling instead of failing.
            if (text.Length == 0)
            {
                throw new NetworkException();
            }

            if (jsonMode != 0)
            {
                Dictionary<string, object?> data;
                try
                {
                    data = ParseJsonObject(text);
                }
                catch (JsonException e)
                {
                    throw new ApiException($"invalid JSON response: {response}", e);
                }

                var status = GetLong(data, "status");
                var request = data.TryGetValue("request", out var r) ? r as string : null;

                if (status == 0 && request == "CAPCHA_NOT_READY")
                {
                    throw new NetworkException();
                }

                if (status != 1)
                {
                    throw new ApiException($"cannot recognize response {JsonSerializer.Serialize(data)}");
                }

                return data;
            }

            if (text == "CAPCHA_NOT_READY")
            {
                throw new NetworkException();
            }

            if (!text.StartsWith("OK|", StringComparison.Ordinal))
            {
                throw new ApiException($"cannot recognize response {response}");
            }

            return text.Substring(3);
        }

        /// <summary>
        /// Parse an <c>in.php</c> submit response. CapSkip returns <c>OK|&lt;id&gt;</c> by
        /// default, or <c>{"status":1,"request":"&lt;id&gt;"}</c> when the submit carried
        /// <c>json=1</c>. Both forms are accepted.
        /// </summary>
        internal static string ParseSubmitResponse(string? response)
        {
            var text = (response ?? string.Empty).Trim();

            if (text.StartsWith("OK|", StringComparison.Ordinal))
            {
                return text.Substring(3);
            }

            Dictionary<string, object?>? data;
            try
            {
                data = ParseJsonObject(text);
            }
            catch (JsonException)
            {
                data = null;
            }

            if (data != null && GetLong(data, "status") == 1 && data.ContainsKey("request"))
            {
                return Convert.ToString(data["request"], CultureInfo.InvariantCulture) ?? string.Empty;
            }

            throw new ApiException($"cannot recognize response {response}");
        }

        /// <summary>Fold a polled result (string token or JSON object) into a <see cref="SolveResult"/>.</summary>
        internal static SolveResult ApplyPollResult(SolveResult result, object polled)
        {
            if (polled is IDictionary<string, object?> dict)
            {
                // Capy is the one method whose answer is an object rather than a
                // string, and json=1 puts that object straight into `request`. It is
                // re-serialized rather than ToString()'d, so Code carries the same
                // JSON a plain-text poll would have delivered after OK|.
                result.Code = dict.TryGetValue("request", out var req)
                    ? StringifyRequest(req)
                    : string.Empty;

                object? userAgent = null;
                if (dict.TryGetValue("useragent", out var ua1) && ua1 != null)
                {
                    userAgent = ua1;
                }
                else if (dict.TryGetValue("userAgent", out var ua2) && ua2 != null)
                {
                    userAgent = ua2;
                }

                var userAgentText = userAgent is null
                    ? null
                    : Convert.ToString(userAgent, CultureInfo.InvariantCulture);
                if (!string.IsNullOrEmpty(userAgentText))
                {
                    result.UserAgent = userAgentText;
                }

                // ALTCHA's createTask-shaped "solution" object, which no other
                // captcha type sends. Its counter is the only field both ALTCHA
                // generations report the same way, so take it here rather than
                // inferring it from the token, whose shape differs per scheme.
                if (dict.TryGetValue("solution", out var solution)
                    && solution is IDictionary<string, object?> solutionDict)
                {
                    if (solutionDict.TryGetValue("number", out var number))
                    {
                        result.Number = ToCounter(number);
                    }

                    // A client must never be left without the token the poll
                    // actually carried, so the createTask-shaped object is read as a
                    // fallback when `request` came through empty.
                    if (result.Code.Length == 0
                        && solutionDict.TryGetValue("token", out var solutionToken))
                    {
                        result.Code = Convert.ToString(solutionToken, CultureInfo.InvariantCulture)
                            ?? string.Empty;
                    }
                }
            }
            else
            {
                result.Code = Convert.ToString(polled, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return result;
        }

        /// <summary>
        /// Render a polled <c>request</c> value as the string <see cref="SolveResult.Code"/>
        /// holds: JSON for an object answer, the value itself for a plain token.
        /// </summary>
        private static string StringifyRequest(object? request)
        {
            if (request is null)
            {
                return string.Empty;
            }

            if (request is string text)
            {
                return text;
            }

            // An object or array answer — Capy's three-value solution, or AWS WAF's
            // voucher and token. Serializing it keeps Code a string without losing
            // anything the caller may want to forward verbatim. The relaxed encoder
            // leaves +, / and = as they are: AWS WAF tokens are base64, and the
            // default encoder would turn every + into +, valid JSON but not the
            // text a plain-text poll delivers after OK|.
            if (request is IDictionary<string, object?> || request is IList<object?>)
            {
                return JsonSerializer.Serialize(request, RawAnswerJson);
            }

            return Convert.ToString(request, CultureInfo.InvariantCulture) ?? string.Empty;
        }

        /// <summary>
        /// Expand a Capy answer — an object rather than a token — into
        /// <see cref="SolveResult.CaptchaKey"/>, <see cref="SolveResult.ChallengeKey"/>,
        /// <see cref="SolveResult.Answer"/> and <see cref="SolveResult.RespKey"/>.
        /// </summary>
        /// <remarks>
        /// These three values go into the target form's <c>capy_captchakey</c>,
        /// <c>capy_challengekey</c> and <c>capy_answer</c> fields.
        /// <see cref="SolveResult.Code"/> keeps the raw JSON answer so callers that
        /// forward it verbatim (or that were written against another solver's API)
        /// keep working. If it does not parse, the result is returned untouched
        /// rather than masking the server's reply.
        /// </remarks>
        internal static SolveResult ApplyCapySolution(SolveResult result)
        {
            if (!TryParseObjectAnswer(result, out var payload))
            {
                return result;
            }

            result.CaptchaKey = ObjectAnswerField(payload, "captchakey") ?? result.CaptchaKey;
            result.ChallengeKey = ObjectAnswerField(payload, "challengekey") ?? result.ChallengeKey;
            result.Answer = ObjectAnswerField(payload, "answer") ?? result.Answer;
            result.RespKey = ObjectAnswerField(payload, "respKey") ?? result.RespKey;

            return result;
        }

        /// <summary>
        /// Expand an AWS WAF answer — an object rather than a token — into
        /// <see cref="SolveResult.CaptchaVoucher"/> and
        /// <see cref="SolveResult.ExistingToken"/>.
        /// </summary>
        /// <remarks>
        /// The two only work as a pair: the voucher AWS issued for the answered
        /// puzzle, and the token it was issued for. The site's own <c>voucher</c>
        /// endpoint trades them for the <c>aws-waf-token</c> cookie.
        /// <see cref="SolveResult.Code"/> keeps the raw JSON answer. If it does not
        /// parse, the result is returned untouched rather than masking the server's
        /// reply.
        /// </remarks>
        internal static SolveResult ApplyAmazonWafSolution(SolveResult result)
        {
            if (!TryParseObjectAnswer(result, out var payload))
            {
                return result;
            }

            result.CaptchaVoucher = ObjectAnswerField(payload, "captcha_voucher") ?? result.CaptchaVoucher;
            result.ExistingToken = ObjectAnswerField(payload, "existing_token") ?? result.ExistingToken;

            return result;
        }

        /// <summary>
        /// Read an answer that is an object rather than a token out of
        /// <see cref="SolveResult.Code"/>.
        /// </summary>
        /// <remarks>
        /// Code always holds that object as JSON: a json=1 poll puts the object
        /// straight into <c>request</c> and <see cref="ApplyPollResult"/>
        /// re-serializes it, while a plain-text poll delivers the same JSON after
        /// <c>OK|</c>. Returns false when it does not parse as an object.
        /// </remarks>
        private static bool TryParseObjectAnswer(SolveResult result, out Dictionary<string, object?> payload)
        {
            try
            {
                payload = ParseJsonObject(result.Code ?? string.Empty);
                return true;
            }
            catch (JsonException)
            {
                payload = new Dictionary<string, object?>();
                return false;
            }
        }

        private static string? ObjectAnswerField(IDictionary<string, object?> payload, string key)
        {
            return payload.TryGetValue(key, out var value) && value != null
                ? Convert.ToString(value, CultureInfo.InvariantCulture)
                : null;
        }

        /// <summary>
        /// Expose a single-token answer as <see cref="SolveResult.Token"/>, named for
        /// the form field it fills. <see cref="SolveResult.Code"/> keeps the same
        /// string, so callers that forward it verbatim keep working.
        /// </summary>
        internal static SolveResult ApplyTokenSolution(SolveResult result)
        {
            result.Token = result.Code ?? string.Empty;
            return result;
        }

        /// <summary>
        /// Expand a GeeTest answer — a JSON string in <c>request</c>, keyed with the
        /// <c>geetest_</c> prefix the target site's own form fields use — into
        /// <see cref="SolveResult.Challenge"/>, <see cref="SolveResult.Validate"/>,
        /// and <see cref="SolveResult.Seccode"/>.
        /// </summary>
        /// <remarks>
        /// <see cref="SolveResult.Code"/> keeps the raw JSON string so callers that
        /// forward it verbatim (or that were written against another solver's API)
        /// keep working. If it does not parse, the result is returned untouched
        /// rather than masking the server's reply.
        /// </remarks>
        internal static SolveResult ApplyGeetestSolution(SolveResult result)
        {
            Dictionary<string, object?> payload;
            try
            {
                payload = ParseJsonObject(result.Code ?? string.Empty);
            }
            catch (JsonException)
            {
                return result;
            }

            result.Challenge = GeetestField(payload, "geetest_challenge", "challenge") ?? result.Challenge;
            result.Validate = GeetestField(payload, "geetest_validate", "validate") ?? result.Validate;
            result.Seccode = GeetestField(payload, "geetest_seccode", "seccode") ?? result.Seccode;

            return result;
        }

        /// <summary>
        /// Expose the ALTCHA answer as <see cref="SolveResult.Token"/>, and the
        /// winning counter as <see cref="SolveResult.Number"/>.
        /// </summary>
        /// <remarks>
        /// ALTCHA answers come back as a base64 payload: the challenge document with
        /// the winning counter added. That payload is what the site's own
        /// <c>altcha</c> form field carries, so it is posted back verbatim.
        /// <see cref="SolveResult.Code"/> keeps the raw answer so callers that
        /// forward it (or that were written against another solver's API) keep
        /// working.
        /// <para>
        /// The counter comes from the server's own solution object when the poll
        /// carried one, because that is the single field both ALTCHA generations
        /// report the same way. Only if it is absent — a plain-text poll — is it dug
        /// out of the token, which is shaped differently per scheme. If neither
        /// yields one, the result keeps its token and simply has no number, rather
        /// than masking the server's reply.
        /// </para>
        /// </remarks>
        internal static SolveResult ApplyAltchaSolution(SolveResult result)
        {
            var code = result.Code ?? string.Empty;
            result.Token = code;

            // Already taken from the server's own solution object during the poll.
            if (result.Number != null)
            {
                return result;
            }

            byte[] decoded;
            try
            {
                decoded = Convert.FromBase64String(code);
            }
            catch (FormatException)
            {
                return result;
            }

            Dictionary<string, object?> payload;
            try
            {
                payload = ParseJsonObject(Encoding.UTF8.GetString(decoded));
            }
            catch (JsonException)
            {
                return result;
            }
            catch (DecoderFallbackException)
            {
                return result;
            }

            // The two ALTCHA generations nest the counter differently: a legacy
            // payload is the challenge document with a top-level "number" added,
            // while a proof-of-work v2 payload is
            // {"challenge": {...}, "solution": {"counter": N, ...}} with no
            // "number" at all.
            if (payload.TryGetValue("number", out var number) && number != null)
            {
                result.Number = ToCounter(number);
            }
            else if (payload.TryGetValue("solution", out var solution)
                && solution is IDictionary<string, object?> solutionDict
                && solutionDict.TryGetValue("counter", out var counter))
            {
                result.Number = ToCounter(counter);
            }

            return result;
        }

        /// <summary>
        /// Convert a JSON counter to a long, or null if it is not a number.
        /// </summary>
        /// <remarks>
        /// A non-numeric counter is the server telling us something we do not
        /// model; it is left out rather than failing the whole solve.
        /// </remarks>
        private static long? ToCounter(object? value)
        {
            if (value == null)
            {
                return null;
            }

            try
            {
                return Convert.ToInt64(value, CultureInfo.InvariantCulture);
            }
            catch (Exception ex) when (ex is FormatException || ex is InvalidCastException
                || ex is OverflowException)
            {
                return null;
            }
        }

        private static string? GeetestField(IDictionary<string, object?> payload, string prefixed, string shortName)
        {
            if (payload.TryGetValue(prefixed, out var value) && value != null)
            {
                return Convert.ToString(value, CultureInfo.InvariantCulture);
            }

            if (payload.TryGetValue(shortName, out var fallback) && fallback != null)
            {
                return Convert.ToString(fallback, CultureInfo.InvariantCulture);
            }

            return null;
        }

        private static Dictionary<string, object?> ParseJsonObject(string text)
        {
            using var doc = JsonDocument.Parse(text);
            if (doc.RootElement.ValueKind != JsonValueKind.Object)
            {
                throw new JsonException("expected a JSON object");
            }

            return (Dictionary<string, object?>)ConvertElement(doc.RootElement)!;
        }

        private static object? ConvertElement(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    var obj = new Dictionary<string, object?>();
                    foreach (var property in element.EnumerateObject())
                    {
                        obj[property.Name] = ConvertElement(property.Value);
                    }

                    return obj;

                case JsonValueKind.Array:
                    var list = new List<object?>();
                    foreach (var item in element.EnumerateArray())
                    {
                        list.Add(ConvertElement(item));
                    }

                    return list;

                case JsonValueKind.String:
                    return element.GetString();

                case JsonValueKind.Number:
                    return element.TryGetInt64(out var l) ? l : (object)element.GetDouble();

                case JsonValueKind.True:
                    return true;

                case JsonValueKind.False:
                    return false;

                default:
                    return null;
            }
        }

        private static long? GetLong(IDictionary<string, object?> data, string key)
        {
            if (!data.TryGetValue(key, out var value) || value is null)
            {
                return null;
            }

            switch (value)
            {
                case long l:
                    return l;
                case int i:
                    return i;
                case double d:
                    return (long)d;
                case string s when long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed):
                    return parsed;
                default:
                    return null;
            }
        }
    }
}
