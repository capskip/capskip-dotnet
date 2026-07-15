using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace CapSkip
{
    /// <summary>
    /// Low-level HTTP client for the CapSkip <c>in.php</c> / <c>res.php</c> endpoints.
    /// The <see cref="InAsync"/> and <see cref="ResAsync"/> methods are
    /// <see langword="virtual"/> so tests can substitute a mock client.
    /// </summary>
    public class ApiClient
    {
        /// <summary>The host CapSkip is listening on.</summary>
        public string Host { get; }

        /// <summary>The port CapSkip is listening on.</summary>
        public int Port { get; }

        /// <summary>Create a client pointed at a CapSkip host and port.</summary>
        public ApiClient(string host = "127.0.0.1", int port = 8080)
        {
            Host = host;
            Port = port;
        }

        /// <summary>The base URL, e.g. <c>http://127.0.0.1:8080</c>.</summary>
        public string BaseUrl => $"http://{Host}:{Port}";

        /// <summary>
        /// Submit a captcha to <c>in.php</c>. A <c>files</c> entry
        /// (<c>field -&gt; path</c>) or a single <c>file</c> path triggers a
        /// multipart upload; otherwise the fields are sent url-encoded.
        /// </summary>
        /// <param name="options">Form fields for the request.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The raw <c>in.php</c> response text.</returns>
        public virtual async Task<string> InAsync(
            IDictionary<string, object?> options,
            CancellationToken cancellationToken = default)
        {
            var fields = new Dictionary<string, object?>(options);
            var files = ExtractFiles(fields);
            var url = $"{BaseUrl}/in.php";

            // File reads happen before the request so a missing file surfaces as a
            // filesystem error rather than being masked as a NetworkException.
            Func<Task<HttpResponse>> send;
            if (files.Count > 0)
            {
                var parts = files
                    .Select(kv => new FilePart(kv.Key, Http.FileName(kv.Value), Http.ReadFile(kv.Value)))
                    .ToList();
                send = () => Http.PostMultipartAsync(url, fields, parts, cancellationToken);
            }
            else if (fields.ContainsKey("file"))
            {
                var path = Convert.ToString(fields["file"], CultureInfo.InvariantCulture) ?? string.Empty;
                fields.Remove("file");
                var content = Http.ReadFile(path);
                var parts = new[] { new FilePart("file", Http.FileName(path), content) };
                send = () => Http.PostMultipartAsync(url, fields, parts, cancellationToken);
            }
            else
            {
                send = () => Http.PostFormAsync(url, fields, cancellationToken);
            }

            HttpResponse resp;
            try
            {
                resp = await send().ConfigureAwait(false);
            }
            catch (Exception e) when (IsNetworkError(e))
            {
                throw new NetworkException(e);
            }

            return ReadBody(resp);
        }

        /// <summary>
        /// Poll a result from <c>res.php</c>.
        /// </summary>
        /// <param name="query">Query parameters (<c>key</c>, <c>action</c>, <c>id</c>, <c>json</c>).</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The raw <c>res.php</c> response text.</returns>
        public virtual async Task<string> ResAsync(
            IDictionary<string, object?> query,
            CancellationToken cancellationToken = default)
        {
            HttpResponse resp;
            try
            {
                resp = await Http.GetAsync($"{BaseUrl}/res.php", query, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception e) when (IsNetworkError(e))
            {
                throw new NetworkException(e);
            }

            return ReadBody(resp);
        }

        private static string ReadBody(HttpResponse resp)
        {
            if (resp.StatusCode != 200)
            {
                throw new NetworkException($"bad response: {resp.StatusCode}");
            }

            var text = resp.Text;
            if (text.Contains("ERROR"))
            {
                throw new ApiException(text);
            }

            return text;
        }

        private static Dictionary<string, string> ExtractFiles(IDictionary<string, object?> fields)
        {
            var result = new Dictionary<string, string>();
            if (!fields.TryGetValue("files", out var value))
            {
                return result;
            }

            fields.Remove("files");

            switch (value)
            {
                case null:
                    break;
                case IDictionary<string, string> typed:
                    foreach (var kv in typed)
                    {
                        result[kv.Key] = kv.Value;
                    }

                    break;
                case IDictionary<string, object?> loose:
                    foreach (var kv in loose)
                    {
                        result[kv.Key] = Convert.ToString(kv.Value, CultureInfo.InvariantCulture) ?? string.Empty;
                    }

                    break;
                default:
                    throw new ValidationException("'files' must be a map of field name to file path.");
            }

            return result;
        }

        private static bool IsNetworkError(Exception e)
        {
            return e is HttpRequestException
                   || e is SocketException
                   || e is TaskCanceledException
                   || e is OperationCanceledException
                   || e is IOException;
        }
    }
}
