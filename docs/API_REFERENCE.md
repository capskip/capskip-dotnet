# CapSkip API Reference

Complete reference aligned with the [official CapSkip API](https://capskip.com/api-docs/).

CapSkip exposes a standard captcha-solver HTTP API on your local machine:

```
POST http://<host>:<port>/in.php   → submit captcha
GET  http://<host>:<port>/res.php  → poll result
```

The SDK only supports the captcha types documented by CapSkip.

---

## Supported captcha types

| Type | SDK method | `method` (POST) |
|---|---|---|
| Image captcha | `NormalAsync()` | `post` or `base64` |
| reCAPTCHA v2 | `RecaptchaAsync(..., version="v2")` | `userrecaptcha` |
| reCAPTCHA v3 | `RecaptchaAsync(..., version="v3")` | `userrecaptcha` + `version=v3` |
| Cloudflare Turnstile | `TurnstileAsync()` | `turnstile` |
| GeeTest v3 (slide) | `GeetestAsync()` | `geetest` |
| ALTCHA (proof-of-work) | `AltchaAsync()` | `altcha` |
| Capy Puzzle (slide) | `CapyAsync()` | `capy` |
| CaptchaFox (widget) | `CaptchaFoxAsync()` | `captchafox` |
| Friendly Captcha (proof-of-work) | `FriendlyCaptchaAsync()` | `friendly_captcha` |
| AWS WAF (challenge page or widget) | `AmazonWafAsync()` | `amazon_waf` |
| DataDome (slider and device check) | `DataDomeAsync()` | `datadome` |
| hCaptcha (checkbox, invisible, Enterprise) | `HCaptchaAsync()` | `hcaptcha` |

**Proxy** is supported for every method except image
captcha. For ALTCHA the proxy is used only for the `challenge_url` fetch.

---

## CapSkipClient

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: "capskip",
    host: "127.0.0.1",
    port: 8080,
    defaultTimeout: 120,
    recaptchaTimeout: 300,
    pollingInterval: 5);       // max seconds between polls; starts at 0.25s and backs off to this
```

| Constructor parameter | Type | Default | Description |
|---|---|---|---|
| `apiKey` | `string` | `"capskip"` | CapSkip API key (any string when validation is off) |
| `host` | `string` | `"127.0.0.1"` | CapSkip host |
| `port` | `int` | `8080` | CapSkip port |
| `defaultTimeout` | `double` | `120` | Seconds to poll an image captcha |
| `recaptchaTimeout` | `double` | `300` | Seconds to poll the browser-backed solves (reCAPTCHA, Turnstile, GeeTest, CaptchaFox, Friendly Captcha, AWS WAF, DataDome, hCaptcha) |
| `pollingInterval` | `double` | `5` | Max seconds between polls (starts at 0.25s, backs off to this) |

`AsyncCapSkip` is a subclass alias of `CapSkipClient`, provided for parity with the
other CapSkip SDKs.

---

## 1. Image captcha — `NormalAsync(file, options?)`

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `post` (multipart file) or `base64` |
| `file` | file | Yes* | Image file when `method=post` |
| `body` | string | Yes* | Base64 image when `method=base64` |
| `json` | int | No | `0` plain text (default), `1` JSON |

### GET `/res.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `action` | string | Yes | `get` |
| `id` | int | Yes | Captcha ID from `in.php` |
| `json` | int | No | `0` plain text (default), `1` JSON |

### SDK usage

```csharp
await solver.NormalAsync("captcha.png");
await solver.NormalAsync("https://example.com/captcha.jpg");
await solver.NormalAsync("data:image/png;base64,iVBORw0KGgo...", new Dictionary<string, object?> { ["json"] = 1 });
```

Only `json` is accepted as an extra option. Proxy is **not** supported.

---

## 2. reCAPTCHA v2 — `RecaptchaAsync(sitekey, url, options?)`

### POST `/in.php`

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `key` | string | Yes | — | CapSkip API key |
| `method` | string | Yes | — | `userrecaptcha` |
| `googlekey` | string | Yes | — | Site key (`data-sitekey` / `k`) |
| `pageurl` | string | Yes | — | Full page URL |
| `enterprise` | int | No | `0` | `1` = Enterprise v2 |
| `invisible` | int | No | `0` | `1` = Invisible reCAPTCHA |
| `data-s` | string | No | — | Google Search / services `data-s` value |
| `json` | int | No | `0` | `1` = JSON response |
| `proxy` | string | No | — | `IP:PORT` or `login:pass@IP:PORT` |
| `proxytype` | string | No | `HTTP` | `HTTP`, `HTTPS`, `SOCKS5`, `SOCKS5H` |

Do **not** send `version`, `action`, or `score` for v2.

### SDK usage

```csharp
// Standard v2
await solver.RecaptchaAsync("...", "https://example.com");

// Invisible v2
await solver.RecaptchaAsync("...", "...", new Dictionary<string, object?> { ["invisible"] = 1 });

// Enterprise v2
await solver.RecaptchaAsync("...", "...", new Dictionary<string, object?> { ["enterprise"] = 1 });

// Enterprise v2 with data-s (SDK alias: datas)
await solver.RecaptchaAsync("...", "...", new Dictionary<string, object?> { ["enterprise"] = 1, ["datas"] = "..." });

// With proxy
await solver.RecaptchaAsync("...", "...", new Dictionary<string, object?>
{
    ["proxy"] = new Proxy("HTTPS", "user:pass@1.2.3.4:3128"),
});
```

