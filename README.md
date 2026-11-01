# CapSkip .NET SDK — Captcha Solver for C# and .NET

[![NuGet](https://img.shields.io/nuget/v/CapSkip.svg)](https://www.nuget.org/packages/CapSkip/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET-Standard%202.0-blueviolet.svg)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml)

**Solve reCAPTCHA v2, reCAPTCHA v3, Cloudflare Turnstile, GeeTest, ALTCHA, Capy Puzzle, CaptchaFox, Friendly Captcha, AWS WAF, DataDome and image captchas from C#.**

Official .NET client for [CapSkip](https://capskip.com), a **local captcha solver** that runs on your own machine. Licensed once, not billed per solve.

```bash
dotnet add package CapSkip
```

Targets **.NET Standard 2.0**, so it runs on .NET 6/7/8/9+, .NET Core 2.0+, and .NET Framework 4.6.1+.

---

## How it works

CapSkip is a desktop app. It does the solving on your machine and exposes the standard captcha-solver HTTP API — the same `in.php` / `res.php` endpoints every 2captcha-compatible client already speaks — on `127.0.0.1:8080`.

This SDK is a thin wrapper over that API, with the method names you would expect: `NormalAsync()`, `RecaptchaAsync()`, `TurnstileAsync()`, `GeetestAsync()`, `AltchaAsync()`, `CapyAsync()`, `CaptchaFoxAsync()`, `FriendlyCaptchaAsync()`, `AmazonWafAsync()`, `DataDomeAsync()`. Nothing leaves your network, and there is no credit balance to keep an eye on.

## Supported captcha types

| Captcha | Method |
|---|---|
| **Image captcha solver** (distorted text / OCR) | `solver.NormalAsync(file)` |
| **reCAPTCHA v2 solver** (checkbox) | `solver.RecaptchaAsync(sitekey, url)` |
| **reCAPTCHA v2 invisible solver** | `RecaptchaAsync(sitekey, url, new() { ["invisible"] = 1 })` |
| **reCAPTCHA Enterprise solver** | `RecaptchaAsync(sitekey, url, new() { ["enterprise"] = 1 })` |
| **reCAPTCHA v3 solver** | `RecaptchaAsync(sitekey, url, new() { ["version"] = "v3", ["action"] = "submit" })` |
| reCAPTCHA v3 Enterprise | `RecaptchaAsync(sitekey, url, new() { ["version"] = "v3", ["enterprise"] = 1 })` |
| **Cloudflare Turnstile solver** (widget) | `solver.TurnstileAsync(sitekey, url)` |
| Cloudflare Turnstile (challenge page) | `TurnstileAsync(sitekey, url, new() { ["data"] = ..., ["pagedata"] = ... })` |
| **GeeTest v3 solver** (slide puzzle) | `solver.GeetestAsync(gt, challenge, url)` |
| **ALTCHA solver** (proof-of-work) | `solver.AltchaAsync(url, options)` |
| **Capy Puzzle solver** (slide puzzle) | `solver.CapyAsync(sitekey, url)` |
| **CaptchaFox solver** (widget) | `solver.CaptchaFoxAsync(sitekey, url)` |
| **Friendly Captcha solver** (proof-of-work) | `FriendlyCaptchaAsync(sitekey, url, new() { ["version"] = "v2" })` |
| **AWS WAF solver** (challenge page or widget) | `solver.AmazonWafAsync(sitekey, url, options)` |
| **DataDome solver** (slider and device check) | `solver.DataDomeAsync(captchaUrl, url)` |

The options argument is a `Dictionary<string, object?>` — the table shortens it to `new() { ... }` to stay readable; full signatures are in the [API Reference](docs/API_REFERENCE.md).

**hCaptcha and FunCaptcha/Arkose are not supported.** hCaptcha is the one people misidentify most often, since it also puts a `data-sitekey` on the widget — check for `class="h-captcha"` or a `js.hcaptcha.com` script before reaching for `RecaptchaAsync()`.

Point the SDK at the [live captcha demo pages](https://capskip.com/captcha-demo/) to sanity-check your setup against real widgets.

---

## Quick start (5 minutes)

### 1. Install the CapSkip captcha solver

Download and run the CapSkip desktop app — [CapSkipInstaller.msi](https://capskip.com/download/CapSkipInstaller.msi). Leave it running in the background.

In CapSkip settings, note:

- **API port** (default: `8080`)
- **API key** (optional — if validation is disabled, any string works)

### 2. Install the SDK

```bash
dotnet add package CapSkip
```

### 3. Solve your first captcha

```csharp
using CapSkip;

var solver = new CapSkipClient(host: "127.0.0.1", port: 8080);

var result = await solver.RecaptchaAsync(
    "YOUR_SITEKEY",
    "https://example.com/page-with-recaptcha");

Console.WriteLine(result.Code); // g-recaptcha-response token
```

> **Prerequisite:** CapSkip must be running before you call the SDK. If you see a connection error, see [Troubleshooting](docs/TROUBLESHOOTING.md).

Every solve method is asynchronous — `await` it (or call `.GetAwaiter().GetResult()` from synchronous code).

---

## Why solve captchas locally

Cloud captcha APIs charge per solve, which turns a retry loop into an expense and routes every page URL and sitekey you touch through someone else's queue.

CapSkip flips that around:

- **Unlimited solving** — one license, no per-captcha charge, no balance to top up
- **Runs on `127.0.0.1`** — the SDK never talks to a third-party server
- **No per-key rate limit** — throughput is whatever your machine can manage
- **Fast** — image captchas come back in well under a second; a typical reCAPTCHA v2 lands in 30–45 seconds

### Coming from 2captcha or Anti-Captcha

CapSkip answers on the same `in.php` / `res.php` endpoints, so it works as a **2captcha API alternative**: an existing integration usually needs nothing more than its host pointed at `127.0.0.1:8080`. The [migration notes](https://capskip.com/2captcha-api-alternative/) cover the details, if you would rather keep your current client library than switch to this one.

---

## Documentation

| Guide | Description |
|---|---|
| [Tutorial](docs/TUTORIAL.md) | Complete walkthrough of every captcha type |
| [Getting Started](docs/GETTING_STARTED.md) | Full setup: CapSkip app, SDK install, first program |
| [API Reference](docs/API_REFERENCE.md) | All classes, methods, parameters, and return values |
| [Examples](examples/) | Ready-to-run samples for every captcha type |
| [Troubleshooting](docs/TROUBLESHOOTING.md) | Connection errors, timeouts, proxy issues |
| [Contributing](CONTRIBUTING.md) | Development setup, tests, pull requests |
| [Changelog](CHANGELOG.md) | Release history |

---

## Configuration

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: "capskip",        // your CapSkip API key (or any string if validation is off)
    host: "127.0.0.1",        // CapSkip host
    port: 8080,               // CapSkip port from app settings
    defaultTimeout: 120,      // seconds — image captcha polling timeout
    recaptchaTimeout: 300,    // seconds — reCAPTCHA / Turnstile / GeeTest / CaptchaFox /
                              // Friendly Captcha / AWS WAF / DataDome polling timeout
                              // (ALTCHA and Capy use defaultTimeout — neither is a browser solve)
    pollingInterval: 5);      // max seconds between res.php polls (starts at 0.25s, backs off to this)
```

Use environment variables in production:

```bash
# Linux / macOS
export CAPSKIP_API_KEY="your-key"
export CAPSKIP_HOST="127.0.0.1"
export CAPSKIP_PORT="8080"
```

```powershell
# Windows PowerShell
$env:CAPSKIP_API_KEY = "your-key"
$env:CAPSKIP_HOST = "127.0.0.1"
$env:CAPSKIP_PORT = "8080"
```

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: Environment.GetEnvironmentVariable("CAPSKIP_API_KEY") ?? "capskip",
    host: Environment.GetEnvironmentVariable("CAPSKIP_HOST") ?? "127.0.0.1",
    port: int.TryParse(Environment.GetEnvironmentVariable("CAPSKIP_PORT"), out var p) ? p : 8080);
```

---

## Usage examples

### Image captcha

```csharp
await solver.NormalAsync("captcha.png");
await solver.NormalAsync("https://example.com/captcha.jpg");
await solver.NormalAsync("data:image/png;base64,iVBORw0KGgo...");
// result.Code holds the recognized text
```

### reCAPTCHA v2 / v3

```csharp
// reCAPTCHA v2
var v2 = await solver.RecaptchaAsync("...", "https://example.com");

// reCAPTCHA v3
var v3 = await solver.RecaptchaAsync("...", "https://example.com", new Dictionary<string, object?>
{
    ["version"] = "v3",
    ["action"] = "submit",
    ["score"] = 0.7,
});
```

### Cloudflare Turnstile

```csharp
var result = await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com");
```

### GeeTest v3

`gt` is static per site, but `challenge` is single-use and expires in about a
minute — fetch a fresh pair right before solving.

```csharp
var result = await solver.GeetestAsync(
    "81388ea1fc187e0c335c0a8907ff2625",
    "7cf6a8b1a2c34d5e6f7089abcdef0123",
    "https://example.com/login");

// Post these back exactly as the site's own front-end would
result.Challenge; result.Validate; result.Seccode;
```

### ALTCHA

ALTCHA is proof-of-work, not recognition — there is nothing to read, so a solve
is deterministic and takes milliseconds. Give CapSkip the endpoint that serves
the challenge, or the challenge document itself.

```csharp
var result = await solver.AltchaAsync(
    "https://example.com/signup",
    new Dictionary<string, object?>
    {
        ["challenge_url"] = "https://example.com/captcha/api/altcha/challenge",
    });

// Post this back in the form field the widget uses, named `altcha`
result.Token;
```

Challenges expire fast — some sites inside two minutes — so fetch one
immediately before solving and submit the token promptly.

### Capy Puzzle

A Capy solution is not a token. It is three values that together go into the
target form, so post all three back exactly as returned.

```csharp
var result = await solver.CapyAsync(
    "PUZZLE_Abc1dEFghIJKLM2no34P56q7rStu8v",
    "https://example.com/login");

// capy_captchakey, capy_challengekey and capy_answer respectively
result.CaptchaKey; result.ChallengeKey; result.Answer;
```

`Answer` is the drag path the widget would have recorded — submit it verbatim,
without trimming or re-encoding it. The challenge key is single-use and
short-lived, so submit promptly rather than caching the three values.

Pass `api_server` when the widget script points somewhere other than
`https://jp.api.capy.me`. Only the `puzzle` family is solved; `["version"] =
"avatar"` is refused rather than answered with a puzzle answer the site would
reject.

### CaptchaFox

```csharp
var result = await solver.CaptchaFoxAsync(
    "sk_xtNxpk6fCdFbxh1_xJeGflSdCE9tn99G",
    "https://example.com/signup");

// Post this back in the form field the widget uses, named `cf-captcha-response`
result.Token;

// The UA the token was minted under — submit under this one, not your own
result.UserAgent;
```

`url` has to be the page the widget actually runs on: CaptchaFox checks it
against the domains the key is registered for and refuses a mismatch
permanently, not intermittently.

Pass `api_server` only when the page does not load the default widget. A page
loading the MAM package expects a `MAM_` prefixed token, and sending the wrong
source still succeeds — it just returns a token the site will not accept, which
reads as a silent verification failure rather than an error.

### Friendly Captcha

Two different protocols ship under this name and a sitekey does not tell you
which one a site uses, so say which — or send the widget script and let CapSkip
read it off the build the site actually loads.

```csharp
var result = await solver.FriendlyCaptchaAsync(
    "FCMGEMUD2M567T8G",
    "https://example.com/signup",
    new Dictionary<string, object?> { ["version"] = "v2" });

// v1 → frc-captcha-solution, v2 → frc-captcha-response
result.Token;
```

```csharp
// Or let the script URL decide the version
var result = await solver.FriendlyCaptchaAsync(
    "FCMGEMUD2M567T8G",
    "https://example.com/signup",
    new Dictionary<string, object?>
    {
        ["module_script"] = "https://cdn.example.com/@friendlycaptcha/sdk@0.1.6/site.min.js",
    });
```

Solving the wrong version returns a well-formed token the target site rejects,
with nothing to indicate the version was the problem. With neither `version` nor
a script URL, v1 is assumed. Pass `["api_server"] = "eu"` for a sitekey on the EU
data-residency tenant. A v2 token is roughly six kilobytes, so size whatever
carries it accordingly.

### AWS WAF

An AWS WAF solution is not a token either. It is a voucher and the token it was
issued for, which only work as a pair: you trade them with AWS for the
`aws-waf-token` cookie.

On a **challenge page** (the HTTP 405 page carrying `window.gokuProps`), the
sitekey is `gokuProps.key`. `iv` and `context` are `gokuProps.iv` and
`gokuProps.context`; they are optional and travel together — send both or
neither. Left out, CapSkip reads them from the page at solve time, which is the
safer choice when your copy of the page is more than a few minutes old.

```csharp
var result = await solver.AmazonWafAsync(
    "AQIDAHjcYuEXAMPLEghicBgQ/7bFaQZ+m5FKCMDnO+vTbNg96AHDh0IR5vgzHNceHYqZR+GOAAAAfjB8",
    "https://example.com/",
    new Dictionary<string, object?>
    {
        ["iv"] = "CgAFRDIjfgAAA5yY",
        ["context"] = "Rbp3r8fvbuhmrCqpUe8Mh1TLvs8OSKJZiSs2vCF4dt9rxVZI7xYJa2n5k/9r+XaXe7KUIu2I8nXTDHHkDvIOTDc2",
        ["challenge_script"] = "https://3f2a9c7d41b0.edge.sdk.awswaf.com/3f2a9c7d41b0/8e51d0c7a2f4/challenge.js",
    });

result.CaptchaVoucher; result.ExistingToken;
```

On a **widget page** (`AwsWafCaptcha.renderCaptcha`), the sitekey is the
widget's `apiKey`; pass `jsapiScript` with the full URL of jsapi.js and no
`iv` / `context`.

```csharp
var result = await solver.AmazonWafAsync(
    "Xr9kEXAMPLE2bWQx1vTq8LmZ0aYc4dHs7nFp3gJt6uKw5eRi9AbCdEfGh=_0_1",
    "https://example.com/login",
    new Dictionary<string, object?>
    {
        ["jsapiScript"] = "https://5b7e0a19c3d8.us-east-1.captcha-sdk.awswaf.com/5b7e0a19c3d8/jsapi.js",
    });
```

To use the result, POST `{"captcha_voucher": ..., "existing_token": ...}` as
JSON to the `voucher` endpoint beside the page's challenge.js. The `token` in
its response is the `aws-waf-token` cookie. Exchange the pair as soon as it arrives
rather than storing it for later.

Every script URL — `challenge_script`, `captcha_script`, `jsapiScript` — must be
an https URL on an awswaf.com host ending in challenge.js, captcha.js or
jsapi.js. Anything else is refused with a `ValidationException` before the
request is sent: CapSkip posts the solution to the host the script names, so a
script that is not an AWS WAF integration is never accepted. `challengeScript`,
`captchaScript` and `jsapi_script` are accepted as aliases. Full parameters are
in the [CapSkip API docs](https://capskip.com/api-docs/#aws-waf).

### DataDome

DataDome answers a request it doubts with a block page (status 403, sometimes
401) instead of the site. CapSkip passes the challenge on that page, a slider
or a device check, and returns the `datadome` cookie that lets your requests
through. The arguments follow 2Captcha's `datadome(captcha_url, pageurl,
userAgent, proxy)`: the challenge address and the page URL positionally, and
the optional `userAgent` and `proxy` in the options dictionary.

```csharp
var result = await solver.DataDomeAsync(
    "https://geo.captcha-delivery.com/captcha/?initialCid=...&hash=...&cid=...&t=fe&referer=...&s=45225&e=...&dm=cd",
    "https://www.example.com/",                    // the page that returned the block page
    new Dictionary<string, object?>
    {
        ["userAgent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
                      + "(KHTML, like Gecko) Chrome/156.0.0.0 Safari/537.36",
        ["proxy"] = new Proxy("HTTP", "login:password@203.0.113.10:8080"),
    });

result.Cookie;     // "datadome=..." -- send it as the Cookie header
result.Code;       // the cookie exactly as DataDome set it: Max-Age, Domain, Path...
result.UserAgent;  // the User-Agent the cookie was earned with
result.Proxy;      // set only when CapSkip picked the proxy, see below
```

`captchaUrl` is the address of the challenge iframe on
`geo.captcha-delivery.com`: `/captcha/` for a slider, `/interstitial/` for a
device check. Take the iframe's `src` when the block page has one; most build it
with a script instead, so assemble it from the page's `var dd={...}` object as
the [API docs](https://capskip.com/api-docs/#datadome) show. Each block page is
a fresh challenge, so submit it soon and fetch the page again for the next task.
`captchaUrl` / `captchaURL` are accepted as option aliases of `captcha_url`.

**The cookie belongs to one IP address and one browser.** DataDome refuses it
from any other address, or with any other User-Agent, so:

- **Send the User-Agent you will use**, or none. It must be Chrome for Windows,
  with the same major version as the browser CapSkip solves with; anything else
  (Firefox, Safari, Edge, mobile, macOS) would earn a cookie the site rejects,
  so it is refused with a `ValidationException` before the request is sent, and
  a different Chrome major is refused by CapSkip with `ERROR_WRONG_USERAGENT`.
  Send none and CapSkip uses its own browser's: copy `result.UserAgent` into
  your requests as it is.
- **Solve from the address you will use the cookie from.** If your requests go
  out through a proxy, pass that proxy with the task. If they come from the
  machine running CapSkip, pass none.
- **When CapSkip picks the proxy, it tells you which.** The CapSkip app has a
  DataDome proxy list of its own (Settings, Captcha types, DataDome, Configure
  Proxies). A task sent without a proxy is solved through that list, and the
  result then carries `Proxy` as `host:port`: send the cookie through that
  proxy, or it will be refused. A proxy you send always takes priority over
  the list, and then `Proxy` stays `null`.

```csharp
var handler = new HttpClientHandler { UseCookies = false };
if (result.Proxy != null)
{
    // The list's proxy, named without its login: add your credentials if it has one.
    handler.Proxy = new WebProxy($"http://{result.Proxy}");
}

using var http = new HttpClient(handler);
var request = new HttpRequestMessage(HttpMethod.Get, "https://www.example.com/");
request.Headers.TryAddWithoutValidation("User-Agent", result.UserAgent);
request.Headers.TryAddWithoutValidation("Cookie", result.Cookie);
var page = await http.SendAsync(request);
```

A block page whose `dd` object carries `'t':'bv'` means DataDome has banned the
address that fetched it; no solve from there can pass, so it is refused before
the request is sent. Change the address and request the page again.

### With a proxy (every method except image captcha)

```csharp
// Proxy is not supported for image captcha
await solver.RecaptchaAsync("...", "https://example.com", new Dictionary<string, object?>
{
    ["proxy"] = new Proxy("HTTPS", "user:pass@1.2.3.4:3128"),
});
await solver.TurnstileAsync("...", "https://example.com", new Dictionary<string, object?>
{
    ["proxy"] = new Proxy("HTTP", "1.2.3.4:3128"),
});
```

### Parallel solving

```csharp
using CapSkip;

var solver = new CapSkipClient();
var results = await Task.WhenAll(
    solver.RecaptchaAsync("...", "https://a.com"),
    solver.TurnstileAsync("...", "https://b.com"));

Console.WriteLine($"{results[0].Code} {results[1].Code}");
```

> `AsyncCapSkip` is provided as an alias of `CapSkipClient` — .NET I/O is asynchronous by nature, so every method already returns a `Task`. It exists for parity with the other CapSkip SDKs.

More examples: [`examples/`](examples/)

---

## Selenium, Playwright and PuppeteerSharp

The SDK hands back a token; your existing browser tooling does the driving. The shape is the same whichever you use:

1. Read the sitekey off the page (`data-sitekey`, or the widget's config object).
2. Call the matching solve method with that sitekey and the page URL.
3. Write the token into the response field and submit.

### Selenium WebDriver

```csharp
var sitekey = driver.FindElement(By.CssSelector("[data-sitekey]")).GetAttribute("data-sitekey");
var result = await solver.RecaptchaAsync(sitekey, driver.Url);

((IJavaScriptExecutor)driver).ExecuteScript(
    "document.getElementById('g-recaptcha-response').value = arguments[0];", result.Code);
driver.FindElement(By.CssSelector("form")).Submit();
```

### Playwright for .NET

```csharp
var sitekey = await page.GetAttributeAsync("[data-sitekey]", "data-sitekey");
var result = await solver.RecaptchaAsync(sitekey, page.Url);

await page.EvaluateAsync(
    "t => document.getElementById('g-recaptcha-response').value = t", result.Code);
```

PuppeteerSharp follows the same pattern with `page.EvaluateFunctionAsync`. Longer walkthroughs: [Selenium](https://capskip.com/selenium-captcha-solver/), [Playwright](https://capskip.com/playwright-captcha-solver/) and [Puppeteer](https://capskip.com/puppeteer-captcha-solver/).

---

## Return value

Every solve method resolves to a `SolveResult`:

```csharp
public sealed class SolveResult
{
    public string CaptchaId { get; }  // internal ID from CapSkip
    public string Code { get; }       // solution — text for image, token for reCAPTCHA/Turnstile
    public string? UserAgent { get; } // Turnstile, CaptchaFox & DataDome — the UA that minted the token
    public string? Challenge { get; } // GeeTest only — geetest_challenge
    public string? Validate  { get; } // GeeTest only — geetest_validate
    public string? Seccode   { get; } // GeeTest only — geetest_seccode
    public string? Token     { get; } // ALTCHA, CaptchaFox & Friendly Captcha — the payload to post back
    public long?   Number    { get; } // ALTCHA only — the counter that solved it
    public string? CaptchaKey   { get; } // Capy only — capy_captchakey
    public string? ChallengeKey { get; } // Capy only — capy_challengekey
    public string? Answer       { get; } // Capy only — capy_answer (the drag path)
    public string? RespKey      { get; } // Capy only — empty; carries nothing
    public string? CaptchaVoucher { get; } // AWS WAF only — captcha_voucher
    public string? ExistingToken  { get; } // AWS WAF only — existing_token (pairs with the voucher)
    public string? Cookie    { get; } // DataDome only — the datadome=... pair for a Cookie header
    public string? Proxy     { get; } // DataDome — host:port when CapSkip picked the proxy
}
```

---

## Error handling

All SDK exceptions derive from `CapSkipError`, so you can catch that one type — or handle each kind:

```csharp
using CapSkip;

try
{
    var result = await solver.RecaptchaAsync("...", "...");
}
catch (ValidationException)   { /* invalid parameters */ }
catch (NetworkException)      { /* CapSkip not running, or captcha not ready (manual polling) */ }
catch (ApiException)          { /* API returned an error code */ }
catch (CapSkip.TimeoutException) { /* polling timeout exceeded */ }
catch (CapSkipError)          { /* any other CapSkip failure */ }
```

> **Note:** `CapSkip.TimeoutException` and `CapSkip.ValidationException` share their short
> names with types in `System`. If you have both `using System;` and `using CapSkip;`,
> qualify them as `CapSkip.TimeoutException` / `CapSkip.ValidationException`, or just catch
> the base `CapSkipError`.

---

## FAQ

### How do I solve a captcha in C#?

Install the CapSkip desktop app, `dotnet add package CapSkip`, then await the method that matches the widget — `RecaptchaAsync()`, `TurnstileAsync()`, `GeetestAsync()`, `AltchaAsync()`, `CapyAsync()`, `CaptchaFoxAsync()`, `FriendlyCaptchaAsync()`, `AmazonWafAsync()`, `DataDomeAsync()` or `NormalAsync()`. Each returns a `SolveResult` whose `Code` is the token (a cookie for DataDome), or the recognized text in the case of an image captcha.

### Is this a free captcha solver?

The SDK itself is MIT-licensed and free. Solving needs the CapSkip app, which is bought once rather than metered per captcha, so your cost stops scaling with volume.

### Which captchas can it solve?

reCAPTCHA v2 (checkbox and invisible), reCAPTCHA v3, reCAPTCHA Enterprise, Cloudflare Turnstile, GeeTest v3, ALTCHA, Capy Puzzle, CaptchaFox, Friendly Captcha, AWS WAF, DataDome, and image/text captchas. Not hCaptcha, and not FunCaptcha/Arkose.

### Does it work with Selenium and Playwright?

Yes — see [above](#selenium-playwright-and-puppeteersharp). The SDK never touches a browser itself, so it drops into whatever stack you already have, PuppeteerSharp and plain `HttpClient` included.

### Which .NET versions are supported?

The package targets .NET Standard 2.0, which covers .NET 6 through 9+, .NET Core 2.0+, and .NET Framework 4.6.1+.

### Why is my reCAPTCHA v3 score low?

Google derives v3 scores from IP reputation, cookies and browsing history. A solver returns a valid token, but it cannot change how Google grades that token — `score` is forwarded as the target you want, not a guarantee. If a site enforces a high threshold, solve through a cleaner IP using the `proxy` option.

### Can I use it as a 2captcha alternative?

Yes. CapSkip serves the same endpoints, so you can either move to this SDK or repoint an existing 2captcha client at `127.0.0.1:8080`.

### Does the captcha have to be on a public page?

For widget captchas, yes — CapSkip loads the URL you pass it. Image captchas only need the image, and that can be a local file.

---

## Development

```bash
git clone https://github.com/capskip/capskip-dotnet.git
cd capskip-dotnet
dotnet test
```

The test suite mocks the HTTP layer and spins up a local mock server — no CapSkip app or network access required. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full development workflow.

---

## Links

- [CapSkip — local captcha solver](https://capskip.com) · [download the app](https://capskip.com/download/CapSkipInstaller.msi)
- [Captcha demo pages](https://capskip.com/captcha-demo/) — live reCAPTCHA, Turnstile, GeeTest and image widgets
- [C# captcha solver guide](https://capskip.com/csharp-captcha-solver/)
- [HTTP API docs](https://capskip.com/api-docs/)
- Other clients: [Python](https://github.com/capskip/capskip-python) · [Node.js](https://github.com/capskip/capskip-node) · [PHP](https://github.com/capskip/capskip-php) · [MCP server for AI agents](https://github.com/capskip/capskip-mcp)
- [NuGet package](https://www.nuget.org/packages/CapSkip/) · [report an issue](https://github.com/capskip/capskip-dotnet/issues)

---

## License

MIT — see [LICENSE](LICENSE).
