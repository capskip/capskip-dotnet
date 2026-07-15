# CapSkip .NET SDK

[![.NET Standard 2.0](https://img.shields.io/badge/.NET-Standard%202.0-blueviolet.svg)](https://learn.microsoft.com/dotnet/standard/net-standard)
[![License: MIT](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Tests](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/capskip/capskip-dotnet/actions/workflows/ci.yml)

Official .NET / C# client for the [CapSkip](https://capskip.com) **local** captcha solver.

CapSkip runs on your machine and exposes a standard captcha-solver HTTP API (the familiar `in.php` / `res.php` endpoints). This SDK wraps that API with clean, familiar method names, so you can solve captchas locally — no cloud service and no per-solve API fees beyond your CapSkip license.

Targets **.NET Standard 2.0**, so it runs on .NET 6/7/8/9+, .NET Core 2.0+, and .NET Framework 4.6.1+.

---

## Quick start (5 minutes)

### 1. Install CapSkip

Download and run the CapSkip desktop app from [capskip.com](https://capskip.com). Leave it running in the background.

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

## Supported captcha types

| Type | SDK method |
|---|---|
| Image CAPTCHA (distorted text) | `solver.NormalAsync(file)` |
| reCAPTCHA v2 (checkbox) | `solver.RecaptchaAsync(sitekey, url)` |
| reCAPTCHA v2 Invisible | `solver.RecaptchaAsync(sitekey, url, new() { ["invisible"] = 1 })` |
| reCAPTCHA v2 Enterprise | `solver.RecaptchaAsync(sitekey, url, new() { ["enterprise"] = 1 })` |
| reCAPTCHA v3 | `solver.RecaptchaAsync(sitekey, url, new() { ["version"] = "v3" })` |
| reCAPTCHA v3 Enterprise | `solver.RecaptchaAsync(sitekey, url, new() { ["version"] = "v3", ["enterprise"] = 1 })` |
| Cloudflare Turnstile (widget) | `solver.TurnstileAsync(sitekey, url)` |
| Cloudflare Turnstile (challenge page) | `solver.TurnstileAsync(sitekey, url, new() { ["data"] = ..., ["pagedata"] = ... })` |

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
    recaptchaTimeout: 300,    // seconds — reCAPTCHA / Turnstile polling timeout
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
var v3 = await solver.RecaptchaAsync("...", "https://example.com", new()
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

### With a proxy (reCAPTCHA & Turnstile only)

```csharp
// Proxy is not supported for image captcha
await solver.RecaptchaAsync("...", "https://example.com", new()
{
    ["proxy"] = new Proxy("HTTPS", "user:pass@1.2.3.4:3128"),
});
await solver.TurnstileAsync("...", "https://example.com", new()
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

## Return value

Every solve method resolves to a `SolveResult`:

```csharp
public sealed class SolveResult
{
    public string CaptchaId { get; }  // internal ID from CapSkip
    public string Code { get; }       // solution — text for image, token for reCAPTCHA/Turnstile
    public string? UserAgent { get; } // Turnstile only — use when submitting challenge-page tokens
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

## Development

```bash
git clone https://github.com/capskip/capskip-dotnet.git
cd capskip-dotnet
dotnet test
```

The test suite mocks the HTTP layer and spins up a local mock server — no CapSkip app or network access required. See [CONTRIBUTING.md](CONTRIBUTING.md) for the full development workflow.

---

## Links

- [CapSkip website](https://capskip.com)
- [CapSkip API docs](https://capskip.com/api-docs/)
- [Report an issue](https://github.com/capskip/capskip-dotnet/issues)
- [NuGet package](https://www.nuget.org/packages/CapSkip/)

---

## License

MIT — see [LICENSE](LICENSE).