---

## 3. reCAPTCHA v3 — `RecaptchaAsync(..., version="v3", ...)`

### POST `/in.php`

| Parameter | Type | Required | Default | Description |
|---|---|---|---|---|
| `key` | string | Yes | — | CapSkip API key |
| `method` | string | Yes | — | `userrecaptcha` |
| `version` | string | Yes | — | `v3` |
| `googlekey` | string | Yes | — | Site key |
| `pageurl` | string | Yes | — | Full page URL |
| `enterprise` | int | No | `0` | `1` = Enterprise v3 |
| `action` | string | No | `verify` | Action from `grecaptcha.execute()` |
| `min_score` | double | No | `0.4` | Minimum acceptable score |
| `json` | int | No | `0` | `1` = JSON response |
| `proxy` | string | No | — | Proxy address |
| `proxytype` | string | No | `HTTP` | Proxy type |

Do **not** send `invisible` for v3.

### SDK usage

```csharp
await solver.RecaptchaAsync("...", "https://example.com", new Dictionary<string, object?>
{
    ["version"] = "v3",
    ["action"] = "submit",
    ["score"] = 0.7,          // or the API name: ["min_score"] = 0.7
    ["enterprise"] = 0,
});
```

---

## 4. Cloudflare Turnstile — `TurnstileAsync(sitekey, url, options?)`

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `turnstile` |
| `sitekey` | string | Yes | Turnstile sitekey |
| `pageurl` | string | Yes | Full page URL |
| `action` | string | No | From `data-action` or `turnstile.render()` |
| `data` | string | No | `cData` / `data-cdata` |
| `pagedata` | string | No | `chlPageData` (challenge pages) |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

### GET `/res.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `action` | string | Yes | `get` |
| `id` | int | Yes | Captcha ID |
| `json` | int | **Yes** | Must be `1` to receive User-Agent |

The SDK **automatically** polls Turnstile results with `json=1` and includes
`UserAgent` in the result when CapSkip returns it.

### SDK usage

```csharp
// Standalone widget
var result = await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com");
Console.WriteLine(result.Code);
Console.WriteLine(result.UserAgent);  // present when CapSkip returns it

// Challenge page
var challenge = await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com", new Dictionary<string, object?>
{
    ["action"] = "managed",
    ["data"] = "cData_value",
    ["pagedata"] = "chlPageData_value",
});
// Use challenge.UserAgent when submitting the token
```

---

## 5. GeeTest v3 — `GeetestAsync(gt, challenge, url, options)`

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `geetest` |
| `gt` | string | Yes | Static per-site GeeTest id |
| `challenge` | string | Yes | Single-use challenge token |
| `pageurl` | string | Yes | Full page URL |
| `api_server` | string | No | GeeTest API server domain, e.g. `api-na.geetest.com` |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

### Getting `gt` and `challenge`

Both come from the target site, which fetches them from an endpoint returning
`{"gt": "...", "challenge": "..."}` (often `.../register.php` or a `gettype`/`get.php`
request). Find it in DevTools → Network, or read them out of the
`initGeetest({ gt, challenge })` call in the page scripts.

> **`challenge` is single-use and expires in about a minute.** Fetch a fresh pair
> immediately before each solve. If a solve comes back with a bad-challenge error,
> request a new pair and retry — reusing one never succeeds.

### SDK usage

```csharp
var result = await solver.GeetestAsync(
    "81388ea1fc187e0c335c0a8907ff2625",
    "7cf6a8b1a2c34d5e6f7089abcdef0123",
    "https://example.com/login");

result.Challenge;   // geetest_challenge
result.Validate;    // geetest_validate
result.Seccode;     // geetest_seccode
result.Code;        // the same answer as a raw JSON string
```

Post the three fields back exactly as the site's own front-end would:

```csharp
var body = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["geetest_challenge"] = result.Challenge!,
    ["geetest_validate"] = result.Validate!,
    ["geetest_seccode"] = result.Seccode!,
});
```

Pass `api_server` when the site uses a non-default GeeTest API server domain:

```csharp
await solver.GeetestAsync(gt, challenge, url, new Dictionary<string, object?> { ["api_server"] = "api-na.geetest.com" });
```

GeeTest is a real browser solve, so it uses the longer `recaptchaTimeout` budget
rather than `defaultTimeout`.

---

## 6. ALTCHA — `AltchaAsync(url, options)`

ALTCHA is not a recognition captcha. There is no image, audio or text to read:
the site issues a proof-of-work challenge and the client must brute-force a
number that satisfies it. A solve is therefore deterministic and cheap —
typically milliseconds.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `altcha` |
| `pageurl` | string | Yes | Full URL of the page the challenge came from |
| `challenge_url` | string | One of the two | Endpoint CapSkip fetches the challenge from |
| `challenge_json` | string | One of the two | The challenge document itself, as a JSON string |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address — used **only** for the `challenge_url` fetch |
| `proxytype` | string | No | Proxy type |

