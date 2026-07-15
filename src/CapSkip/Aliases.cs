namespace CapSkip
{
    /// <summary>
    /// Alias of <see cref="CapSkipClient"/>. .NET I/O is asynchronous by nature, so
    /// every CapSkip method already returns a <see cref="System.Threading.Tasks.Task"/> — this
    /// type exists so code ported from other CapSkip SDKs (which distinguish a
    /// synchronous and an asynchronous client) keeps working unchanged.
    /// </summary>
    public sealed class AsyncCapSkip : CapSkipClient
    {
        /// <inheritdoc cref="CapSkipClient(string, string, int, double, double, double)"/>
        public AsyncCapSkip(
            string apiKey = "capskip",
            string host = "127.0.0.1",
            int port = 8080,
            double defaultTimeout = 120,
            double recaptchaTimeout = 300,
            double pollingInterval = 5)
            : base(apiKey, host, port, defaultTimeout, recaptchaTimeout, pollingInterval)
        {
        }
    }

    /// <summary>Alias of <see cref="ApiClient"/> for cross-SDK parity.</summary>
    public sealed class AsyncApiClient : ApiClient
    {
        /// <inheritdoc cref="ApiClient(string, int)"/>
        public AsyncApiClient(string host = "127.0.0.1", int port = 8080)
            : base(host, port)
        {
        }
    }
}
