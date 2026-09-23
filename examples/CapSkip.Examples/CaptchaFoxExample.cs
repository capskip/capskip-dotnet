using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    /// <summary>
    /// Solve a CaptchaFox challenge.
    ///
    /// CaptchaFox scores the browser itself rather than asking the visitor to read
    /// anything, so most solves draw no puzzle at all. CapSkip drives the real widget
    /// in a real browser and returns the verification token it would have produced.
    ///
    /// You need two values, both read off the target page:
    ///
    ///   * <c>sitekey</c> the public key the widget renders with, conventionally
    ///     prefixed <c>sk_</c>. It is on the widget container as
    ///     <c>data-sitekey</c>, or in the <c>captchafox.render(...)</c> call. If the
    ///     page builds the widget at runtime, open DevTools → Network and find the
    ///     request to <c>api.captchafox.com</c>: the key is the path segment after
    ///     <c>/captcha/</c>.
    ///   * <c>pageurl</c> the full URL of the page the widget appears on
    ///
    /// The page URL has to match: CaptchaFox keys are registered against a list of
    /// allowed domains and the service checks the host before it issues anything. A
    /// key that is correct but used on a page outside that list is refused
    /// permanently, not intermittently — so if a sitekey fails immediately and
    /// consistently, check that you are sending the page the widget actually runs on
    /// rather than a search page, a redirect or a shortened link.
    /// </summary>
    public static class CaptchaFoxExample
    {
        private const string Sitekey = "sk_xtNxpk6fCdFbxh1_xJeGflSdCE9tn99G";
        private const string PageUrl = "https://example.com/signup";

        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            var result = await solver.CaptchaFoxAsync(Sitekey, PageUrl);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Token:      {Truncate(result.Token, 60)}...");
            Console.WriteLine($"User-Agent: {result.UserAgent ?? "(none reported)"}");

            // --- Choosing the widget source ----------------------------------
            // CaptchaFox publishes its widget from two places, and which one a site
            // loads decides the shape of the token it expects back. Send api_server
            // only when the target page does not use the default
            // (https://cdn.captchafox.com/).
            //
            //   await solver.CaptchaFoxAsync(Sitekey, PageUrl, new Dictionary<string, object?>
            //   {
            //       ["api_server"] = "https://s.uicdn.com/mampkg/@mamdev/core.frontend.libs.captchafox/",
            //   });
            //
            // That one returns a MAM_ prefixed token. Read the value from the
            // <script> tag on the target page: if you send the wrong one the solve
            // still succeeds, but the token comes back in a format the site will not
            // accept, which looks like a silent verification failure rather than an
            // error.
            //
            // --- With a per-request proxy ------------------------------------
            // CapSkip does not force a proxy, but CaptchaFox scores the network a
            // widget runs on as well as the browser, so repeated solves from one
            // address push that address toward the interactive challenges and then
            // toward refusals.
            //
            //   ["proxy"] = new Proxy("HTTP", "login:password@1.2.3.4:8080"),

            // Post the token back in the form field the widget uses, named
            // `cf-captcha-response`. Treat it as opaque and pass it through
            // unchanged — it is verified server side against the session that
            // produced it, so any edit invalidates it.
            //
            //   new FormUrlEncodedContent(new Dictionary<string, string>
            //   {
            //       ["email"] = "someone@example.com",
            //       ["cf-captcha-response"] = result.Token!,
            //   });
            //
            // Some integrations read the token from a JSON body field instead, so
            // check what the page's own submit sends and mirror it.
            Console.WriteLine("Form field: cf-captcha-response");

            // Submit under the User-Agent the token was minted with, not your own.
            // CapSkip solves in a real browser and uses that browser's identity, so
            // the UA it reports is the one the token is bound to.
            //
            //   request.Headers.UserAgent.ParseAdd(result.UserAgent);
            //
            // Tokens are short lived: submit promptly rather than holding one while a
            // user fills in a form, and solve again if the form is abandoned and
            // resumed.

            // Two of CaptchaFox's challenge types are not solvable — image-select and
            // the audio fallback, both rare. They are reported as unsolvable rather
            // than waited out, so treat a failure as a signal to resubmit into a fresh
            // challenge rather than as a permanent failure of the key.
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