Sending both challenge parameters is allowed: the inline `challenge_json` wins,
because fetching would only re-obtain what you already supplied.

### Getting the challenge

Open DevTools → Network on the target page and look for the request the
`<altcha-widget>` makes for its challenge (often something like
`/altcha/challenge`). The request URL is your `challenge_url`; its JSON response
is your `challenge_json`.

The widget attribute that names that endpoint depends on the widget version, so
read the page source rather than assuming:

| Widget | Attribute |
|---|---|
| v1 / v2 | `challengeurl="…"`, with a separate `challengejson="…"` for an inline challenge |
| v3+ | `challenge="…"` — the same attribute takes either a URL or the challenge data |

> **Challenges expire, and the window is short** — some sites inside two minutes.
> Once expired, the site refuses the solution with a bare "verification failed"
> that looks exactly like a wrong answer. Fetch the challenge immediately before
> solving and submit the token promptly; do not fetch a batch in advance, and do
> not hold a token while a user fills in a form.
>
> CapSkip refuses an already-expired inline challenge immediately rather than
> burning CPU on a token that cannot work. If you passed `challenge_url` and the
> challenge expired while the job queued, it fetches a fresh one automatically.

### SDK usage

```csharp
// CapSkip fetches the challenge for you
var result = await solver.AltchaAsync(
    "https://example.com/signup",
    new Dictionary<string, object?>
    {
        ["challenge_url"] = "https://example.com/captcha/api/altcha/challenge",
    });

// …or hand it the document you already have. No network request at all.
var inline = await solver.AltchaAsync(
    "https://example.com/signup",
    new Dictionary<string, object?>
    {
        ["challenge_json"] = new Dictionary<string, object>
        {
            ["algorithm"] = "SHA-256",
            ["challenge"] = "…",
            ["salt"] = "…",
            ["signature"] = "…",
            ["maxnumber"] = 1000000,
        },
    });

result.Token;    // the base64 payload to post back
result.Number;   // the counter that solved it
result.Code;     // the same string as Token
```

`challenge_json` accepts a dictionary (serialized for you) or a JSON string.

`Number` is reported for both ALTCHA generations. Their tokens differ — a legacy
payload carries it as a top-level `number`, while a proof-of-work v2 payload has
none, its counter sitting at `solution.counter` — so it is read from the server's
own `solution` object, which reports both the same way.

Post the token back in the form field the widget uses, named `altcha`:

```csharp
var body = new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["email"] = "someone@example.com",
    ["altcha"] = result.Token!,
});
```

Do not re-encode, trim or re-order the token: it is base64 of a JSON document
whose fields are covered by the server's HMAC signature, so any modification
invalidates it. Some integrations read the payload from a JSON body field
instead — check what the page's own submit sends and mirror it.

Unlike GeeTest and reCAPTCHA this is CPU proof-of-work rather than a browser
solve, so it uses `defaultTimeout`, not `recaptchaTimeout`.

### Algorithms

CapSkip supports the legacy scheme (SHA-1/256/384/512) and PoW v2 with PBKDF2 or
SHA. **Argon2id and scrypt are refused**, not attempted: a task using one returns
`ERROR_CAPTCHA_UNSOLVABLE` and is never retried. ALTCHA itself recommends PBKDF2
as the default, so this affects a minority of sites.

All three widget types (`native`, `checkbox`, `switch`) work — the distinction is
purely visual and never reaches CapSkip.

---

## 7. Capy Puzzle — `CapyAsync(sitekey, url, options?)`

Capy is a slide puzzle: a piece has to be dragged into the hole cut out of a
photograph. CapSkip locates the hole and produces the drag path — one HTTP fetch
plus pixel math, no browser.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `capy` |
| `captchakey` | string | Yes | The site's public Capy key, conventionally prefixed `PUZZLE_`. The SDK sends its `sitekey` argument here. |
| `pageurl` | string | Yes | Full URL of the page the captcha is on |
| `api_server` | string | No | Root of the Capy API the key lives behind. Default `https://jp.api.capy.me` |
| `version` | string | No | `puzzle` (the default). Only the puzzle family is solved |
| `useragent` | string | No | User-Agent to send with the puzzle request |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

`sitekey` and `websiteKey` are accepted by the server as aliases for
`captchakey`; the SDK sends the documented name.

`["version"] = "avatar"` is refused by the SDK before the request is made. Avatar
is a different challenge behind a different endpoint, and answering it with a
puzzle answer would bill for a solve the target site rejects, indistinguishably
from a broken solver.

> `api.capy.me` no longer resolves, although several solver services still
> document it. The live host is `jp.api.capy.me`; if a site's widget points
> somewhere else, pass that as `api_server`.

### Getting the captcha key

It is in the page source as `capy_captchakey`, or in the widget script URL:
`<script src="https://jp.api.capy.me/puzzle/get_js/?k=PUZZLE_XXXX">`.

### GET `/res.php`

