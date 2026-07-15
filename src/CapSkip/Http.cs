using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CapSkip
{
    /// <summary>The outcome of an HTTP request: a status code plus the raw body bytes.</summary>
    internal sealed class HttpResponse
    {
        public int StatusCode { get; }

        public byte[] Body { get; }

        public HttpResponse(int statusCode, byte[] body)
        {
            StatusCode = statusCode;
            Body = body;
        }

        /// <summary>The body decoded as UTF-8 text.</summary>
        public string Text => Encoding.UTF8.GetString(Body);
    }

    /// <summary>A single file part in a multipart/form-data upload.</summary>
    internal sealed class FilePart
    {
        public string Name { get; }

        public string FileName { get; }

        public byte[] Content { get; }

        public FilePart(string name, string fileName, byte[] content)
        {
            Name = name;
            FileName = fileName;
            Content = content;
        }
    }

    /// <summary>
    /// Thin HTTP layer built on <see cref="System.Net.Http.HttpClient"/>. CapSkip only ever
    /// talks to a local endpoint (in.php / res.php) plus the occasional image
    /// download, so a small request helper is all the SDK needs.
    /// </summary>
    internal static class Http
    {
        // A single shared client, as recommended for HttpClient. Redirects are
        // followed by default, matching the other CapSkip SDKs.
        private static readonly HttpClient Client = new HttpClient();

        /// <summary>Render a value the way the form/query encoders expect (invariant, null -&gt; "").</summary>
        public static string Stringify(object? value)
        {
            switch (value)
            {
                case null:
                    return string.Empty;
                case string s:
                    return s;
                case bool b:
                    // Match JavaScript/Python truthiness on the wire.
                    return b ? "true" : "false";
                case IFormattable formattable:
                    return formattable.ToString(null, CultureInfo.InvariantCulture);
                default:
                    return value.ToString() ?? string.Empty;
            }
        }

        /// <summary>Build a <c>?a=b&amp;c=d</c> query string (empty when there are no params).</summary>
        public static string BuildQuery(IEnumerable<KeyValuePair<string, object?>> parameters)
        {
            var pairs = parameters
                .Select(kv => $"{Uri.EscapeDataString(kv.Key)}={Uri.EscapeDataString(Stringify(kv.Value))}")
                .ToList();
            return pairs.Count > 0 ? "?" + string.Join("&", pairs) : string.Empty;
        }

        /// <summary>GET a URL with an object of query parameters.</summary>
        public static Task<HttpResponse> GetAsync(
            string url,
            IEnumerable<KeyValuePair<string, object?>>? parameters = null,
            CancellationToken cancellationToken = default)
        {
            var query = parameters is null ? string.Empty : BuildQuery(parameters);
            return SendAsync(new HttpRequestMessage(HttpMethod.Get, url + query), cancellationToken);
        }

        /// <summary>POST an application/x-www-form-urlencoded body.</summary>
        public static Task<HttpResponse> PostFormAsync(
            string url,
            IEnumerable<KeyValuePair<string, object?>> fields,
            CancellationToken cancellationToken = default)
        {
            var encoded = new FormUrlEncodedContent(
                fields.Select(kv => new KeyValuePair<string, string>(kv.Key, Stringify(kv.Value))));
            return SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = encoded }, cancellationToken);
        }

        /// <summary>
        /// POST a multipart/form-data body carrying one or more file parts. The body is
        /// hand-built (field parts carry no per-part Content-Type) so it matches what
        /// Python's requests / Node send — the shape CapSkip's uploader accepts.
        /// </summary>
        public static Task<HttpResponse> PostMultipartAsync(
            string url,
            IEnumerable<KeyValuePair<string, object?>> fields,
            IEnumerable<FilePart> files,
            CancellationToken cancellationToken = default)
        {
            var boundary = "----CapSkipFormBoundary" + Guid.NewGuid().ToString("N");
            using var buffer = new MemoryStream();

            void WriteAscii(string text)
            {
                var bytes = Encoding.UTF8.GetBytes(text);
                buffer.Write(bytes, 0, bytes.Length);
            }

            foreach (var field in fields)
            {
                WriteAscii($"--{boundary}\r\n");
                WriteAscii($"Content-Disposition: form-data; name=\"{field.Key}\"\r\n\r\n");
                WriteAscii($"{Stringify(field.Value)}\r\n");
            }

            foreach (var file in files)
            {
                WriteAscii($"--{boundary}\r\n");
                WriteAscii($"Content-Disposition: form-data; name=\"{file.Name}\"; filename=\"{file.FileName}\"\r\n");
                WriteAscii("Content-Type: application/octet-stream\r\n\r\n");
                buffer.Write(file.Content, 0, file.Content.Length);
                WriteAscii("\r\n");
            }

            WriteAscii($"--{boundary}--\r\n");

            var content = new ByteArrayContent(buffer.ToArray());
            content.Headers.TryAddWithoutValidation("Content-Type", $"multipart/form-data; boundary={boundary}");
            return SendAsync(new HttpRequestMessage(HttpMethod.Post, url) { Content = content }, cancellationToken);
        }

        private static async Task<HttpResponse> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            using (request)
            using (var response = await Client.SendAsync(request, cancellationToken).ConfigureAwait(false))
            {
                var body = await response.Content.ReadAsByteArrayAsync().ConfigureAwait(false);
                return new HttpResponse((int)response.StatusCode, body);
            }
        }

        /// <summary>Read a file as bytes (kept here so callers share one IO surface).</summary>
        public static byte[] ReadFile(string path) => File.ReadAllBytes(path);

        /// <summary>The base name of a path, used as the multipart <c>filename</c>.</summary>
        public static string FileName(string path) => Path.GetFileName(path);
    }
}
