# CapSkip .NET SDK — Captcha Solver for C# and .NET

[![NuGet](https://img.shields.io/nuget/v/CapSkip.svg)](https://www.nuget.org/packages/CapSkip/)
[![.NET Standard 2.0](https://img.shields.io/badge/.NET-Standard%202.0-blueviolet.svg)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml)

**Solve reCAPTCHA v2, reCAPTCHA v3, Cloudflare Turnstile, GeeTest and image captchas from C#.**

Official .NET client for [CapSkip](https://capskip.com), a **local captcha solver** that runs on your own machine. Licensed once, not billed per solve.

```bash
dotnet add package CapSkip
```

Targets **.NET Standard 2.0**, so it runs on .NET 6/7/8/9+, .NET Core 2.0+, and .NET Framework 4.6.1+.

---

## How it works

CapSkip is a desktop app. It does the solving on your machine and exposes the standard captcha-solver HTTP API — the same `in.php` / `res.php` endpoints every 2captcha-compatible client already speaks — on `127.0.0.1:8080`.

This SDK is a thin wrapper over that API, with the method names you would expect: `NormalAsync()`, `RecaptchaAsync()`, `TurnstileAsync()`, `GeetestAsync()`. Nothing leaves your network, and there is no credit balance to keep an eye on.

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

The options argument is a `Dictionary<string, object?>` — the table shortens it to `new() { ... }` to stay readable; full signatures are in the [API Reference](docs/API_REFERENCE.md).

**hCaptcha and FunCaptcha/Arkose are not supported.** hCaptcha is the one people misidentify most often, since it also puts a `data-sitekey` on the widget — check for `class="h-captcha"` or a `js.hcaptcha.com` script before reaching for `RecaptchaAsync()`.

Point the SDK at the [live captcha demo pages](https://capskip.com/captcha-demo/) to sanity-check your setup against real widgets.

---

## Quick start (5 minutes)

### 1. Install the CapSkip captcha solver

Download and run the CapSkip desktop app from [capskip.com](https://capskip.com/download/). Leave it running in the background.

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
    recaptchaTimeout: 300,    // seconds — reCAPTCHA / Turnstile / GeeTest polling timeout
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

### With a proxy (reCAPTCHA, Turnstile & GeeTest only)

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
    public string? UserAgent { get; } // Turnstile only — use when submitting challenge-page tokens
    public string? Challenge { get; } // GeeTest only — geetest_challenge
    public string? Validate  { get; } // GeeTest only — geetest_validate
    public string? Seccode   { get; } // GeeTest only — geetest_seccode
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

Install the CapSkip desktop app, `dotnet add package CapSkip`, then await the method that matches the widget — `RecaptchaAsync()`, `TurnstileAsync()`, `GeetestAsync()` or `NormalAsync()`. Each returns a `SolveResult` whose `Code` is the token, or the recognized text in the case of an image captcha.

### Is this a free captcha solver?

The SDK itself is MIT-licensed and free. Solving needs the CapSkip app, which is bought once rather than metered per captcha, so your cost stops scaling with volume.

### Which captchas can it solve?

reCAPTCHA v2 (checkbox and invisible), reCAPTCHA v3, reCAPTCHA Enterprise, Cloudflare Turnstile, GeeTest v3, and image/text captchas. Not hCaptcha, and not FunCaptcha/Arkose.

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

- [CapSkip — local captcha solver](https://capskip.com) · [download](https://capskip.com/download/)
- [Captcha demo pages](https://capskip.com/captcha-demo/) — live reCAPTCHA, Turnstile, GeeTest and image widgets
- [C# captcha solver guide](https://capskip.com/csharp-captcha-solver/)
- [HTTP API docs](https://capskip.com/api-docs/)
- Other clients: [Python](https://github.com/capskip/capskip-python) · [Node.js](https://github.com/capskip/capskip-node) · [PHP](https://github.com/capskip/capskip-php) · [MCP server for AI agents](https://github.com/capskip/capskip-mcp)
- [NuGet package](https://www.nuget.org/packages/CapSkip/) · [report an issue](https://github.com/capskip/capskip-dotnet/issues)

---

## License

MIT — see [LICENSE](LICENSE).
