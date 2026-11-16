using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve a GeeTest v4 CAPTCHA.
    ///
    /// GeeTest v4 identifies its widget with a single public <c>captcha_id</c>.
    /// Unlike v3 there is no per-request challenge to fetch first: the ID is the
    /// same for every visitor, so read it once and reuse it.
    ///
    /// The page loads <c>https://static.geetest.com/v4/gt4.js</c> and passes the ID
    /// to <c>initGeetest4</c> as <c>captchaId</c>:
    ///
    ///   <c>initGeetest4({ captchaId: "e392e1d7fd421dc63325744d5a2b9c73", product: "bind" }, ...);</c>
    ///
    /// If the call is built at runtime, open the Network tab and find the request
    /// to <c>gcaptcha4.geetest.com/load</c>: the ID is its <c>captcha_id</c> query
    /// parameter.
    ///
    /// A passed check produces five values, which the site verifies with GeeTest on
    /// its server: <c>captcha_id</c>, <c>lot_number</c>, <c>pass_token</c>,
    /// <c>gen_time</c> and <c>captcha_output</c>. CapSkip returns all five.
    /// </summary>
    public static class GeetestV4Example
    {
        // GeeTest's own public v4 demo page and its captcha_id.
        private const string CaptchaId = "54088bb07d2df3c46b79f80300b0abbe";
        private const string PageUrl = "https://gt4.geetest.com/";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.GeetestV4Async(CaptchaId, PageUrl);

            Console.WriteLine($"Captcha ID:     {result.CaptchaId}");
            Console.WriteLine($"captcha_id:     {result.GeetestCaptchaId}");
            Console.WriteLine($"lot_number:     {result.LotNumber}");
            Console.WriteLine($"pass_token:     {result.PassToken}");
            Console.WriteLine($"gen_time:       {result.GenTime}");
            Console.WriteLine($"captcha_output: {Truncate(result.CaptchaOutput, 40)}...");

            // --- Optional parameters -----------------------------------------
            //
            //   var result = await solver.GeetestV4Async(CaptchaId, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["risk_type"] = "slide",    // slide, icon, nine, word, phrase, match, winlinze,
            //                                   // svg_seed, svg_icon or ai; applied only where the
            //                                   // site allows it
            //       ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
            //   });
            //
            // --- Submitting the result ---------------------------------------
            //
            // Send the five values under the same names in the request the page's
            // own submit makes, unchanged, and solve again for every submission:
            //
            //   var form = new FormUrlEncodedContent(new Dictionary<string, string>
            //   {
            //       ["captcha_id"] = result.GeetestCaptchaId!,
            //       ["lot_number"] = result.LotNumber!,
            //       ["pass_token"] = result.PassToken!,
            //       ["gen_time"] = result.GenTime!,
            //       ["captcha_output"] = result.CaptchaOutput!,
            //   });
            //   await http.PostAsync(loginUrl, form);
            //
            // If GeeTest itself could not be reached, result.IsOffline is true, and
            // the site decides whether to accept the pass.
            if (result.IsOffline == true)
            {
                Console.WriteLine("GeeTest passed the widget in offline mode.");
            }
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
