using System;
using System.Collections.Generic;
using System.Globalization;
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
                result.Code = dict.TryGetValue("request", out var req)
                    ? Convert.ToString(req, CultureInfo.InvariantCulture) ?? string.Empty
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
            }
            else
            {
                result.Code = Convert.ToString(polled, CultureInfo.InvariantCulture) ?? string.Empty;
            }

            return result;
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
