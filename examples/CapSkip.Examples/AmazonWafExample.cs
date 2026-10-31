using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve an AWS WAF CAPTCHA.
    ///
    /// AWS WAF guards a site in one of two ways, and the values you read off the
    /// target page depend on which one you are looking at:
    ///
    ///   * A challenge page — the HTTP 405 page whose source sets
    ///     <c>window.gokuProps = { key, iv, context }</c>. The sitekey is
    ///     <c>gokuProps.key</c>; <c>iv</c> and <c>context</c> are optional and travel
    ///     together (send both or neither). Pass <c>challenge_script</c> /
    ///     <c>captcha_script</c> with the full URLs of the page's challenge.js and
    ///     captcha.js.
    ///   * A widget page — the site calls <c>AwsWafCaptcha.renderCaptcha</c> itself.
    ///     The sitekey is the widget's <c>apiKey</c>; pass <c>jsapiScript</c> with
    ///     the full URL of jsapi.js, and no iv/context.
    ///
    /// Every script URL has to be an https URL on an awswaf.com host. CapSkip POSTs
    /// the solution to the host the script names, so the SDK refuses any other
    /// script before the request is made.
    ///
    /// Unlike reCAPTCHA or Turnstile, the answer is NOT a single token. It is a
    /// voucher and the token it was issued for, which only work as a pair: you trade
    /// them with AWS for the <c>aws-waf-token</c> cookie.
    ///
    /// The keys and tokens below are made-up placeholders in the real shapes — read
    /// the live values off your own target page.
    /// </summary>
    public static class AmazonWafExample
    {
        private const string PageUrl = "https://example.com/";

        // Challenge page: gokuProps.key, gokuProps.iv, gokuProps.context.
        private const string Sitekey =
            "AQIDAHjcYuEXAMPLEghicBgQ/7bFaQZ+m5FKCMDnO+vTbNg96AHDh0IR5vgzHNceHYqZR+GOAAAAfjB8";

        private const string Iv = "CgAFRDIjfgAAA5yY";

        private const string Context =
            "Rbp3r8fvbuhmrCqpUe8Mh1TLvs8OSKJZiSs2vCF4dt9rxVZI7xYJa2n5k/9r+XaXe7KUIu2I8nXTDHHkDvIOTDc2";

        private const string ChallengeJs =
            "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/challenge.js";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.AmazonWafAsync(Sitekey, PageUrl, new Dictionary<string, object?>
            {
                ["iv"] = Iv,
                ["context"] = Context,
                ["challenge_script"] = ChallengeJs,
            });

            Console.WriteLine($"Captcha ID:      {result.CaptchaId}");
            Console.WriteLine($"Captcha voucher: {Truncate(result.CaptchaVoucher, 48)}...");
            Console.WriteLine($"Existing token:  {Truncate(result.ExistingToken, 48)}...");

            // --- Leaving iv and context out ----------------------------------
            // CapSkip then reads them from the page at solve time, which is the safer
            // choice when your copy of the page is more than a few minutes old.
            //
            //   await solver.AmazonWafAsync(Sitekey, PageUrl);
            //
            // --- A widget page -----------------------------------------------
            //
            //   await solver.AmazonWafAsync(
            //       "Xr9kEXAMPLE2bWQx1vTq8LmZ0aYc4dHs7nFp3gJt6uKw5eRi9AbCdEfGh=_0_1",
            //       "https://example.com/login",
            //       new Dictionary<string, object?>
            //       {
            //           ["jsapiScript"] =
            //               "https://5b7e0a19c3d8.us-east-1.captcha-sdk.awswaf.com/5b7e0a19c3d8/jsapi.js",
            //       });
            //
            // --- With a per-request proxy ------------------------------------
            //
            //   ["proxy"] = new Proxy("HTTP", "login:password@1.2.3.4:8080"),

            // Trade the pair for the cookie: POST both, as JSON under their wire names,
            // to the voucher endpoint beside the page's challenge.js. The "token" in
            // the response is the aws-waf-token cookie for the protected site.
            //
            //   var voucherUrl = ChallengeJs.Substring(0, ChallengeJs.LastIndexOf('/') + 1) + "voucher";
            //   var body = JsonSerializer.Serialize(new Dictionary<string, string>
            //   {
            //       ["captcha_voucher"] = result.CaptchaVoucher!,
            //       ["existing_token"] = result.ExistingToken!,
            //   });
            //   var response = await http.PostAsync(
            //       voucherUrl, new StringContent(body, Encoding.UTF8, "application/json"));
            //   // read "token" from the JSON response -> the aws-waf-token cookie
            //
            // Exchange the pair as soon as it arrives rather than storing it for later.
            Console.WriteLine("Next:            POST both to the voucher endpoint beside challenge.js");
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
