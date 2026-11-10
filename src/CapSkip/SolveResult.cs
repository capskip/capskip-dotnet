namespace CapSkip
{
    /// <summary>
    /// The result every solve method returns. Mirrors the dictionary the other
    /// CapSkip SDKs return (<c>captchaId</c>, <c>code</c>, for Turnstile
    /// <c>userAgent</c>, for GeeTest <c>challenge</c>/<c>validate</c>/<c>seccode</c>, and for
    /// ALTCHA <c>token</c>/<c>number</c>, for Capy
    /// <c>captchakey</c>/<c>challengekey</c>/<c>answer</c>, for AWS WAF
    /// <c>captcha_voucher</c>/<c>existing_token</c>, for DataDome
    /// <c>cookie</c>/<c>userAgent</c>/<c>proxy</c>, for hCaptcha
    /// <c>token</c>/<c>respKey</c>/<c>userAgent</c>, and for MTCaptcha <c>token</c>).
    /// </summary>
    public sealed class SolveResult
    {
        /// <summary>CapSkip's internal id for this solve.</summary>
        public string CaptchaId { get; set; } = "";

        /// <summary>
        /// The solution: recognized text for image captchas, a token for
        /// reCAPTCHA / Turnstile. For GeeTest this is the raw JSON string CapSkip
        /// returns — prefer <see cref="Challenge"/>, <see cref="Validate"/>, and
        /// <see cref="Seccode"/>.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// Turnstile and CaptchaFox — the User-Agent to send when submitting the
        /// token. For CaptchaFox this is the browser's own, not one you sent, and it
        /// stays <see langword="null"/> when the solve captured none rather than
        /// being guessed. For DataDome, the User-Agent the cookie was earned with
        /// (yours, or CapSkip's browser's when you sent none): send it with every
        /// request that carries the cookie. For hCaptcha, the User-Agent the token
        /// was earned with, when the reply carried one: submit the form with it.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>
        /// GeeTest only — the <c>geetest_challenge</c> value to post back.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Challenge { get; set; }

        /// <summary>
        /// GeeTest only — the <c>geetest_validate</c> value to post back.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Validate { get; set; }

        /// <summary>
        /// GeeTest only — the <c>geetest_seccode</c> value to post back.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Seccode { get; set; }

        /// <summary>
        /// The token, named for the form field it fills: the same string as
        /// <see cref="Code"/>. For ALTCHA the base64 payload for the site's
        /// <c>altcha</c> field; for hCaptcha the value for
        /// <c>h-captcha-response</c> (and <c>g-recaptcha-response</c>); for MTCaptcha
        /// the <c>v1(...)</c> value for <c>mtcaptcha-verifiedtoken</c>; likewise for
        /// CaptchaFox and Friendly Captcha. <see langword="null"/> for other captcha
        /// types.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// ALTCHA only — the counter that solved the challenge.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public long? Number { get; set; }

        /// <summary>
        /// Capy only — the value for the target form's <c>capy_captchakey</c> field.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? CaptchaKey { get; set; }

        /// <summary>
        /// Capy only — the value for the target form's <c>capy_challengekey</c>
        /// field. <see langword="null"/> for other captcha types.
        /// </summary>
        public string? ChallengeKey { get; set; }

        /// <summary>
        /// Capy only — the value for the target form's <c>capy_answer</c> field. The
        /// drag path the widget would have recorded; submit it verbatim, without
        /// trimming or re-encoding it. <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Answer { get; set; }

        /// <summary>
        /// hCaptcha — the second value hCaptcha hands the widget next to the token
        /// (<c>hcaptcha.getRespKey()</c>). Never <see langword="null"/> for an
        /// hCaptcha result: empty when the widget handed out none. Most sites never
        /// read it. Capy — empty for a puzzle solve, present for shape compatibility
        /// with 2Captcha's documented response. Any other polled result carries it
        /// when the server sent one; <see langword="null"/> otherwise.
        /// </summary>
        public string? RespKey { get; set; }

        /// <summary>
        /// AWS WAF only — the <c>captcha_voucher</c> AWS issued for the answered
        /// puzzle. Works only together with <see cref="ExistingToken"/>: POST both,
        /// as JSON under their wire names, to the <c>voucher</c> endpoint beside the
        /// page's challenge.js. <see langword="null"/> for other captcha types.
        /// </summary>
        public string? CaptchaVoucher { get; set; }

        /// <summary>
        /// AWS WAF only — the <c>existing_token</c> the voucher was issued for.
        /// Works only together with <see cref="CaptchaVoucher"/>.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? ExistingToken { get; set; }

        /// <summary>
        /// DataDome only — the <c>datadome=...</c> pair alone, the part of
        /// <see cref="Code"/> before the first semicolon, ready for a <c>Cookie</c>
        /// request header. <see cref="Code"/> keeps the cookie exactly as DataDome
        /// set it, attributes included, for a cookie jar. Send it with
        /// <see cref="UserAgent"/>, from the same IP address that earned it.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Cookie { get; set; }

        /// <summary>
        /// DataDome — the proxy the task was solved through, as <c>host:port</c>,
        /// present only when the task sent no proxy and the CapSkip app solved
        /// through its own DataDome proxy list. The cookie works only through that
        /// proxy. <see langword="null"/> otherwise, and for other captcha types.
        /// </summary>
        public string? Proxy { get; set; }

        /// <summary>A compact, human-readable representation for logging.</summary>
        public override string ToString()
        {
            if (Cookie != null)
            {
                return Proxy is null
                    ? $"SolveResult {{ CaptchaId = {CaptchaId}, Cookie = {Cookie}, UserAgent = {UserAgent} }}"
                    : $"SolveResult {{ CaptchaId = {CaptchaId}, Cookie = {Cookie}, UserAgent = {UserAgent}, "
                        + $"Proxy = {Proxy} }}";
            }

            if (CaptchaVoucher != null)
            {
                return $"SolveResult {{ CaptchaId = {CaptchaId}, CaptchaVoucher = {CaptchaVoucher}, "
                    + $"ExistingToken = {ExistingToken} }}";
            }

            if (Answer != null)
            {
                return $"SolveResult {{ CaptchaId = {CaptchaId}, CaptchaKey = {CaptchaKey}, "
                    + $"ChallengeKey = {ChallengeKey}, Answer = {Answer} }}";
            }

            if (Number != null)
            {
                return $"SolveResult {{ CaptchaId = {CaptchaId}, Number = {Number}, "
                    + $"Token = {Token} }}";
            }

            if (Validate != null)
            {
                return $"SolveResult {{ CaptchaId = {CaptchaId}, Challenge = {Challenge}, "
                    + $"Validate = {Validate}, Seccode = {Seccode} }}";
            }

            if (RespKey != null && Token != null)
            {
                return UserAgent is null
                    ? $"SolveResult {{ CaptchaId = {CaptchaId}, Token = {Token}, RespKey = {RespKey} }}"
                    : $"SolveResult {{ CaptchaId = {CaptchaId}, Token = {Token}, RespKey = {RespKey}, "
                        + $"UserAgent = {UserAgent} }}";
            }

            return UserAgent is null
                ? $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code} }}"
                : $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code}, UserAgent = {UserAgent} }}";
        }
    }
}
