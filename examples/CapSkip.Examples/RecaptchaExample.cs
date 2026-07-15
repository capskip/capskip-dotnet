using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    internal static class RecaptchaExample
    {
        // Google's official reCAPTCHA v2 test key and demo page — safe to run as-is.
        private const string SiteKey = "6LeIxAcTAAAAAJcZVRqyHh71UMIEGNQ_MXjiZKhI";
        private const string PageUrl = "https://www.google.com/recaptcha/api2/demo";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.RecaptchaAsync(SiteKey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {result.Code}");
        }
    }
}
