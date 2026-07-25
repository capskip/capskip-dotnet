# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.1.0] - 2026-07-26

### Added

- **GeeTest v3 (slide) support** via `GeetestAsync(gt, challenge, url, options)`.
  Accepts the optional `api_server` domain override and the usual `proxy`
  option. `SolveResult` gained `Challenge`, `Validate`, and `Seccode` properties
  carrying the parsed answer, while `Code` keeps the raw JSON string CapSkip
  returns.
- Parameter aliases `apiServer` and `api_subdomain` for `api_server`.
- `proxytype` is now validated against the values CapSkip accepts (`HTTP`,
  `HTTPS`, `SOCKS5`, `SOCKS5H`, case-insensitive) for every proxy-capable captcha
  type. `SOCKS4` and other values previously reached the server and came back as
  `ERROR_BAD_PARAMETERS`; they now raise `ValidationException` locally.

## [1.0.2] - 2026-07-15

### Added

- Initial release of the CapSkip .NET SDK
- `CapSkipClient` client for the local CapSkip API — every method returns a `Task`
- `AsyncCapSkip` / `AsyncApiClient` provided as aliases for cross-SDK parity
- Image CAPTCHA solving via `NormalAsync()` (file, URL, base64, or data-URI)
- reCAPTCHA v2 / v3 solving via `RecaptchaAsync()` (invisible, enterprise, proxy)
- Cloudflare Turnstile solving via `TurnstileAsync()` (widget and challenge page)
- Turnstile automatically polls with `json=1` and returns `UserAgent` when provided
- Manual workflow: `SendAsync()`, `GetResultAsync()`, `SolveAsync()`
- Adaptive result polling — starts at 0.25s and backs off (doubling) up to the
  configured `pollingInterval`, so fast solves (e.g. image captchas) return in a
  fraction of a second; `pollingInterval` also accepts sub-second values
- Familiar parameter aliases (`url`→`pageurl`, `score`→`min_score`, etc.)
- Proxy support via the `Proxy` type or a `{ ["type"] = ..., ["uri"] = ... }` dictionary
- Strict per-captcha parameter validation — only documented parameters are accepted
- Both submit response forms parsed — `OK|<id>` and the `json=1` shape
  `{"status":1,"request":"<id>"}`
- Empty `res.php` bodies treated as "not ready" so polling never fails spuriously
- Exception hierarchy: `ValidationException`, `NetworkException`, `ApiException`, `TimeoutException`
- Targets .NET Standard 2.0 (runs on .NET Framework 4.6.1+, .NET Core 2.0+, .NET 5+)
- No third-party runtime dependencies beyond `System.Text.Json`
- Runnable example programs for every supported captcha type
- Unit and integration tests (xUnit) with a mocked API client and a local mock server

[1.0.2]: https://github.com/capskip/capskip-dotnet/releases/tag/v1.0.2
