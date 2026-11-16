using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve a Yandex SmartCaptcha.
    ///
    /// Yandex SmartCaptcha is the CAPTCHA service of Yandex Cloud, used on sign-up,
    /// login, contact and checkout forms. The visitor ticks "I'm not a robot" or
    /// pulls a slider, and Yandex may then ask for a challenge. When the visitor
    /// passes, the widget writes a token into a hidden form field named
    /// <c>smart-token</c>. CapSkip takes the sitekey and page address and returns
    /// that token, whatever challenges Yandex asks for on the way.
    ///
    /// You need two values, both read off the target page:
    ///
    ///   * <c>sitekey</c> the <c>data-sitekey</c> of the widget's container:
    ///     <c>&lt;div class="smart-captcha" data-sitekey="ysc1_HrTv0IuXWNH5JxfcWnRozoir2pTFbxsaZbrbGI4e292406f2"&gt;&lt;/div&gt;</c>
    ///     Pages that render the widget from their own script pass the same key to
    ///     <c>window.smartCaptcha.render</c> as <c>sitekey</c>. Client keys usually
    ///     start with <c>ysc1_</c>.
    ///   * <c>url</c> the full URL of the page the widget is on. Yandex checks the
    ///     sitekey against that domain.
    /// </summary>
    public static class YandexSmartExample
    {
        // Yandex's public SmartCaptcha demo page and its sitekey.
        private const string Sitekey = "FEXfAbHQsToo97VidNVk3j4dC74nGW1DgdxjtNB9";
        private const string PageUrl = "https://smartcaptcha.yandexcloud.net/demo";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.YandexSmartAsync(Sitekey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {Truncate(result.Token, 60)}...");

            // --- Optional parameters -----------------------------------------
            //
            //   var result = await solver.YandexSmartAsync(Sitekey, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["userAgent"] = "Mozilla/5.0 ...",    // the User-Agent to present to Yandex
            //       ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
            //   });
            //
            // --- Submitting the token ----------------------------------------
            //
            // Put the token in the form's smart-token field and submit the form as
            // the page would. A token is valid for five minutes and can be checked
            // only once. With Selenium:
            //
            //   ((IJavaScriptExecutor)driver).ExecuteScript(
            //       "document.querySelector('input[name=\"smart-token\"]').value = arguments[0];",
            //       result.Token);
            Console.WriteLine("Form field: smart-token");

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
