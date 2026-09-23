using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve a Capy Puzzle captcha.
    ///
    /// Capy is a slide puzzle: a piece has to be dragged into the hole cut out of a
    /// photograph. CapSkip locates the hole and produces the drag path, which takes
    /// one HTTP fetch and about a tenth of a second of pixel math — no browser.
    ///
    /// You need two values, both read off the target page:
    ///
    ///   * <c>captchakey</c> the site's public Capy key, prefixed <c>PUZZLE_</c>.
    ///     Look for <c>capy_captchakey</c> in the page source, or read it out of the
    ///     widget script URL:
    ///     <c>&lt;script src="https://jp.api.capy.me/puzzle/get_js/?k=PUZZLE_XXXX"&gt;</c>
    ///   * <c>pageurl</c>    the full URL of the page the captcha is on
    ///
    /// Optionally <c>api_server</c>, the root of the Capy API the key lives behind,
    /// taken from that same script URL. CapSkip defaults to
    /// <c>https://jp.api.capy.me</c>. Note that <c>api.capy.me</c> no longer
    /// resolves, although several solver services still document it — if a site's
    /// widget points elsewhere, pass that.
    ///
    /// Unlike reCAPTCHA or Turnstile, the answer is NOT a single token. It is three
    /// values that together go into the target form, and all three must be submitted.
    /// </summary>
    public static class CapyExample
    {
        private const string CaptchaKey = "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v";
        private const string PageUrl = "https://example.com/login";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.CapyAsync(CaptchaKey, PageUrl);

            Console.WriteLine($"Captcha ID:    {result.CaptchaId}");
            Console.WriteLine($"Captcha key:   {result.CaptchaKey}");
            Console.WriteLine($"Challenge key: {result.ChallengeKey}");
            Console.WriteLine($"Answer:        {Truncate(result.Answer, 48)}...");

            // --- With a non-default API server -------------------------------
            //
            //   await solver.CapyAsync(CaptchaKey, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["api_server"] = "https://jp.api.capy.me/",
            //   });
            //
            // --- With a per-request proxy ------------------------------------
            // The proxy is used for the puzzle-image fetch, which is the only request
            // a Capy solve makes. Worth configuring once you solve at any volume: a
            // steady stream of puzzle draws from one address is the pattern rate
            // limiting exists to catch.
            //
            //   ["proxy"] = new Proxy("HTTP", "login:password@1.2.3.4:8080"),

            // Post all three values back in the fields the Capy widget would have
            // filled in. Submit them verbatim — Answer is the drag path the widget
            // would have recorded, and the site's backend verifies it against the
            // challenge it issued, so trimming or re-encoding it invalidates the solve.
            //
            //   new FormUrlEncodedContent(new Dictionary<string, string>
            //   {
            //       ["email"] = "someone@example.com",
            //       ["capy_captchakey"] = result.CaptchaKey!,
            //       ["capy_challengekey"] = result.ChallengeKey!,
            //       ["capy_answer"] = result.Answer!,
            //   });
            Console.WriteLine("Form fields:   capy_captchakey, capy_challengekey, capy_answer");

            // The challenge key is single-use and short-lived: CapSkip generates it at
            // solve time and the puzzle is bound to it. Submit promptly rather than
            // caching these values or reusing a challengekey for a second submission.
            //
            // One more thing worth knowing if you ever build against Capy yourself: it
            // grades the wall-clock gap between issuing the puzzle and verifying the
            // answer, and refuses anything superhuman with "CAPTCHA verification
            // failed" — the same message a wrong answer gets. CapSkip therefore holds
            // every result until two seconds have elapsed, so a solve takes about that
            // long rather than a fifth of a second. Nothing to configure; just do not
            // be surprised by the latency.

            // RespKey comes back empty for a puzzle solve. It exists for shape
            // compatibility with 2Captcha's documented response and carries nothing.
            Console.WriteLine($"respKey empty: {result.RespKey == string.Empty}");
        }

        private static string Truncate(string? value, int length)
        {
            if (string.IsNullOrEmpty(value))
            {
                return string.Empty;
            }

            return value!.Length <= length ? value : value.Substring(0, length);
        }
    }
}
