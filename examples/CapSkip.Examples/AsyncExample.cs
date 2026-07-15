using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapSkip;

namespace CapSkip.Examples
{
    internal static class AsyncExample
    {
        // Official public test keys and demo pages — safe to run as-is.
        private const string RecaptchaSiteKey = "6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI"; // reCAPTCHA v2 test key
        private const string RecaptchaUrl = "https://www.google.com/recaptcha/api2/demo";
        private const string TurnstileSiteKey = "1x00000000000000000000AA"; // Turnstile test key (always passes)
        private const string TurnstileUrl = "https://demo.turnstile.workers.dev/";

        public static async Task RunAsync()
        {
            // AsyncCapSkip is an alias of CapSkipClient, kept for parity with the other
            // CapSkip SDKs. Every method already returns a Task.
            var solver = new AsyncCapSkip(
                apiKey: ExampleConfig.ApiKey,
                host: ExampleConfig.Host,
                port: ExampleConfig.Port);

            // Kick all three off at once, then await each — they solve concurrently.
            var jobs = new (string Name, Task<SolveResult> Task)[]
            {
                ("recaptcha v2", solver.RecaptchaAsync(RecaptchaSiteKey, RecaptchaUrl)),
                ("recaptcha v3", solver.RecaptchaAsync(RecaptchaSiteKey, RecaptchaUrl, new Dictionary<string, object?>
                {
                    ["version"] = "v3",
                    ["action"] = "submit",
                    ["score"] = 0.7,
                })),
                ("turnstile", solver.TurnstileAsync(TurnstileSiteKey, TurnstileUrl)),
            };

            foreach (var (name, task) in jobs)
            {
                try
                {
                    var result = await task;
                    Console.WriteLine($"{name}: {result}");
                }
                catch (CapSkipError error)
                {
                    Console.WriteLine($"{name}: {error.GetType().Name}: {error.Message}");
                }
            }
        }
    }
}
