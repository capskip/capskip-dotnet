using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Pass a DataDome challenge and fetch the page with the cookie.
    ///
    /// DataDome answers a request it doubts with a block page (status 403,
    /// sometimes 401) instead of the site. That page carries a <c>var dd={...}</c>
    /// object and loads the challenge in an iframe from geo.captcha-delivery.com: a
    /// slider under /captcha/, or a few-second device check under /interstitial/.
    /// CapSkip passes either and returns the <c>datadome</c> cookie.
    ///
    /// The cookie belongs to one IP address and one User-Agent, so:
    ///
    ///   * solve through the proxy your requests use (or none, if they leave from
    ///     the machine running CapSkip), and
    ///   * send the User-Agent CapSkip reports back with every request.
    ///
    /// When you send no proxy and the CapSkip app has its own DataDome proxy list,
    /// it solves through one of those and the result names it as
    /// <c>Proxy</c> (host:port): the cookie then only works through that proxy.
    ///
    /// Environment:
    ///   TARGET_URL      a page behind DataDome (default: https://www.example.com/)
    ///   DATADOME_PROXY  optional, e.g. http://login:password@203.0.113.10:8080
    ///   CAPSKIP_API_KEY / CAPSKIP_HOST / CAPSKIP_PORT
    /// </summary>
    public static class DataDomeExample
    {
        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();
            var pageUrl = Environment.GetEnvironmentVariable("TARGET_URL") ?? "https://www.example.com/";

            // scheme://[login:password@]host:port, or unset
            var proxyUrl = Environment.GetEnvironmentVariable("DATADOME_PROXY");
            if (string.IsNullOrEmpty(proxyUrl))
            {
                proxyUrl = null;
            }

            // 1. Request the page. A 403/401 with a dd object is DataDome's block page.
            string html;
            HttpStatusCode status;
            using (var http = MakeHttpClient(proxyUrl))
            {
                using var response = await http.GetAsync(pageUrl);
                status = response.StatusCode;
                html = await response.Content.ReadAsStringAsync();
            }

            var captchaUrl = CaptchaUrlFromBlockPage(html, pageUrl);
            if ((status != HttpStatusCode.Forbidden && status != HttpStatusCode.Unauthorized) || captchaUrl is null)
            {
                Console.WriteLine($"No DataDome block page at {pageUrl} (HTTP {(int)status}).");
                return;
            }

            Console.WriteLine($"Block page: {Truncate(captchaUrl, 90)}...");

            // 2. Solve it. 2Captcha's argument order: captcha_url and pageurl, then the
            //    optional userAgent and proxy. No userAgent: CapSkip uses its own
            //    browser's and reports it. With one, it must be Chrome for Windows with
            //    the same major version as that browser.
            var options = new Dictionary<string, object?>();
            if (proxyUrl != null)
            {
                var proxyUri = new Uri(proxyUrl);
                options["proxy"] = new Proxy(
                    proxyUri.Scheme.ToUpperInvariant(),
                    proxyUrl.Substring(proxyUrl.IndexOf("://", StringComparison.Ordinal) + 3));
            }

            var result = await solver.DataDomeAsync(captchaUrl, pageUrl, options);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Cookie:     {Truncate(result.Cookie, 60)}...");
            Console.WriteLine($"User-Agent: {result.UserAgent}");
            if (!string.IsNullOrEmpty(result.Proxy))
            {
                Console.WriteLine(
                    $"Proxy:      {result.Proxy} (from the CapSkip app DataDome list; the cookie only works through it)");
            }

            // 3. Repeat the request with the cookie, the same User-Agent and the same IP.
            var via = proxyUrl;
            if (!string.IsNullOrEmpty(result.Proxy) && proxyUrl is null)
            {
                // Named without its login: add yours if that list entry has one.
                via = "http://" + result.Proxy;
            }

            using (var http = MakeHttpClient(via))
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, pageUrl);
                request.Headers.TryAddWithoutValidation("User-Agent", result.UserAgent);

                // Cookie is the datadome=... pair; Code keeps the attributes DataDome
                // set (Domain, Max-Age...) if you would rather fill a cookie jar.
                request.Headers.TryAddWithoutValidation("Cookie", result.Cookie);
                using var again = await http.SendAsync(request);
                Console.WriteLine($"With the cookie: HTTP {(int)again.StatusCode}");
            }
        }

        /// <summary>The challenge iframe address, as the block page's own script builds it.</summary>
        public static string? CaptchaUrlFromBlockPage(string html, string pageUrl)
        {
            var iframe = Regex.Match(html, "<iframe[^>]+src=\"(https://[^\"]*captcha-delivery\\.com/[^\"]+)\"");
            if (iframe.Success)
            {
                return iframe.Groups[1].Value; // HTML-escaped &amp; is fine, CapSkip decodes it
            }

            var match = Regex.Match(html, @"var dd=(\{[^}]*\})");
            if (!match.Success)
            {
                return null;
            }

            using var doc = JsonDocument.Parse(match.Groups[1].Value.Replace('\'', '"'));
            var dd = doc.RootElement;
            string? Get(string name) =>
                dd.TryGetProperty(name, out var value)
                    ? (value.ValueKind == JsonValueKind.String ? value.GetString() : value.GetRawText())
                    : null;

            var path = Get("rt") == "i" ? "interstitial" : "captcha";
            var query = new List<KeyValuePair<string, string?>>
            {
                new KeyValuePair<string, string?>("initialCid", Get("cid")),
                new KeyValuePair<string, string?>("hash", Get("hsh")),
                new KeyValuePair<string, string?>("cid", Get("cookie")),
            };
            if (!string.IsNullOrEmpty(Get("t")))
            {
                query.Add(new KeyValuePair<string, string?>("t", Get("t")));
            }

            query.Add(new KeyValuePair<string, string?>("referer", pageUrl));
            query.Add(new KeyValuePair<string, string?>("s", Get("s")));
            foreach (var key in new[] { "e", "b" })
            {
                if (!string.IsNullOrEmpty(Get(key)))
                {
                    query.Add(new KeyValuePair<string, string?>(key, Get(key)));
                }
            }

            query.Add(new KeyValuePair<string, string?>("dm", "cd"));

            var host = Get("host") ?? "geo.captcha-delivery.com";
            var queryString = string.Join(
                "&",
                query.Select(kv => kv.Key + "=" + Uri.EscapeDataString(kv.Value ?? string.Empty)));
            return $"https://{host}/{path}/?{queryString}";
        }

        private static HttpClient MakeHttpClient(string? proxyUrl)
        {
            // UseCookies off, so the Cookie header set by hand goes out as it is.
            var handler = new HttpClientHandler { UseCookies = false };
            if (proxyUrl != null)
            {
                var uri = new Uri(proxyUrl);
                var proxy = new WebProxy(new Uri($"{uri.Scheme}://{uri.Host}:{uri.Port}"));
                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var parts = uri.UserInfo.Split(new[] { ':' }, 2);
                    proxy.Credentials = new NetworkCredential(
                        Uri.UnescapeDataString(parts[0]),
                        parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty);
                }

                handler.Proxy = proxy;
                handler.UseProxy = true;
            }

            return new HttpClient(handler) { Timeout = TimeSpan.FromSeconds(30) };
        }

        private static string Truncate(string? value, int length)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value!.Length <= length ? value : value.Substring(0, length);
        }
    }
}
