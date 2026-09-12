using System;
using System.Collections.Generic;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve an ALTCHA proof-of-work challenge.
    ///
    /// ALTCHA is not a recognition captcha — there is nothing to read. The site
    /// issues a challenge and the browser must brute-force a number that satisfies
    /// it. CapSkip does that work for you, in milliseconds.
    ///
    /// You need the challenge, in one of two forms:
    ///
    ///   * <c>challenge_url</c>  the endpoint that serves it; CapSkip fetches it
    ///   * <c>challenge_json</c> the challenge document itself, if you already have it
    ///
    /// To find them, open DevTools → Network on the target page and look for the
    /// request the <c>&lt;altcha-widget&gt;</c> makes for its challenge (often
    /// something like <c>/altcha/challenge</c>). The request URL is your
    /// <c>challenge_url</c>; its JSON response is your <c>challenge_json</c>.
    ///
    /// Note the widget attribute that names the endpoint changed between versions:
    /// v1/v2 use <c>challengeurl="..."</c>, while v3+ uses <c>challenge="..."</c> for
    /// both a URL and inline data. Read the page source rather than assuming.
    ///
    /// Challenges expire fast — some sites inside two minutes — so fetch one
    /// immediately before solving and post the token promptly. An expired challenge
    /// is rejected with a bare "verification failed" that looks exactly like a wrong
    /// answer.
    ///
    /// This example issues its own challenge the way a site's server would, so it
    /// runs as-is with no third-party dependency. Swap in your target's endpoint to
    /// use it for real.
    /// </summary>
    internal static class AltchaExample
    {
        private const string PageUrl = "https://example.com/signup";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            // Set CAPSKIP_ALTCHA_CHALLENGE_URL to point this at your own target's
            // challenge endpoint; otherwise it solves a challenge minted below.
            var challengeUrl = Environment.GetEnvironmentVariable("CAPSKIP_ALTCHA_CHALLENGE_URL");

            var options = string.IsNullOrEmpty(challengeUrl)
                // --- Option A: you already have the challenge document -------------
                // No network request at all: CapSkip solves it locally.
                ? new Dictionary<string, object?> { ["challenge_json"] = IssueChallenge() }
                // --- Option B: let CapSkip fetch the challenge ----------------------
                : new Dictionary<string, object?> { ["challenge_url"] = challengeUrl };

            var result = await solver.AltchaAsync(PageUrl, options);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Number:     {result.Number}");
            Console.WriteLine($"Token:      {result.Token?.Substring(0, 60)}...");

            // Add a proxy when the challenge endpoint should be fetched from a
            // particular IP — it is used only for that fetch, never for the solve:
            //
            //   ["proxy"] = new Proxy("HTTP", "login:password@1.2.3.4:8080"),

            // Post the token back in the form field the widget uses, named `altcha`:
            //
            //   new FormUrlEncodedContent(new Dictionary<string, string>
            //   {
            //       ["email"] = "someone@example.com",
            //       ["altcha"] = result.Token!,
            //   });
            //
            // Do not re-encode, trim or re-order it. The token is base64 of a JSON
            // document whose fields are covered by the server's HMAC signature, so any
            // modification invalidates it.

            // Code holds the same string as Token, which is what you forward if you
            // are porting code written against another solver's API.
            Console.WriteLine($"Code == Token: {result.Code == result.Token}");
        }

        /// <summary>
        /// Mint an ALTCHA challenge, exactly as a site's own server would.
        ///
        /// Replace this with a fetch of your target's challenge endpoint — or skip it
        /// entirely and pass <c>challenge_url</c> so CapSkip does the fetching.
        /// </summary>
        private static Dictionary<string, object> IssueChallenge(int number = 54321)
        {
            var saltBytes = new byte[12];
            using (var rng = RandomNumberGenerator.Create())
            {
                rng.GetBytes(saltBytes);
            }

            var expires = DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 600;
            var salt = $"{Convert.ToHexString(saltBytes).ToLowerInvariant()}?expires={expires}";

            using var sha = SHA256.Create();
            var digest = sha.ComputeHash(Encoding.UTF8.GetBytes(
                salt + number.ToString(CultureInfo.InvariantCulture)));

            return new Dictionary<string, object>
            {
                ["algorithm"] = "SHA-256",
                ["challenge"] = Convert.ToHexString(digest).ToLowerInvariant(),
                ["salt"] = salt,
                ["signature"] = new string('0', 64),
                ["maxnumber"] = 100000,
            };
        }
    }
}
