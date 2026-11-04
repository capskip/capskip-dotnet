using System;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve an hCaptcha widget.
    ///
    /// hCaptcha is the checkbox widget behind many sign-up, login and checkout
    /// forms. CapSkip passes the widget in a real browser for the sitekey and page
    /// you send and returns the token the widget would have put in the form.
    /// Invisible widgets and hCaptcha Enterprise use the same method.
    ///
    /// You need two values, both read off the target page:
    ///
    ///   * <c>sitekey</c> the public identifier of the widget, the same for every
    ///     visitor. Most pages carry it on the container as <c>data-sitekey</c>:
    ///     <c>&lt;div class="h-captcha" data-sitekey="..."&gt;</c>. When a page
    ///     builds the widget with a script, look for the <c>sitekey</c> option of
    ///     <c>hcaptcha.render()</c>, or the <c>sitekey=</c> parameter in the address
    ///     of the hCaptcha iframe.
    ///   * <c>url</c> the full URL of the page the widget is on.
    ///
    /// A widget with <c>data-size="invisible"</c> is invisible: pass
    /// <c>invisible: true</c>. Some hCaptcha Enterprise sites pass an <c>rqdata</c>
    /// value to the widget through <c>hcaptcha.setData()</c> before it runs; send it
    /// as <c>data</c>. It is issued for one challenge and expires quickly, so read a
    /// fresh one for every task.
    /// </summary>
    public static class HCaptchaExample
    {
        // hCaptcha's own public demo page and its sitekey.
        private const string Sitekey = "a5f74b19-9e45-40e0-b45d-47ff91b7a6c2";
        private const string PageUrl = "https://accounts.hcaptcha.com/demo";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.HCaptchaAsync(Sitekey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {Truncate(result.Token, 60)}...");
            Console.WriteLine(
                $"respKey:    {(string.IsNullOrEmpty(result.RespKey) ? "(empty)" : Truncate(result.RespKey, 40) + "...")}");
            Console.WriteLine($"User-Agent: {result.UserAgent ?? "(none reported)"}");

            // --- Invisible and Enterprise widgets ----------------------------
            //
            //   await solver.HCaptchaAsync(Sitekey, PageUrl, invisible: true, new Dictionary<string, object?>
            //   {
            //       ["data"] = rqdata,   // the rqdata the page passed to hcaptcha.setData()
            //   });
            //
            // or, all in one dictionary:
            //
            //   await solver.HCaptchaAsync(Sitekey, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["invisible"] = true,   // data-size="invisible"
            //       ["data"] = rqdata,
            //   });
            //
            // --- With a per-request proxy ------------------------------------
            // CapSkip does not force a proxy. At volume, though, hCaptcha scores the
            // network a token comes from as well as the browser, so send a proxy with
            // each task (or configure a pool in the app: Settings, Captcha types,
            // hCaptcha, Configure Proxies). A proxy sent with a task always takes
            // priority over the pool.
            //
            //   ["proxy"] = new Proxy("HTTP", "login:password@1.2.3.4:8080"),

            // --- Submitting the token ----------------------------------------
            // The widget writes its token into a hidden textarea named
            // h-captcha-response (and a copy named g-recaptcha-response). Put the
            // token in both and submit the form, or call the widget's data-callback
            // with it. Without a browser, post it as those form fields with the
            // User-Agent from the result:
            //
            //   using var http = new HttpClient();
            //   var request = new HttpRequestMessage(HttpMethod.Post, formUrl)
            //   {
            //       Content = new FormUrlEncodedContent(new Dictionary<string, string>
            //       {
            //           ["email"] = "someone@example.com",
            //           ["h-captcha-response"] = result.Token!,
            //           ["g-recaptcha-response"] = result.Token!,
            //       }),
            //   };
            //   request.Headers.UserAgent.ParseAdd(result.UserAgent);
            //   await http.SendAsync(request);
            //
            // Tokens are single use and expire about two minutes after they are
            // issued, so submit each one soon and solve again for the next form
            // submission.
            Console.WriteLine("Form fields: h-captcha-response, g-recaptcha-response");

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
