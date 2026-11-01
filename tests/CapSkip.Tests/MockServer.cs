using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CapSkip.Tests
{
    /// <summary>
    /// A local mock CapSkip server exercising the full submit/poll round trip over
    /// real HTTP, mirroring <c>tests/conftest.py</c> from the Python SDK. Built on a
    /// raw <see cref="TcpListener"/> so it needs no URL-ACL reservation on Windows.
    /// </summary>
    public sealed class MockServer : IDisposable
    {
        public const string Code = "SOLVED_TOKEN_abc123";
        public const string UserAgent = "CapSkipUA/1.0";

        // ALTCHA answers are base64 of the challenge document with the winning
        // counter added, so the mock has to return a real one for the token/number
        // parsing to mean anything.
        public const long AltchaNumber = 9661;
        public const string AltchaToken = "eyJhbGdvcml0aG0iOiJTSEEtMjU2IiwiY2hhbGxlbmdlIjoiM2RkMjgyNTNiZTZjYzBjNTRkOTVmN2Y5OGM1MTdlNjgiLCJudW1iZXIiOjk2NjEsInNhbHQiOiI0NmQ1YjFjODg3MWU1MTUyZDkwMmVlM2Y/ZXhwaXJlcz0xODkzNDU2MDAwIiwic2lnbmF0dXJlIjoiNGIxY2YwZTBiZTBmNGU1MjQ3ZTUwYjBmOWE0NDk4MzAifQ==";

        // A minimal valid 1x1 PNG. The SDK never inspects the content, so the exact
        // pixels do not matter.
        public static readonly byte[] Png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==");

        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        /// <summary>
        /// Capy answers are not a token: three values that together go into the
        /// target form. <c>answer</c> is the drag path the widget would have
        /// recorded, so the mock carries a realistic one — the expansion is only
        /// meaningful against a real shape.
        /// </summary>
        public const string CapyCaptchaKey = "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v";

        public const string CapyChallengeKey = "BalY2gJaI8uA2SGVOZhqBQ3V0CYSNNGP";

        public const string CapyAnswer = "0xax8ex0xax84x0xkx7qx0x18x76x0x1ix6sx0x26x68x0x2gx5kx0x34x50x";

        /// <summary>The Capy solution object, exactly as CapSkip serializes it.</summary>
        public const string CapySolutionJson =
            "{\"captchakey\":\"" + CapyCaptchaKey + "\","
            + "\"challengekey\":\"" + CapyChallengeKey + "\","
            + "\"answer\":\"" + CapyAnswer + "\","
            + "\"respKey\":\"\"}";

        public const string CaptchaFoxToken =
            "177f50c25b845601e5c779cdb51b040d523e8ab69efb4d5b343e28df07d05076";

        /// <summary>
        /// The UA the browser actually minted the token under — deliberately not one
        /// a caller could have sent, so a test can tell the two apart.
        /// </summary>
        public const string CaptchaFoxUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/146.0.0.0 Safari/537.36";

        /// <summary>
        /// A v1 token: four dot-separated parts. A v2 one is a single opaque string
        /// of roughly six kilobytes, which shape-wise changes nothing the SDK does.
        /// </summary>
        public const string FriendlyCaptchaToken =
            "c62c4da36bbaf7f253873035832709ef."
            + "aqwpWwdbzRWKY/UQAQwwpgAAAAAAAAAAM7hBvJOzqjc=."
            + "AAAAAArcCQABAAAAxv8QAAIAAACKYRgA.AgAB";

        /// <summary>
        /// An AWS WAF answer is two values that only work as a pair, so like Capy's
        /// it is an object. The shapes follow what AWS really issues — a dotted
        /// voucher, and a token of three colon-separated parts.
        /// </summary>
        public const string AmazonWafCaptchaVoucher =
            "eyJ2b3VjaGVyIjoiRVhBTVBMRSJ9.9QwZcEXAMPLEvoucherJk4LmN0pQ";

        public const string AmazonWafExistingToken =
            "7d4c2a91-EXAMPLE-4f0b-9e6d-3a1b5c8e2f70:EQoAeXAMPLEtokenAAAA:Wm9wc2E=";

        /// <summary>The AWS WAF solution object, exactly as CapSkip serializes it.</summary>
        public const string AmazonWafSolutionJson =
            "{\"captcha_voucher\":\"" + AmazonWafCaptchaVoucher + "\","
            + "\"existing_token\":\"" + AmazonWafExistingToken + "\"}";

        /// <summary>
        /// DataDome answers with a cookie, not a token: the Set-Cookie value exactly
        /// as DataDome set it, attributes included. Next to it CapSkip names the
        /// User-Agent the cookie was earned with (the caller's, or its own browser's
        /// when the task sent none) and, only when the task sent no proxy and the
        /// app's DataDome proxy list supplied one, that proxy as host:port.
        /// </summary>
        public const string DataDomeCookie =
            "datadome=FAPPpli4jkT1MZGtgF8ez7mjSz8MrJ9fGRNE51Gj79cq9fXp5xn~F1P5APWjqvJojf4U9dq0R81Xaj2hpYtjle95mkg; "
            + "Max-Age=31536000; Domain=.example.com; Path=/; Secure; SameSite=Lax";

        public const string DataDomeBrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
            + "(KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36";

        public const string DataDomeListProxy = "203.0.113.10:8080";

        private readonly ConcurrentDictionary<string, IReadOnlyDictionary<string, string>> _submitted =
            new ConcurrentDictionary<string, IReadOnlyDictionary<string, string>>();

        private readonly ConcurrentDictionary<string, string> _idType = new ConcurrentDictionary<string, string>();
        private readonly ConcurrentDictionary<string, int> _pollCount = new ConcurrentDictionary<string, int>();
        private int _ids;

        public MockServer()
        {
            _listener = new TcpListener(IPAddress.Loopback, 0);
            _listener.Start();
            var endpoint = (IPEndPoint)_listener.LocalEndpoint;
            Host = endpoint.Address.ToString();
            Port = endpoint.Port;
            _ = Task.Run(AcceptLoopAsync);
        }

        public string Host { get; }

        public int Port { get; }

        public void Dispose()
        {
            _cts.Cancel();
            _listener.Stop();
            _cts.Dispose();
        }

        private async Task AcceptLoopAsync()
        {
            while (!_cts.IsCancellationRequested)
            {
                TcpClient client;
                try
                {
                    client = await _listener.AcceptTcpClientAsync().ConfigureAwait(false);
                }
                catch
                {
                    break;
                }

                _ = Task.Run(() => HandleClientAsync(client));
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (client)
                using (var stream = client.GetStream())
                {
                    var request = await ReadRequestAsync(stream).ConfigureAwait(false);
                    if (request.RequestLine is null)
                    {
                        return;
                    }

                    var tokens = request.RequestLine.Split(' ');
                    var method = tokens[0];
                    var target = tokens.Length > 1 ? tokens[1] : "/";
                    var questionMark = target.IndexOf('?');
                    var path = questionMark >= 0 ? target.Substring(0, questionMark) : target;
                    var queryString = questionMark >= 0 ? target.Substring(questionMark + 1) : string.Empty;

                    if (method == "GET" && path == "/image.png")
                    {
                        WriteResponse(stream, Png, "image/png");
                    }
                    else if (method == "GET" && path == "/res.php")
                    {
                        HandleRes(stream, ParseQuery(queryString));
                    }
                    else if (method == "POST" && path == "/in.php")
                    {
                        request.Headers.TryGetValue("content-type", out var contentType);
                        HandleIn(stream, request.Body, contentType ?? string.Empty);
                    }
                    else
                    {
                        Send(stream, "ERROR_NOT_FOUND");
                    }
                }
            }
            catch
            {
                // Connection errors during a test shutdown are expected; ignore.
            }
        }

        private void HandleRes(NetworkStream stream, IReadOnlyDictionary<string, string> query)
        {
            query.TryGetValue("id", out var cid);
            cid ??= string.Empty;
            var wantJson = query.TryGetValue("json", out var jsonFlag) && jsonFlag == "1";
            var count = _pollCount.AddOrUpdate(cid, 1, (_, value) => value + 1);

            // CapSkip returns an empty 200 body when no result is available yet
            // (briefly right after submit, for an unknown id, or once a solved token
            // has already been read). It must be treated as "not ready".
            if (cid.StartsWith("empty", StringComparison.Ordinal) && count < 3)
            {
                Send(stream, string.Empty);
                return;
            }

            var notReady = cid.StartsWith("never", StringComparison.Ordinal)
                           || (cid.StartsWith("slow", StringComparison.Ordinal) && count < 2);

            if (notReady)
            {
                Send(
                    stream,
                    wantJson ? "{\"status\":0,\"request\":\"CAPCHA_NOT_READY\"}" : "CAPCHA_NOT_READY",
                    wantJson ? "application/json" : "text/plain");
            }
            else if (_idType.TryGetValue(cid, out var altchaType) && altchaType == "altcha")
            {
                // CapSkip emits a superset: the legacy status/request pair plus the
                // createTask-shaped solution object.
                if (wantJson)
                {
                    Send(
                        stream,
                        $"{{\"status\":1,\"request\":\"{AltchaToken}\",\"solution\":"
                        + $"{{\"token\":\"{AltchaToken}\",\"number\":{AltchaNumber}}}}}",
                        "application/json");
                }
                else
                {
                    Send(stream, $"OK|{AltchaToken}");
                }
            }
            else if (_idType.TryGetValue(cid, out var capyType) && capyType == "capy")
            {
                // The one method whose answer is an object rather than a string:
                // json=1 puts it straight into `request`, and plain text sends it as
                // a single line of JSON after OK|.
                if (wantJson)
                {
                    Send(
                        stream,
                        $"{{\"status\":1,\"request\":{CapySolutionJson},"
                        + $"\"solution\":{CapySolutionJson}}}",
                        "application/json");
                }
                else
                {
                    Send(stream, $"OK|{CapySolutionJson}");
                }
            }
            else if (_idType.TryGetValue(cid, out var foxType) && foxType == "captchafox")
            {
                // The UA is the browser's own, and CapSkip reports it at the top
                // level and inside solution both.
                if (wantJson)
                {
                    Send(
                        stream,
                        $"{{\"status\":1,\"request\":\"{CaptchaFoxToken}\","
                        + $"\"userAgent\":\"{CaptchaFoxUserAgent}\",\"solution\":"
                        + $"{{\"token\":\"{CaptchaFoxToken}\",\"userAgent\":\"{CaptchaFoxUserAgent}\"}}}}",
                        "application/json");
                }
                else
                {
                    Send(stream, $"OK|{CaptchaFoxToken}");
                }
            }
            else if (_idType.TryGetValue(cid, out var wafType) && wafType == "amazon_waf")
            {
                // What CapSkip sends: the bare status/request pair, with the
                // two-value object as `request` — no createTask `solution`.
                if (wantJson)
                {
                    Send(
                        stream,
                        $"{{\"status\":1,\"request\":{AmazonWafSolutionJson}}}",
                        "application/json");
                }
                else
                {
                    Send(stream, $"OK|{AmazonWafSolutionJson}");
                }
            }
            else if (_idType.TryGetValue(cid, out var ddType) && ddType == "datadome")
            {
                // As CapSkip answers: the cookie as `request`, the User-Agent it was
                // earned with, and `proxy` only when the app's own list supplied one.
                _submitted.TryGetValue(cid, out var sent);
                string? sentUserAgent = null;
                string? sentProxy = null;
                if (sent != null)
                {
                    if (!sent.TryGetValue("userAgent", out sentUserAgent) || sentUserAgent.Length == 0)
                    {
                        sent.TryGetValue("useragent", out sentUserAgent);
                    }

                    sent.TryGetValue("proxy", out sentProxy);
                }

                if (wantJson)
                {
                    var reply = new Dictionary<string, object>
                    {
                        ["status"] = 1,
                        ["request"] = DataDomeCookie,
                        ["userAgent"] = string.IsNullOrEmpty(sentUserAgent) ? DataDomeBrowserUserAgent : sentUserAgent!,
                    };
                    if (string.IsNullOrEmpty(sentProxy))
                    {
                        reply["proxy"] = DataDomeListProxy;
                    }

                    Send(stream, JsonSerializer.Serialize(reply), "application/json");
                }
                else
                {
                    Send(stream, $"OK|{DataDomeCookie}");
                }
            }
            else if (_idType.TryGetValue(cid, out var friendlyType) && friendlyType == "friendly_captcha")
            {
                if (wantJson)
                {
                    Send(
                        stream,
                        $"{{\"status\":1,\"request\":\"{FriendlyCaptchaToken}\",\"solution\":"
                        + $"{{\"token\":\"{FriendlyCaptchaToken}\"}}}}",
                        "application/json");
                }
                else
                {
                    Send(stream, $"OK|{FriendlyCaptchaToken}");
                }
            }
            else if (wantJson && _idType.TryGetValue(cid, out var type) && type == "turnstile")
            {
                Send(stream, $"{{\"status\":1,\"request\":\"{Code}\",\"useragent\":\"{UserAgent}\"}}", "application/json");
            }
            else if (wantJson)
            {
                Send(stream, $"{{\"status\":1,\"request\":\"{Code}\"}}", "application/json");
            }
            else
            {
                Send(stream, $"OK|{Code}");
            }
        }

        private void HandleIn(NetworkStream stream, byte[] body, string contentType)
        {
            Dictionary<string, string> fields;
            string key;
            if (contentType.StartsWith("multipart/form-data", StringComparison.OrdinalIgnoreCase))
            {
                fields = new Dictionary<string, string> { ["method"] = "post" };
                key = "capskip";
            }
            else
            {
                fields = ParseQuery(Encoding.UTF8.GetString(body));
                key = fields.TryGetValue("key", out var k) ? k : "capskip";
            }

            if (key == "badkey")
            {
                Send(stream, "ERROR_WRONG_USER_KEY");
                return;
            }

            var pageurl = fields.TryGetValue("pageurl", out var p) ? p : string.Empty;
            var id = Interlocked.Increment(ref _ids);
            string cid;
            if (pageurl.Contains("never"))
            {
                cid = $"never{id}";
            }
            else if (pageurl.Contains("slow"))
            {
                cid = $"slow{id}";
            }
            else if (pageurl.Contains("empty"))
            {
                cid = $"empty{id}";
            }
            else
            {
                cid = id.ToString();
            }

            _idType[cid] = fields.TryGetValue("method", out var method) ? method : string.Empty;
            _submitted[cid] = fields;

            // in.php returns JSON when the submit carried json=1, mirroring real CapSkip.
            if (fields.TryGetValue("json", out var jsonFlag) && jsonFlag == "1")
            {
                Send(stream, $"{{\"status\":1,\"request\":\"{cid}\"}}", "application/json");
            }
            else
            {
                Send(stream, $"OK|{cid}");
            }
        }

        /// <summary>
        /// The form fields in.php received for a captcha id, decoded the way a real
        /// server decodes them — so a test can check a value crossed the wire intact.
        /// </summary>
        public IReadOnlyDictionary<string, string> Submitted(string captchaId) => _submitted[captchaId];

        private static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            foreach (var pair in query.Split('&'))
            {
                if (pair.Length == 0)
                {
                    continue;
                }

                var equals = pair.IndexOf('=');
                var name = equals >= 0 ? pair.Substring(0, equals) : pair;
                var value = equals >= 0 ? pair.Substring(equals + 1) : string.Empty;
                result[Decode(name)] = Decode(value);
            }

            return result;
        }

        private static string Decode(string value) => Uri.UnescapeDataString(value.Replace('+', ' '));

        private static void Send(NetworkStream stream, string text, string contentType = "text/plain")
        {
            WriteResponse(stream, Encoding.UTF8.GetBytes(text), contentType);
        }

        private static void WriteResponse(NetworkStream stream, byte[] body, string contentType)
        {
            var header = "HTTP/1.1 200 OK\r\n"
                         + $"Content-Type: {contentType}\r\n"
                         + $"Content-Length: {body.Length}\r\n"
                         + "Connection: close\r\n\r\n";
            var headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(body, 0, body.Length);
            stream.Flush();
        }

        private static async Task<ParsedRequest> ReadRequestAsync(NetworkStream stream)
        {
            var buffer = new byte[8192];
            var data = new List<byte>();
            var headerEnd = -1;

            while (headerEnd < 0)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    data.Add(buffer[i]);
                }

                headerEnd = IndexOfHeaderEnd(data);
            }

            if (headerEnd < 0)
            {
                return new ParsedRequest(null, new Dictionary<string, string>(), Array.Empty<byte>());
            }

            var headerText = Encoding.ASCII.GetString(data.ToArray(), 0, headerEnd);
            var lines = headerText.Split(new[] { "\r\n" }, StringSplitOptions.None);
            var requestLine = lines.Length > 0 ? lines[0] : null;

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            for (var i = 1; i < lines.Length; i++)
            {
                var colon = lines[i].IndexOf(':');
                if (colon > 0)
                {
                    headers[lines[i].Substring(0, colon).Trim()] = lines[i].Substring(colon + 1).Trim();
                }
            }

            var contentLength = 0;
            if (headers.TryGetValue("content-length", out var length))
            {
                int.TryParse(length, out contentLength);
            }

            var bodyStart = headerEnd + 4;
            var body = new List<byte>();
            for (var i = bodyStart; i < data.Count; i++)
            {
                body.Add(data[i]);
            }

            while (body.Count < contentLength)
            {
                var read = await stream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                if (read == 0)
                {
                    break;
                }

                for (var i = 0; i < read; i++)
                {
                    body.Add(buffer[i]);
                }
            }

            return new ParsedRequest(requestLine, headers, body.ToArray());
        }

        private static int IndexOfHeaderEnd(List<byte> data)
        {
            for (var i = 0; i + 3 < data.Count; i++)
            {
                if (data[i] == 13 && data[i + 1] == 10 && data[i + 2] == 13 && data[i + 3] == 10)
                {
                    return i;
                }
            }

            return -1;
        }

        private sealed class ParsedRequest
        {
            public ParsedRequest(string? requestLine, Dictionary<string, string> headers, byte[] body)
            {
                RequestLine = requestLine;
                Headers = headers;
                Body = body;
            }

            public string? RequestLine { get; }

            public Dictionary<string, string> Headers { get; }

            public byte[] Body { get; }
        }
    }
}
