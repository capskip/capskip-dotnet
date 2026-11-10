using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve an MTCaptcha text captcha.
    ///
    /// MTCaptcha is a text captcha used on login, sign-up and contact forms: the
    /// visitor types the distorted characters in a picture, and the widget writes a
    /// verified token into a hidden form field named <c>mtcaptcha-verifiedtoken</c>.
    /// CapSkip takes the sitekey and page address and returns that token.
    ///
    /// You need two values, both read off the target page:
    ///
    ///   * <c>sitekey</c> starts with <c>MTPublic-</c>. Most pages set it in a
    ///     <c>mtcaptchaConfig</c> object in the page source:
    ///     <c>var mtcaptchaConfig = { "sitekey": "MTPublic-KzqLY1cKH", "action": "login" };</c>
    ///     When the widget is on screen it is also the <c>sitekey</c> query
    ///     parameter of its iframe on <c>service.mtcaptcha.com</c>.
    ///   * <c>url</c> the full URL of the page the widget is on. MTCaptcha issues
    ///     tokens only for the domains the sitekey is registered for.
    ///
    /// If <c>mtcaptchaConfig</c> also sets <c>action</c>, send that value too.
    /// </summary>
    public static class MtCaptchaExample
    {
        // 2Captcha's public MTCaptcha demo page and its sitekey.
        private const string Sitekey = "MTPublic-KzqLY1cKH";
        private const string PageUrl = "https://2captcha.com/demo/mtcaptcha";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.MtCaptchaAsync(Sitekey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {Truncate(result.Token, 60)}...");

            // --- Optional parameters -----------------------------------------
            //
            //   var result = await solver.MtCaptchaAsync(Sitekey, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["action"] = "login",                 // the action mtcaptchaConfig sets
            //       ["userAgent"] = "Mozilla/5.0 ...",    // the User-Agent to present to MTCaptcha
            //       ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
            //   });
            //
            // --- Submitting the token ----------------------------------------
            //
            // Put the token in the form's mtcaptcha-verifiedtoken field and submit
            // the form as the page would. With Selenium:
            //
            //   ((IJavaScriptExecutor)driver).ExecuteScript(
            //       "document.querySelector('[name=\"mtcaptcha-verifiedtoken\"]').value = arguments[0];",
            //       result.Token);
            Console.WriteLine("Form field: mtcaptcha-verifiedtoken");

            // Code holds the same string as Token, which is what you forward if you
            // are porting code written against another solver's API.
            if (result.Code != result.Token)
            {
                throw new InvalidOperationException("Code and Token should be the same string.");
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
