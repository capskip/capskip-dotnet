# Getting Started

This guide walks you through installing CapSkip, installing the .NET SDK, and running your first captcha solve.

---

## Prerequisites

| Requirement | Details |
|---|---|
| **CapSkip app** | Windows desktop app from [capskip.com](https://capskip.com) |
| **.NET** | .NET 6 or newer (the SDK targets .NET Standard 2.0, so .NET Framework 4.6.1+ works too) |
| **Network** | SDK talks to CapSkip on `localhost` — no internet required for the API itself |

---

## Step 1 — Install and configure CapSkip

1. Download CapSkip from [capskip.com](https://capskip.com).
2. Install and launch the application.
3. Open **Settings** and confirm:
   - **Port** — default is `8080` (remember this value)
   - **API key validation** — if enabled, copy your API key; if disabled, any string (e.g. `capskip`) is accepted

CapSkip exposes a standard captcha-solver API:

```
POST http://127.0.0.1:<port>/in.php   → submit captcha, returns OK|<id>
GET  http://127.0.0.1:<port>/res.php  → poll result, returns OK|<answer>
```

### Verify CapSkip is running

**Windows (PowerShell):**

```powershell
Invoke-WebRequest "http://127.0.0.1:8080/res.php?key=capskip&action=get&id=0" -UseBasicParsing
```

You should get a response (even an error like `ERROR_WRONG_CAPTCHA_ID` confirms the server is up).

**Linux / macOS:**

```bash
curl "http://127.0.0.1:8080/res.php?key=capskip&action=get&id=0"
```

---

## Step 2 — Install the .NET SDK

### From NuGet (recommended)

```bash
dotnet add package CapSkip
```

Or with the Package Manager Console in Visual Studio:

```powershell
Install-Package CapSkip
```

### Verify CapSkip connectivity

The bundled example project includes a connectivity check:

```bash
dotnet run --project examples/CapSkip.Examples -- verify
```

If CapSkip is running, you should see `Status: OK — CapSkip is reachable`.

---

## Step 3 — Create your first program

Create a new console app and add the package:

```bash
dotnet new console -n CapSkipDemo
cd CapSkipDemo
dotnet add package CapSkip
```

Replace `Program.cs` with:

```csharp
using CapSkip;

var solver = new CapSkipClient(
    apiKey: Environment.GetEnvironmentVariable("CAPSKIP_API_KEY") ?? "capskip",
    host: Environment.GetEnvironmentVariable("CAPSKIP_HOST") ?? "127.0.0.1",
    port: int.TryParse(Environment.GetEnvironmentVariable("CAPSKIP_PORT"), out var p) ? p : 8080);

const string sitekey = "6Le-wvkSAAAAAPBMRTvw0Q4Muexq9bi0DJwx_mJ-"; // replace with your target page sitekey
const string pageUrl = "https://example.com/login";                 // replace with your target page URL

try
{
    var result = await solver.RecaptchaAsync(sitekey, pageUrl);
    Console.WriteLine($"Captcha ID: {result.CaptchaId}");
    Console.WriteLine($"Token:      {result.Code[..Math.Min(80, result.Code.Length)]}...");
}
catch (NetworkException e)
{
    Console.WriteLine($"Cannot reach CapSkip — is the app running? {e.Message}");
}
catch (CapSkip.TimeoutException e)
{
    Console.WriteLine($"Timed out: {e.Message}");
}
```

Run it:

```bash
dotnet run
```

---

## Step 4 — Run the bundled examples

Clone the repository (if you haven't already) and run an example:

```bash
git clone https://github.com/capskip/capskip-dotnet.git
cd capskip-dotnet
dotnet run --project examples/CapSkip.Examples -- recaptcha
```

| Example (`-- <name>`) | What it demonstrates |
|---|---|
| `image` | Image captcha from the bundled `captcha.png` |
| `recaptcha` | reCAPTCHA v2 (Google's public test key) |
| `turnstile` | Cloudflare Turnstile widget |
| `geetest` | GeeTest v3 slider, including fetching a fresh `gt`/`challenge` pair |
| `async` | Parallel concurrent solving |
| `verify` | Check CapSkip is running |

---

## Environment variables

| Variable | Default | Description |
|---|---|---|
| `CAPSKIP_API_KEY` | `capskip` | API key sent with every request |
| `CAPSKIP_HOST` | `127.0.0.1` | CapSkip host |
| `CAPSKIP_PORT` | `8080` | CapSkip port |

---

## Next steps

- [Tutorial](TUTORIAL.md) — complete walkthrough of every captcha type
- [API Reference](API_REFERENCE.md) — all methods and parameters
- [Troubleshooting](TROUBLESHOOTING.md) — fix common errors
- [CapSkip API docs](https://capskip.com/api-docs/) — raw HTTP API reference
