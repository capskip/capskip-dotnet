namespace CapSkip
{
    /// <summary>
    /// A proxy passed to reCAPTCHA / Turnstile solves. Mirrors the
    /// <c>{ "type": "...", "uri": "..." }</c> dictionary form accepted by the
    /// other CapSkip SDKs, and is expanded into the <c>proxy</c> + <c>proxytype</c>
    /// fields CapSkip's API expects.
    /// </summary>
    public sealed class Proxy
    {
        /// <summary>Proxy type: <c>HTTP</c>, <c>HTTPS</c>, <c>SOCKS5</c>, or <c>SOCKS5H</c>.</summary>
        public string Type { get; set; } = "HTTP";

        /// <summary>Proxy address: <c>login:password@host:port</c> or a bare <c>host:port</c>.</summary>
        public string Uri { get; set; } = "";

        /// <summary>Create an empty proxy (set <see cref="Type"/> and <see cref="Uri"/> via initializer).</summary>
        public Proxy()
        {
        }

        /// <summary>Create a proxy from a type and a URI.</summary>
        public Proxy(string type, string uri)
        {
            Type = type;
            Uri = uri;
        }
    }
}
