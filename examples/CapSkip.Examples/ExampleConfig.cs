using System;
using CapSkip;

namespace CapSkip.Examples
{
    /// <summary>Shared configuration for the examples, read from environment variables.</summary>
    internal static class ExampleConfig
    {
        public static string ApiKey => Environment.GetEnvironmentVariable("CAPSKIP_API_KEY") ?? "capskip";

        public static string Host => Environment.GetEnvironmentVariable("CAPSKIP_HOST") ?? "127.0.0.1";

        public static int Port =>
            int.TryParse(Environment.GetEnvironmentVariable("CAPSKIP_PORT"), out var port) ? port : 8080;

        public static CapSkipClient CreateClient() => new CapSkipClient(apiKey: ApiKey, host: Host, port: Port);
    }
}
