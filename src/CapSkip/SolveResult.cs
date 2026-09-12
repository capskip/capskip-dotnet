namespace CapSkip
{
    /// <summary>
    /// The result every solve method returns. Mirrors the dictionary the other
    /// CapSkip SDKs return (<c>captchaId</c>, <c>code</c>, for Turnstile
    /// <c>userAgent</c>, for GeeTest <c>challenge</c>/<c>validate</c>/<c>seccode</c>, and for
    /// ALTCHA <c>token</c>/<c>number</c>).
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
        /// Turnstile only — the User-Agent to send when submitting the token.
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
        /// ALTCHA only — the base64 payload to post back in the site's <c>altcha</c>
        /// form field. The same string as <see cref="Code"/>, named for where it
        /// goes. <see langword="null"/> for other captcha types.
        /// </summary>
        public string? Token { get; set; }

        /// <summary>
        /// ALTCHA only — the counter that solved the challenge.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public long? Number { get; set; }

        /// <summary>A compact, human-readable representation for logging.</summary>
        public override string ToString()
        {
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

            return UserAgent is null
                ? $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code} }}"
                : $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code}, UserAgent = {UserAgent} }}";
        }
    }
}
