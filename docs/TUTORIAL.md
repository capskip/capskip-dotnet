# CapSkip .NET SDK — Complete Tutorial

This tutorial takes you from zero to solving every captcha type CapSkip supports.
Work through it top to bottom, or jump to the section you need.

**Contents**

1. [How it works](#1-how-it-works)
2. [Install and configure](#2-install-and-configure)
3. [Your first solve](#3-your-first-solve)
4. [Image captcha](#4-image-captcha)
5. [reCAPTCHA v2](#5-recaptcha-v2)
6. [reCAPTCHA v3](#6-recaptcha-v3)
7. [Cloudflare Turnstile](#7-cloudflare-turnstile)
8. [Using a proxy](#8-using-a-proxy)
9. [Concurrency](#9-concurrency)
10. [The manual workflow](#10-the-manual-workflow)
11. [Return values](#11-return-values)
12. [Error handling](#12-error-handling)
13. [End-to-end: solve and submit](#13-end-to-end-solve-and-submit)
14. [Parameter reference](#14-parameter-reference)
15. [Best practices](#15-best-practices)

---

## 1. How it works

CapSkip is a **local** application that solves captchas on your own machine and
exposes a standard captcha-solver HTTP API (documented in the [CapSkip API docs](https://capskip.com/api-docs/)):

```
POST http://<host>:<port>/in.php   → submit a captcha, returns  OK|<id>
GET  http://<host>:<port>/res.php  → poll for the answer, returns  OK|<solution>
```

This SDK is a thin, friendly wrapper around that API. Every solve follows the
same three steps, which the SDK does for you:

1. **Submit** the captcha (`in.php`) and receive a captcha ID.
2. **Poll** the result endpoint (`res.php`) while the answer is not ready.
3. **Return** the solution once CapSkip finishes.

You never have to write the polling loop yourself — call one method and `await`
the answer.

---

## 2. Install and configure

### Install CapSkip

Download and launch the CapSkip desktop app from [capskip.com](https://capskip.com),
and leave it running. In **Settings**, note the **API port** (default `8080`) and,
if API-key validation is enabled, your **API key**.

### Install the SDK

```bash
dotnet add package CapSkip
```

### Configure the client

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: "capskip",        // your CapSkip API key (any string if validation is off)
    host: "127.0.0.1",        // where CapSkip is listening
    port: 8080,               // API port from CapSkip settings
    defaultTimeout: 120,      // seconds to wait for an image captcha
    recaptchaTimeout: 300,    // seconds to wait for reCAPTCHA / Turnstile
    pollingInterval: 5);      // max seconds between result polls (starts at 0.25s, backs off to this)
```

In production, read configuration from the environment instead of hard-coding it:

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: Environment.GetEnvironmentVariable("CAPSKIP_API_KEY") ?? "capskip",
    host: Environment.GetEnvironmentVariable("CAPSKIP_HOST") ?? "127.0.0.1",
    port: int.TryParse(Environment.GetEnvironmentVariable("CAPSKIP_PORT"), out var p) ? p : 8080);
```

---

## 3. Your first solve

```csharp
using CapSkip;

var solver = new CapSkipClient(host: "127.0.0.1", port: 8080);

var result = await solver.RecaptchaAsync(
    "6Le-wvkS...your-sitekey",
    "https://example.com/page-with-recaptcha");

Console.WriteLine(result.Code);       // the g-recaptcha-response token
Console.WriteLine(result.CaptchaId);  // CapSkip's internal ID for this solve
```

`result` is always a `SolveResult`. The solution is in `result.Code`.

---

## 4. Image captcha

Use `solver.NormalAsync(...)` for classic distorted-text images. The SDK accepts
four input forms and auto-detects which one you passed:

```csharp
// 1. Local file path
var r1 = await solver.NormalAsync("captcha.png");

// 2. Remote image URL (the SDK downloads and encodes it)
var r2 = await solver.NormalAsync("https://example.com/captcha.jpg");

// 3. Base64 string (no file extension, longer than 50 characters)
var bytes = File.ReadAllBytes("captcha.png");
var r3 = await solver.NormalAsync(Convert.ToBase64String(bytes));

// 4. Data-URI
var r4 = await solver.NormalAsync("data:image/png;base64,iVBORw0KGgo...");

Console.WriteLine(r1.Code);   // the recognized text
```

Image captcha accepts only one extra option, `json`, which controls the raw
response format from CapSkip:

```csharp
var result = await solver.NormalAsync("captcha.png", new() { ["json"] = 1 });
```

> **Note:** Proxies are **not** supported for image captcha — passing one throws
> `ValidationException`. Proxies apply only to reCAPTCHA and Turnstile.

---

## 5. reCAPTCHA v2

`solver.RecaptchaAsync(sitekey, url)` handles reCAPTCHA v2 by default. The `sitekey`
is the `data-sitekey` attribute of the widget; `url` is the full page URL where it
appears.

```csharp
// Standard checkbox
var result = await solver.RecaptchaAsync(
    "6Le-wvkS...",
    "https://example.com/login");

// Invisible reCAPTCHA v2
await solver.RecaptchaAsync("6Le-wvkS...", "https://example.com", new() { ["invisible"] = 1 });

// Enterprise reCAPTCHA v2
await solver.RecaptchaAsync("6Le-wvkS...", "https://example.com", new() { ["enterprise"] = 1 });

// Enterprise with a data-s value (SDK alias: datas)
await solver.RecaptchaAsync("6Le-wvkS...", "https://example.com", new()
{
    ["enterprise"] = 1,
    ["datas"] = "Crb7Vs...",
});

Console.WriteLine(result.Code);   // g-recaptcha-response token
```

Do **not** pass `version`, `action`, or `score` to a v2 solve — those belong to v3
and will throw `ValidationException`.

---

## 6. reCAPTCHA v3

reCAPTCHA v3 is score-based. Pass `version = "v3"` plus the `action` your target page
uses and, optionally, a minimum score.

```csharp
var result = await solver.RecaptchaAsync("6Le-wvkS...", "https://example.com", new()
{
    ["version"] = "v3",
    ["action"] = "submit",   // must match the action in grecaptcha.execute()
    ["score"] = 0.7,         // SDK alias for min_score (0.1 – 0.9)
    ["enterprise"] = 0,      // set 1 for Enterprise v3
});

Console.WriteLine(result.Code);
```

`invisible` is a v2-only flag and is rejected for v3.

---

## 7. Cloudflare Turnstile

`solver.TurnstileAsync(sitekey, url)` solves Cloudflare Turnstile. The SDK
automatically requests the JSON response so it can return the **User-Agent**
Cloudflare expects.

```csharp
// Standalone widget
var result = await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com");
Console.WriteLine(result.Code);        // cf-turnstile-response token
Console.WriteLine(result.UserAgent);   // present when CapSkip returns it

// With an explicit action
await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com", new() { ["action"] = "login" });

// Cloudflare challenge page (needs cData and chlPageData from the page)
await solver.TurnstileAsync("0x4AAAAAAA...", "https://example.com", new()
{
    ["action"] = "managed",
    ["data"] = "your_cData_value",
    ["pagedata"] = "your_chlPageData_value",
});
```

> **Important:** For challenge pages you **must** send the returned token *and* use
> `result.UserAgent` as the `User-Agent` header when you submit it. Mismatched
> User-Agents are the most common reason a valid token gets rejected.

---

## 8. Using a proxy

Solving through the same IP you will submit from greatly improves acceptance rates
for reCAPTCHA and Turnstile. Pass the proxy as a `Proxy` object (or a dictionary
with `type` and `uri` keys):

```csharp
var proxy = new Proxy("HTTPS", "user:pass@1.2.3.4:3128");

await solver.RecaptchaAsync("...", "https://example.com", new() { ["proxy"] = proxy });
await solver.TurnstileAsync("...", "https://example.com", new() { ["proxy"] = proxy });
```

Supported proxy types: `HTTP`, `HTTPS`, `SOCKS5`, `SOCKS5H`. The `Uri` may include
credentials (`login:password@host:port`) or be a bare `host:port`.

---

## 9. Concurrency

Every solve method returns a `Task`, so you can solve many captchas at once with
`Task.WhenAll`:

```csharp
using CapSkip;

var solver = new CapSkipClient(host: "127.0.0.1", port: 8080);

var results = await Task.WhenAll(
    solver.RecaptchaAsync("...", "https://a.com"),
    solver.RecaptchaAsync("...", "https://b.com", new() { ["version"] = "v3", ["action"] = "submit" }),
    solver.TurnstileAsync("0x4A...", "https://c.com"));

Console.WriteLine($"{results[0].Code} {results[1].Code} {results[2].Code}");
```

`AsyncCapSkip` is an alias of `CapSkipClient`, offered for parity with the other
CapSkip SDKs — there is no separate synchronous client because .NET I/O is async
by nature.

---

## 10. The manual workflow

If you want to submit now and collect the answer later, use the two low-level steps
directly.

```csharp
using CapSkip;

// 1. Submit — returns the captcha ID immediately, without waiting.
var captchaId = await solver.SendAsync(new()
{
    ["method"] = "userrecaptcha",
    ["googlekey"] = "6Le-wvkS...",
    ["pageurl"] = "https://example.com",
});

// 2. Poll once. NetworkException means "not ready yet" — retry.
string code;
while (true)
{
    try
    {
        code = (string)await solver.GetResultAsync(captchaId);
        break;
    }
    catch (NetworkException)
    {
        await Task.Delay(5000);
    }
}

Console.WriteLine(code);
```

Pass `json: 1` to `GetResultAsync` to get the full object (including `useragent` for
Turnstile) instead of a plain string; it comes back as a
`Dictionary<string, object?>`.

---

## 11. Return values

Every high-level solve method (`NormalAsync`, `RecaptchaAsync`, `TurnstileAsync`,
`SolveAsync`) returns a `SolveResult`:

```csharp
public sealed class SolveResult
{
    public string CaptchaId { get; }   // CapSkip's internal ID for this solve
    public string Code { get; }        // the solution: text for images, token otherwise
    public string? UserAgent { get; }  // Turnstile only, when CapSkip provides it
}
```

`SendAsync()` returns just the ID string. `GetResultAsync()` returns the solution
string (or a `Dictionary<string, object?>` when `json: 1`).

---

## 12. Error handling

The SDK throws four exception types, all subclasses of `CapSkipError`:

| Exception | When it is thrown |
|---|---|
| `ValidationException` | Invalid or unsupported parameters (e.g. proxy on image captcha) |
| `NetworkException` | CapSkip is unreachable, or the captcha is not ready yet |
| `ApiException` | CapSkip returned an error code (e.g. `ERROR_WRONG_USER_KEY`) |
| `TimeoutException` | Polling exceeded the configured timeout |

```csharp
using CapSkip;

var solver = new CapSkipClient(host: "127.0.0.1", port: 8080);

try
{
    var result = await solver.RecaptchaAsync("...", "https://example.com");
    Console.WriteLine(result.Code);
}
catch (ValidationException e)      { Console.WriteLine($"Bad parameters: {e.Message}"); }
catch (NetworkException e)         { Console.WriteLine($"Is CapSkip running? {e.Message}"); }
catch (ApiException e)             { Console.WriteLine($"CapSkip returned an error: {e.Message}"); }
catch (CapSkip.TimeoutException e) { Console.WriteLine($"Gave up waiting: {e.Message}"); }
```

You can also catch them all at once with the base class:

```csharp
try
{
    var result = await solver.TurnstileAsync("...", "...");
}
catch (CapSkipError e)
{
    Console.WriteLine($"Solve failed: {e.Message}");
}
```

> **Note:** `CapSkip.TimeoutException` and `CapSkip.ValidationException` share their
> short names with `System.TimeoutException` / `System.ComponentModel.DataAnnotations.ValidationException`.
> With both `using System;` and `using CapSkip;` in scope, qualify them as
> `CapSkip.TimeoutException` (as above) or catch the base `CapSkipError`.

---

## 13. End-to-end: solve and submit

A realistic flow — solve a reCAPTCHA, then submit the token to the target site
through the **same** proxy:

```csharp
using System.Net;
using System.Net.Http;
using CapSkip;

var solver = new CapSkipClient(
    apiKey: Environment.GetEnvironmentVariable("CAPSKIP_API_KEY") ?? "capskip",
    host: "127.0.0.1",
    port: 8080);

const string sitekey = "6Le-wvkS...your-sitekey";
const string loginUrl = "https://example.com/login";
var proxy = new Proxy("HTTP", "1.2.3.4:3128");

SolveResult solved;
try
{
    solved = await solver.RecaptchaAsync(sitekey, loginUrl, new() { ["proxy"] = proxy });
}
catch (CapSkipError e)
{
    Console.WriteLine($"Could not solve captcha: {e.Message}");
    return;
}

var token = solved.Code;

// Submit the form using the same proxy so the IP matches.
var handler = new HttpClientHandler { Proxy = new WebProxy($"http://{proxy.Uri}"), UseProxy = true };
using var http = new HttpClient(handler);
var response = await http.PostAsync(loginUrl, new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["username"] = "myuser",
    ["password"] = "mypass",
    ["g-recaptcha-response"] = token,
}));

Console.WriteLine((int)response.StatusCode);
```

For Turnstile challenge pages, also set the User-Agent header:

```csharp
var solved = await solver.TurnstileAsync("0x4A...", challengeUrl, new()
{
    ["data"] = "cData",
    ["pagedata"] = "chlPageData",
});

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("User-Agent", solved.UserAgent);
await http.PostAsync(challengeUrl, new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["cf-turnstile-response"] = solved.Code,
}));
```

---

## 14. Parameter reference

### Solve methods

| Method | Signature |
|---|---|
| Image | `NormalAsync(file, options?)` |
| reCAPTCHA | `RecaptchaAsync(sitekey, url, options?)` |
| Turnstile | `TurnstileAsync(sitekey, url, options?)` |
| Manual submit | `SendAsync(parameters) → id` |
| Manual poll | `GetResultAsync(id, json = 0)` |

`options` is an `IDictionary<string, object?>` — build it with a collection
initializer: `new() { ["version"] = "v3" }`.

### Convenience aliases

The SDK accepts friendly names and converts them to the raw API parameters:

| SDK name | CapSkip API parameter |
|---|---|
| `url` | `pageurl` |
| `score`, `minScore` | `min_score` |
| `datas`, `data_s` | `data-s` |
| `proxy` (`Proxy` or dictionary) | `proxy` + `proxytype` strings |

Anything CapSkip does not document for a given captcha type is rejected with
`ValidationException`, so typos fail fast instead of silently doing nothing.

---

## 15. Best practices

- **Keep CapSkip running.** The SDK talks to a local app; if it is not running you
  get `NetworkException`.
- **Use the token immediately.** reCAPTCHA and Turnstile tokens expire within a
  couple of minutes.
- **Match sitekey and pageurl exactly** to the page the widget loads on.
- **Solve and submit from the same IP** (same proxy) for reCAPTCHA and Turnstile.
- **Never commit secrets.** Read `CAPSKIP_API_KEY` and proxy credentials from the
  environment, not source code.
- **Tune timeouts** for slow captcha types with `recaptchaTimeout` and
  `defaultTimeout`.

---

### Where to go next

- [API Reference](API_REFERENCE.md) — every method, parameter, and endpoint
- [Getting Started](GETTING_STARTED.md) — installation walkthrough
- [Troubleshooting](TROUBLESHOOTING.md) — fixes for common errors
- [CapSkip API docs](https://capskip.com/api-docs/) — the raw HTTP API