The answer is **not a token**. It is an object, which the SDK expands into
`CaptchaKey`, `ChallengeKey`, `Answer` and `RespKey`. See
[Return value](#return-value).

### SDK usage

```csharp
var result = await solver.CapyAsync(
    "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v",
    "https://example.com/login");

// With a non-default API server
var result = await solver.CapyAsync(
    "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v",
    "https://example.com/login",
    new Dictionary<string, object?> { ["api_server"] = "https://jp.api.capy.me/" });

// capy_captchakey, capy_challengekey, capy_answer
result.CaptchaKey; result.ChallengeKey; result.Answer;
```

### Timing

Capy grades the wall-clock gap between issuing the puzzle and verifying the
answer, and refuses anything superhuman with `CAPTCHA verification failed` — the
same message a wrong answer gets. CapSkip therefore holds every result until two
seconds have elapsed since it drew the puzzle. This is invisible if you poll: the
task simply takes about two seconds instead of a fifth of one. Nothing to
configure.

The challenge key is single-use and short-lived, so submit the three values
promptly rather than caching them.

---

## 8. CaptchaFox — `CaptchaFoxAsync(sitekey, url, options?)`

CaptchaFox scores the browser itself rather than asking the visitor to read
anything; most visitors never see a puzzle. CapSkip drives the real widget in a
real browser and returns the verification token.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `captchafox` |
| `sitekey` | string | Yes | The public key the widget renders with, conventionally prefixed `sk_` |
| `pageurl` | string | Yes | Full URL of the page the widget appears on |
| `api_server` | string | No | Widget entry point. Default `https://cdn.captchafox.com/` |
| `useragent` | string | No | Accepted for compatibility and not applied — CapSkip uses its own browser's identity |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

### Choosing the widget source

| `api_server` | Token | Default |
|---|---|---|
| `https://cdn.captchafox.com/` | Plain | Yes |
| `https://s.uicdn.com/mampkg/…` | `MAM_` prefixed | No |

Read the value from the `<script>` tag that loads the widget. If you send the
wrong one the solve still succeeds, but the token comes back in a format the
target site will not accept — a silent verification failure rather than an error.

### The page URL has to match the key

CaptchaFox keys are registered against a list of allowed domains and the service
checks the host before issuing anything. A correct key used on a page outside
that list is refused permanently, not intermittently. CapSkip reports that case
rather than retrying it, because retrying cannot help.

### Challenge types

| Challenge | Frequency | Supported |
|---|---|---|
| Invisible | Usually | Yes |
| Slide | Sometimes | Yes |
| Image select | Rarely | No |
| Audio | Rarely | No |

The two unsupported ones are uncommon and a retry usually draws a different
challenge, so treat an unsolvable result as a signal to resubmit rather than a
permanent failure of the key.

### SDK usage

```csharp
var result = await solver.CaptchaFoxAsync(
    "sk_xtNxpk6fCdFbxh1_xJeGflSdCE9tn99G",
    "https://example.com/signup");

// Post in the form field named cf-captcha-response
result.Token;

// Submit under the UA that minted the token, not your own
result.UserAgent;
```

Uses `recaptchaTimeout` — it is a real browser session, and longer again when an
interactive challenge is drawn.

---

## 9. Friendly Captcha — `FriendlyCaptchaAsync(sitekey, url, options?)`

Proof-of-work, with nothing shown on screen. The widget searches for values that
hash below a difficulty the service sets per request.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `friendly_captcha` |
| `sitekey` | string | Yes | The `data-sitekey` of the element carrying `class="frc-captcha"` |
| `pageurl` | string | Yes | Full URL of the page the widget appears on |
| `version` | string | No | `v1` (default) or `v2`; a bare `1` or `2` is accepted |
| `module_script` | string | No | `src` of the widget script tag carrying `type="module"` |
| `nomodule_script` | string | No | `src` of the widget script tag carrying `nomodule` |
| `api_server` | string | No | Data residency endpoint: `global` (default), `eu`, or a full URL |
| `useragent` | string | No | User-Agent to send with the request |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

### Version 1 and version 2

Two entirely different protocols ship under this one name, and a sitekey does not
tell you which one a site uses — they share a brand and a sitekey namespace and
nothing else. Both are live. Solve the wrong one and you get a well-formed token
the target site rejects, with no indication that the version was the problem.

| `version` | Widget package | Script |
|---|---|---|
| `v1` | `friendly-challenge` | `widget.module.min.js` / `widget.min.js` |
| `v2` | `@friendlycaptcha/sdk` | `site.min.js` |

CapSkip decides in this order, stopping at the first answer: the `version`
parameter; then the script URL from `module_script` or `nomodule_script`, which
is the most reliable signal because it is the build the site actually loads; then
v1. The SDK refuses any other `version` value locally.

### Submitting the token

| `version` | Form field |
|---|---|
| `v1` | `frc-captcha-solution` |
| `v2` | `frc-captcha-response` |

The field names differ, which is what catches an integration moved from one to
the other. A v1 token is four dot-separated parts and runs to a few hundred
characters; a v2 token is a single opaque string beginning `AQQA.` and is roughly
six kilobytes, so size whatever carries it accordingly.

### SDK usage

```csharp
// Say which version
var result = await solver.FriendlyCaptchaAsync(
    "FCMGEMUD2M567T8G",
    "https://example.com/signup",
    new Dictionary<string, object?> { ["version"] = "v2" });

// …or let the script URL decide it
var result = await solver.FriendlyCaptchaAsync(
    "FCMGEMUD2M567T8G",
    "https://example.com/signup",
    new Dictionary<string, object?>
    {
        ["module_script"] = "https://cdn.example.com/@friendlycaptcha/sdk@0.1.6/site.min.js",
    });

result.Token;
```

Uses `recaptchaTimeout`: the service decides how much work a request is worth at
the moment it is made, so solve time is not a constant, and v2 always solves in a
browser.

---

## 10. AWS WAF — `AmazonWafAsync(sitekey, url, options?)`

AWS WAF guards a site either with a challenge page (an HTTP 405 page carrying
`window.gokuProps`) or with a CAPTCHA widget the site renders itself
(`AwsWafCaptcha.renderCaptcha`). CapSkip solves both in a real browser and
returns a voucher, which AWS trades for the `aws-waf-token` cookie. Public docs:
<https://capskip.com/api-docs/#aws-waf>.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `amazon_waf` |
| `sitekey` | string | Yes | Challenge page: `gokuProps.key`. Widget page: the widget's `apiKey` |
| `pageurl` | string | Yes | Full http or https URL of the page |
| `iv` | string | No | Challenge page: `gokuProps.iv`. Send together with `context`, or neither |
| `context` | string | No | Challenge page: `gokuProps.context`. Send together with `iv`, or neither |
| `challenge_script` | string | No | Full URL of the page's challenge.js |
| `captcha_script` | string | No | Full URL of the page's captcha.js |
| `jsapiScript` | string | No | Widget page: full URL of jsapi.js |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | Proxy type |

`iv` and `context` come off the same challenge page and only work as a pair, so
the SDK refuses one without the other. Left out, CapSkip reads them from
`pageurl` at solve time — the safer choice when your copy of the page is more
than a few minutes old. An optional sent as an empty string is treated as not
sent, exactly as the server does.

### Script URLs

CapSkip POSTs the solution to the host a script URL names, so it accepts only
genuine AWS WAF scripts. Each of `challenge_script`, `captcha_script` and
`jsapiScript` has to be:

- an absolute `https` URL with no user name or password and no explicit port
  other than 443;
- on a host ending in `.awswaf.com`;
- a path of one or more `[A-Za-z0-9_-]` directories followed by `challenge.js`,
  `captcha.js` or `jsapi.js` (a query string is fine).

Anything else is refused with a `ValidationException` before the request is
sent, because it is not an AWS WAF integration. The server applies the same rule,
but only once the task is queued.

### GET `/res.php`

The answer is **not a token**. It is an object of two values that only work as a
pair:

```json
{"status": 1, "request": {"captcha_voucher": "...", "existing_token": "..."}}
```

and in plain text, the same object as one line after `OK|`. The SDK expands it
into `CaptchaVoucher` and `ExistingToken`. See [Return value](#return-value).

### Using the result

POST `{"captcha_voucher": ..., "existing_token": ...}` as JSON to the `voucher`
endpoint beside the page's challenge.js. The `token` in its response is the
`aws-waf-token` cookie. Exchange the pair as soon as
it arrives rather than storing it for later.

### SDK usage

```csharp
// Challenge page
var result = await solver.AmazonWafAsync(
    "AQIDAHjcYuEXAMPLEghicBgQ/7bFaQZ+m5FKCMDnO+vTbNg96AHDh0IR5vgzHNceHYqZR+GOAAAAfjB8",
    "https://example.com/",
    new Dictionary<string, object?>
    {
        ["iv"] = "CgAFRDIjfgAAA5yY",
        ["context"] = "Rbp3r8fvbuhmrCqpUe8Mh1TLvs8OSKJZiSs2vCF4dt9rxVZI7xYJa2n5k/9r+XaXe7KUIu2I8nXTDHHkDvIOTDc2",
        ["challenge_script"] = "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/challenge.js",
    });

// Widget page
var result = await solver.AmazonWafAsync(
    "Xr9kEXAMPLE2bWQx1vTq8LmZ0aYc4dHs7nFp3gJt6uKw5eRi9AbCdEfGh=_0_1",
    "https://example.com/login",
    new Dictionary<string, object?>
    {
        ["jsapiScript"] = "https://5b7e0a19c3d8.us-east-1.captcha-sdk.awswaf.com/5b7e0a19c3d8/jsapi.js",
    });

result.CaptchaVoucher; result.ExistingToken;
```

Uses `recaptchaTimeout`: a real browser mints the token the voucher is issued
for, and the puzzle rounds go through AWS on top of that.

---

## 11. DataDome — `DataDomeAsync(captchaUrl, url, options?)`

DataDome is the bot protection behind many large shops, news sites and
marketplaces. When it doubts a visitor, the site answers with a block page
(status 403, sometimes 401) holding a slider or a short device check. CapSkip
passes that challenge and returns the `datadome` cookie; requests that carry it
are let through. Public docs: <https://capskip.com/api-docs/#datadome>.

The arguments follow 2Captcha's `datadome(captcha_url, pageurl, userAgent,
proxy)`: the challenge address and the page URL positionally, and the optional
`userAgent` and `proxy` in the options dictionary. `url` is accepted there for
`pageurl`, as on every other method.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `datadome` |
| `captcha_url` | string | Yes | Address of the challenge iframe on `geo.captcha-delivery.com` |
| `pageurl` | string | Yes | Full http or https URL of the page that returned the block page |
| `userAgent` | string | No | The User-Agent you will send with the cookie: Chrome for Windows, same major version as CapSkip's browser. Sent as `useragent` |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | The proxy your later requests go through |
| `proxytype` | string | No | `HTTP`, `HTTPS`, `SOCKS5` or `SOCKS5H` |

Refused with a `ValidationException` before the request is made, as the server
would refuse them:

- a missing `captcha_url` or `pageurl`;
- a `captcha_url` that is not https on captcha-delivery.com (or a subdomain),
  under `/captcha/` or `/interstitial/`, carrying `initialCid`, `hash` and
  `cid` (server: `ERROR_BAD_PARAMETERS`);
- a `captcha_url` with `t=bv`: the block page bans the address that fetched it,
  so nothing sent from there can pass (server: `ERROR_PROXY_BANNED`);
- a `pageurl` that is not http(s), or that points at captcha-delivery.com
  (server: `ERROR_PAGEURL`);
- a `userAgent` that is not stock Chrome for Windows: Firefox, Safari, Edge,
  Opera, macOS, Linux, Android and iOS all fail (server:
  `ERROR_WRONG_USERAGENT`).

The Chrome **major version** is checked by CapSkip itself, against the browser
it solves with, and a mismatch comes back as an `ApiException` with
`ERROR_WRONG_USERAGENT`. An optional sent as an empty string is treated as not
sent, exactly as the server does.

### Building the `captchaUrl`

If the block page already contains the challenge iframe, take its `src` as it
is (an HTML-escaped `&amp;` is fine; it is sent unchanged and CapSkip decodes
it). Most block pages build it with a script from a `var dd={...}` object
instead:

```csharp
var json = Regex.Match(html, @"var dd=(\{[^}]*\})").Groups[1].Value.Replace('\'', '"');
using var doc = JsonDocument.Parse(json);
var dd = doc.RootElement;
string? Get(string name) => dd.TryGetProperty(name, out var v)
    ? (v.ValueKind == JsonValueKind.String ? v.GetString() : v.GetRawText())
    : null;

var path = Get("rt") == "i" ? "interstitial" : "captcha";
var q = new List<(string Key, string? Value)>
{
    ("initialCid", Get("cid")), ("hash", Get("hsh")), ("cid", Get("cookie")),
};
if (!string.IsNullOrEmpty(Get("t"))) q.Add(("t", Get("t")));
q.Add(("referer", pageUrl));
q.Add(("s", Get("s")));
foreach (var k in new[] { "e", "b" })
{
    if (!string.IsNullOrEmpty(Get(k))) q.Add((k, Get(k)));
}
q.Add(("dm", "cd"));

var captchaUrl = $"https://{Get("host") ?? "geo.captcha-delivery.com"}/{path}/?"
    + string.Join("&", q.Select(p => p.Key + "=" + Uri.EscapeDataString(p.Value ?? "")));
```

Each block page is a fresh challenge: submit it soon, and request the page again
for the next task rather than reusing an old `captchaUrl`.

### The cookie belongs to one IP and one User-Agent

DataDome refuses the cookie from any other address, or with any other
User-Agent:

- **Solve from the address you will use the cookie from.** Pass the proxy your
  requests go through; pass none if they come from the machine running CapSkip.
- **Send the User-Agent you will use, or none.** With none, CapSkip uses its
  browser's own, and the result names it in `UserAgent`.
- **CapSkip's own DataDome proxy list.** The app can hold a list of proxies for
  DataDome (Settings, Captcha types, DataDome, Configure Proxies). A task sent
  without a proxy is solved through that list, and the result then carries
  `Proxy`: that proxy as `host:port`, without its login. The cookie only works
  through it. A proxy sent with the task always takes priority, and then
  `Proxy` stays `null`.

### GET `/res.php`

The SDK polls with `json=1`, where CapSkip returns the cookie in `request` with
`userAgent` and, when its list supplied the proxy, `proxy` next to it:

```json
{
  "status": 1,
  "request": "datadome=FAPPpli4...; Max-Age=31536000; Domain=.example.com; Path=/; Secure; SameSite=Lax",
  "userAgent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36",
  "proxy": "203.0.113.10:8080"
}
```

(In plain-text mode the same two values arrive in the `X-DataDome-User-Agent`
and `X-DataDome-Proxy` response headers.) See [Return value](#return-value).

### SDK usage

```csharp
// 2Captcha's order: captcha_url, pageurl, then userAgent and proxy
var result = await solver.DataDomeAsync(captchaUrl, "https://www.example.com/",
    new Dictionary<string, object?>
    {
        ["userAgent"] = userAgent,
        ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
    });

// No proxy, no User-Agent: CapSkip's browser, and its proxy list if it has one
var result = await solver.DataDomeAsync(captchaUrl, "https://www.example.com/");

request.Headers.TryAddWithoutValidation("User-Agent", result.UserAgent);
request.Headers.TryAddWithoutValidation("Cookie", result.Cookie);   // "datadome=..."
if (result.Proxy != null)
{
    // send this request through result.Proxy
}
```

DataDome may send a replacement `datadome` cookie on later responses; keep the
newest one, as a browser would. When the block page comes back anyway, build a
new `captchaUrl` from it and submit a new task.

Uses `recaptchaTimeout`: the challenge is passed in a real browser.

---

## 12. hCaptcha — `HCaptchaAsync(sitekey, url, options?)`

hCaptcha is the checkbox widget behind many sign-up, login and checkout forms.
CapSkip passes the widget for the sitekey and page you send and returns the
token, ready to submit with the form. Invisible widgets and hCaptcha Enterprise
use the same method. Public docs: <https://capskip.com/api-docs/#hcaptcha>.

The arguments follow 2Captcha's `hcaptcha(sitekey, url, ...)`, so code written
against it keeps working. `pageurl` is accepted in the options for `url`. A
typed overload, `HCaptchaAsync(sitekey, url, bool invisible, options?)`, takes
`invisible` as a bool.

### POST `/in.php`

| Parameter | Type | Required | Description |
|---|---|---|---|
| `key` | string | Yes | CapSkip API key |
| `method` | string | Yes | `hcaptcha` |
| `sitekey` | string | Yes | The widget's sitekey (`data-sitekey`) |
| `pageurl` | string | Yes | Full http or https URL of the page the widget is on |
| `invisible` | int | No | `1` for an invisible widget (`data-size="invisible"`). The SDK takes `true`/`false`, `1`/`0` or `"1"`/`"0"`/`"true"`/`"false"`, sends `1`, and leaves it out when off |
| `data` | string | No | hCaptcha Enterprise: the `rqdata` value the page passes to `hcaptcha.setData()`. Single use; fetch a fresh one per task |
| `userAgent` | string | No | The User-Agent you will submit the form with. Used only when it matches CapSkip's browser. Sent as `useragent` |
| `json` | int | No | `0` plain text, `1` JSON |
| `proxy` | string | No | Proxy address |
| `proxytype` | string | No | `HTTP`, `HTTPS`, `SOCKS5` or `SOCKS5H` |

Refused locally with `ValidationException`, as CapSkip would refuse them: a
missing sitekey (`ERROR_RECAPTCHA_INVALID_SITEKEY`), a `pageurl` that is missing
or not an absolute http(s) address (`ERROR_PAGEURL`), an `invisible` value other
than 0/1/true/false, and any parameter not in the table, 2Captcha's `domain`
among them.

### GET `/res.php`

The SDK polls with `json=1`, where CapSkip returns the token in `request` with
`respKey` and the User-Agent the token was earned with:

```json
{
  "status": 1,
  "request": "P1_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.hadwYXNza2V5xQZ2...f3Kq8nV2",
  "respKey": "E0_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9.eyJpZCI6IjA0...9wB1",
  "useragent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36"
}
```

(In plain-text mode the User-Agent arrives in the `X-HCaptcha-User-Agent`
response header.) A task that hCaptcha refused the sitekey for fails with
`ERROR_RECAPTCHA_INVALID_SITEKEY`, one whose proxy could not be reached with
`ERROR_PROXY_CONNECT_REFUSED`; resubmitting the same task would fail again.

### SDK usage

```csharp
var result = await solver.HCaptchaAsync("a5f74b19-9e45-40e0-b45d-47ff91b7a6c2",
    "https://accounts.hcaptcha.com/demo");

// Invisible Enterprise widget, through a proxy
var result = await solver.HCaptchaAsync(sitekey, pageUrl, invisible: true,
    new Dictionary<string, object?>
    {
        ["data"] = rqdata,
        ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
    });

form["h-captcha-response"] = result.Token;
form["g-recaptcha-response"] = result.Token;   // if the form has one
request.Headers.TryAddWithoutValidation("User-Agent", result.UserAgent);
```

Tokens are single use and expire about two minutes after they are issued.

Uses `recaptchaTimeout`: the widget is passed in a real browser.

---

## Return value

Every solve method returns a `SolveResult`:

```csharp
public sealed class SolveResult
{
    public string CaptchaId { get; }
    public string Code { get; }
    public string? UserAgent { get; }    // Turnstile, CaptchaFox, DataDome & hCaptcha, when the poll includes it
    public string? Challenge { get; }    // GeeTest
    public string? Validate { get; }     // GeeTest
    public string? Seccode { get; }      // GeeTest
    public string? Token { get; }        // ALTCHA, CaptchaFox, Friendly Captcha, hCaptcha
    public long? Number { get; }         // ALTCHA
    public string? CaptchaKey { get; }   // Capy — capy_captchakey
    public string? ChallengeKey { get; } // Capy — capy_challengekey
    public string? Answer { get; }       // Capy — capy_answer (the drag path)
    public string? RespKey { get; }      // hCaptcha — never null, "" when none; Capy — empty
    public string? CaptchaVoucher { get; } // AWS WAF — captcha_voucher
    public string? ExistingToken { get; }  // AWS WAF — existing_token
    public string? Cookie { get; }       // DataDome — the datadome=... pair
    public string? Proxy { get; }        // DataDome — host:port, when CapSkip picked the proxy
}
```

CaptchaFox and Friendly Captcha set `Token` to the same string as `Code`, named
for the form field it goes in. CaptchaFox also sets `UserAgent` when the solve
reported one — the UA the browser minted the token under, not one you sent; it
stays `null` when the solve captured none rather than being guessed.

Capy's answer is an object rather than a token. `Code` carries it as JSON — the
same text a plain-text poll delivers after `OK|` — and it is expanded into
`CaptchaKey`, `ChallengeKey`, `Answer` and `RespKey`, the fields the target form
takes.

AWS WAF's answer is an object too, carried in `Code` the same way and expanded
into `CaptchaVoucher` and `ExistingToken`. The two only work as a pair; POST them
to the site's `voucher` endpoint under their wire names, `captcha_voucher` and
`existing_token`.

DataDome returns a cookie. `Code` keeps it exactly as DataDome set it, for a
cookie jar; `Cookie` is the `datadome=...` pair alone, for a Cookie header.
`UserAgent` is the User-Agent the cookie was earned with, and `Proxy` is set
only when the task sent no proxy and CapSkip used its own DataDome proxy list
(it is carried from any poll reply that names a proxy):

```csharp
// result.CaptchaId  "12345"
// result.Code       "datadome=FAPPpli4...; Max-Age=31536000; Domain=.example.com; Path=/; Secure; SameSite=Lax"
// result.Cookie     "datadome=FAPPpli4..."
// result.UserAgent  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36"
// result.Proxy      "203.0.113.10:8080"
```

hCaptcha sets `Token`, the same string as `Code`, plus `RespKey` and the
User-Agent the token was earned with. `RespKey` is never `null` on an hCaptcha
result, and empty when the widget handed out none (it is carried from any poll
reply that includes one):

```csharp
// result.CaptchaId  "12345"
// result.Code       "P1_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..."
// result.Token      "P1_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..."
// result.RespKey    "E0_eyJ0eXAiOiJKV1QiLCJhbGciOiJIUzI1NiJ9..."
// result.UserAgent  "Mozilla/5.0 (Windows NT 10.0; Win64; x64) ..."
```

---

## SDK parameter aliases

Convenience aliases mapped before sending to CapSkip:

| SDK alias | CapSkip API param |
|---|---|
| `url` | `pageurl` |
| `score` | `min_score` |
| `minScore` | `min_score` |
| `datas` | `data-s` |
| `data_s` | `data-s` |
| `apiServer` | `api_server` |
| `api_subdomain` | `api_server` |
| `userAgent` / `user_agent` | `useragent` |
| `captchaKey` | `captchakey` |
| `moduleScript` | `module_script` |
| `nomoduleScript` / `noModuleScript` | `nomodule_script` |
| `challengeScript` | `challenge_script` |
| `captchaScript` | `captcha_script` |
| `jsapi_script` | `jsapiScript` |
| `captchaUrl` / `captchaURL` | `captcha_url` |
| `proxy` (`Proxy` / dictionary) | `proxy` + `proxytype` strings |

```csharp
var proxy = new Proxy("HTTPS", "login:password@1.2.3.4:3128");
// or: new Dictionary<string, object?> { ["type"] = "HTTPS", ["uri"] = "..." }
```

Unsupported parameters (e.g. `numeric` on image captcha, `action` on v2) throw
`ValidationException`.

---

## Manual workflow

### `SendAsync(parameters)`

Submit without polling. Returns the captcha ID string.

```csharp
var captchaId = await solver.SendAsync(new Dictionary<string, object?>
{
    ["method"] = "userrecaptcha",
    ["googlekey"] = "...",
    ["pageurl"] = "https://example.com",
});
```

### `GetResultAsync(id, json = 0)`

Poll once. Throws `NetworkException` while `CAPCHA_NOT_READY`.

```csharp
var code = (string)await solver.GetResultAsync(captchaId);                        // plain text
var data = (IDictionary<string, object?>)await solver.GetResultAsync(captchaId, 1); // object when json=1
```

### `SolveAsync(options)` / `WaitResultAsync(id, timeout, pollingInterval, json)`

`SolveAsync` submits then polls to completion; `WaitResultAsync` polls an existing
id until solved or the timeout (in seconds) elapses. These power the higher-level
solve methods.

---

## Exceptions

| Exception | When |
|---|---|
| `ValidationException` | Invalid/unsupported parameters |
| `NetworkException` | Connection error, or captcha not ready |
| `ApiException` | CapSkip API error response |
| `TimeoutException` | Polling timeout exceeded |

All derive from `CapSkipError`. `CapSkip.TimeoutException` and
`CapSkip.ValidationException` share short names with `System` types — qualify them
or catch the base `CapSkipError`.

---

## Low-level HTTP (ApiClient)

```csharp
using CapSkip;

var client = new ApiClient(host: "127.0.0.1", port: 8080);

await client.InAsync(new Dictionary<string, object?>
{
    ["method"] = "turnstile",
    ["key"] = "capskip",
    ["sitekey"] = "...",
    ["pageurl"] = "...",
});

await client.ResAsync(new Dictionary<string, object?>
{
    ["key"] = "capskip",
    ["action"] = "get",
    ["id"] = "12345",
    ["json"] = 1,
});
```

`AsyncApiClient` is a subclass alias of `ApiClient`.
