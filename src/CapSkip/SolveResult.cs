namespace CapSkip
{
    /// <summary>
    /// The result every solve method returns. Mirrors the dictionary the other
    /// CapSkip SDKs return (<c>captchaId</c>, <c>code</c>, and — for Turnstile —
    /// <c>userAgent</c>).
    /// </summary>
    public sealed class SolveResult
    {
        /// <summary>CapSkip's internal id for this solve.</summary>
        public string CaptchaId { get; set; } = "";

        /// <summary>
        /// The solution: recognized text for image captchas, a token for
        /// reCAPTCHA / Turnstile.
        /// </summary>
        public string Code { get; set; } = "";

        /// <summary>
        /// Turnstile only — the User-Agent to send when submitting the token.
        /// <see langword="null"/> for other captcha types.
        /// </summary>
        public string? UserAgent { get; set; }

        /// <summary>A compact, human-readable representation for logging.</summary>
        public override string ToString()
        {
            return UserAgent is null
                ? $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code} }}"
                : $"SolveResult {{ CaptchaId = {CaptchaId}, Code = {Code}, UserAgent = {UserAgent} }}";
        }
    }
}
