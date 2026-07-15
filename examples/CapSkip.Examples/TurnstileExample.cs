using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    internal static class TurnstileExample
    {
        // Cloudflare's official Turnstile test key (always passes) and demo page — safe to run as-is.
        private const string SiteKey = "1x00000000000000000000AA";
        private const string PageUrl = "https://demo.turnstile.workers.dev/";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.TurnstileAsync(SiteKey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {result.Code}");
            Console.WriteLine($"User-Agent: {result.UserAgent}");
        }
    }
}
