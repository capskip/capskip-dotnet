using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve a GeeTest v3 slider.
    ///
    /// GeeTest v3 needs two values from the target site:
    ///
    ///   * <c>gt</c>        static per site
    ///   * <c>challenge</c> single-use, expires in about a minute
    ///
    /// The site fetches them itself from an endpoint that returns
    /// <c>{"gt": "...", "challenge": "..."}</c> (often <c>.../register.php</c> or a
    /// <c>gettype</c>/<c>get.php</c> request). Open DevTools → Network to find that
    /// request, then request a *fresh* pair right before solving, as this example does.
    /// </summary>
    internal static class GeetestExample
    {
        // A public GeeTest v3 demo page, and the endpoint that page calls to issue a
        // fresh gt/challenge pair. Safe to run as-is.
        private const string PageUrl = "https://2captcha.com/demo/geetest";
        private const string RegisterUrl = "https://2captcha.com/api/v1/captcha-demo/gee-test/init-params";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var (gt, challenge) = await FetchChallengeAsync();

            var result = await solver.GeetestAsync(gt, challenge, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Challenge:  {result.Challenge}");
            Console.WriteLine($"Validate:   {result.Validate}");
            Console.WriteLine($"Seccode:    {result.Seccode}");

            // Code holds the same answer as a raw JSON string, which is what you forward
            // if you are porting code written against another solver's API.
            Console.WriteLine($"Raw code:   {result.Code}");

            // Post these back exactly as the site's own front-end would, e.g.:
            //
            //   new FormUrlEncodedContent(new Dictionary<string, string>
            //   {
            //       ["geetest_challenge"] = result.Challenge,
            //       ["geetest_validate"]  = result.Validate,
            //       ["geetest_seccode"]   = result.Seccode,
            //   });
        }

        /// <summary>Get a fresh gt/challenge pair. Replace with the endpoint your target uses.</summary>
        private static async Task<(string Gt, string Challenge)> FetchChallengeAsync()
        {
            using var http = new HttpClient();
            var body = await http.GetStringAsync(RegisterUrl);

            using var doc = JsonDocument.Parse(body);
            var gt = doc.RootElement.GetProperty("gt").GetString();
            var challenge = doc.RootElement.GetProperty("challenge").GetString();

            if (string.IsNullOrEmpty(gt) || string.IsNullOrEmpty(challenge))
            {
                throw new InvalidOperationException($"Could not read a gt/challenge pair from {RegisterUrl}");
            }

            return (gt!, challenge!);
        }
    }
}
