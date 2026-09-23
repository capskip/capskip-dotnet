# Changelog

All notable changes to this project are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - 2026-09-22

### Added

- **Capy Puzzle support** via `CapyAsync(sitekey, url, options)`. The site key
  (conventionally prefixed `PUZZLE_`) is sent as the `captchakey` the API
  documents. Optional `api_server` selects the Capy API the key lives behind,
  defaulting to `https://jp.api.capy.me`.
  A Capy answer is **not a token**: the result carries `CaptchaKey`,
  `ChallengeKey` and `Answer`, which go into the target form's
  `capy_captchakey`, `capy_challengekey` and `capy_answer` fields. `Code` keeps
  the raw JSON answer.
- **CaptchaFox support** via `CaptchaFoxAsync(sitekey, url, options)`. The result
  exposes `Token` (for the form's `cf-captcha-response` field) and, when the
  solve reported one, `UserAgent` — the UA the browser actually minted the token
  under, which is not the one you sent. Optional `api_server` selects the widget
  source; the MAM package returns a `MAM_` prefixed token.
- **Friendly Captcha support** via `FriendlyCaptchaAsync(sitekey, url, options)`.
  Pass `version` (`v1`/`v2`, or a bare `1`/`2`), or `module_script` /
  `nomodule_script` with the widget script URLs and let CapSkip read the version
  off the build the site actually loads. Optional `api_server` accepts `global`
  (the default), `eu`, or a full URL for the data-residency tenant. The result
  exposes `Token`, for `frc-captcha-solution` on v1 or `frc-captcha-response` on
  v2.
- `SolveResult.CaptchaKey`, `.ChallengeKey`, `.Answer` and `.RespKey` for Capy.
- Parameter aliases `userAgent`/`user_agent` for `useragent`, `captchaKey` for
  `captchakey`, and `moduleScript`/`nomoduleScript` for `module_script` /
  `nomodule_script`.

### Changed

- A polled `request` that is an object rather than a string — which only Capy
  sends — is now re-serialized into `SolveResult.Code` as JSON, instead of being
  rendered as its type name. `Code` therefore carries the same text a plain-text
  poll would have delivered after `OK|`.
- When `request` comes through empty, the token is read from the
  createTask-shaped `solution` object, so a client is never left without the
  token the poll actually carried.

### Notes

- `["version"] = "avatar"` is refused locally for Capy. CapSkip solves the puzzle
  family only, and answering an avatar request with a puzzle answer would bill
  for a solve the target site rejects, indistinguishably from a broken solver.
- An unknown Friendly Captcha `version` is refused locally too. The two versions
  are different protocols sharing one sitekey namespace, so solving the wrong one
  returns a well-formed token the site rejects with nothing to indicate why.
- CaptchaFox and Friendly Captcha use `recaptchaTimeout`: the first is a real
  browser solve, and the second has its difficulty set per request by the service
  (and always solves in a browser on v2). Capy uses `defaultTimeout` — it is one
  HTTP fetch plus pixel math, held back to roughly two seconds because Capy
  refuses answers that arrive faster than a human could have produced them.

## [1.2.0] - 2026-09-12

### Added

- **ALTCHA support** via `AltchaAsync(url, options)`. Pass `challenge_url` for
  CapSkip to fetch the challenge, or `challenge_json` with the document itself (a
  JSON string, or a dictionary which is serialized for you). Sending both is
  allowed — the inline document wins. `SolveResult` gains `Token` (the value the
  site's `altcha` form field expects) and `Number`, the counter that solved it;
  `Code` keeps the same raw string.
- Parameter aliases `challengeUrl`/`challengeURL` for `challenge_url` and
  `challengeJson`/`challengeJSON` for `challenge_json`.
- Both ALTCHA generations are handled: the legacy scheme (SHA-1/256/384/512) and
  proof-of-work v2 (PBKDF2 or SHA). Their tokens are shaped differently — a v2
  payload carries no top-level `number`, its counter sitting at
  `solution.counter` — so the counter is taken from the server's own `solution`
  object, the one field both report the same way, and dug out of the token only
  when a poll did not carry it.

### Notes

- ALTCHA is CPU proof-of-work rather than a browser solve, so it uses
  `defaultTimeout` instead of the longer `recaptchaTimeout` that reCAPTCHA,
  Turnstile and GeeTest use.
- A proxy passed to `AltchaAsync` applies only to the `challenge_url` fetch; a
  task carrying its challenge inline never touches the network.

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
