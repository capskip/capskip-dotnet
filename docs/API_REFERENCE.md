# CapSkip API Reference

Complete reference aligned with the [official CapSkip API](https://capskip.com/api-docs/).

CapSkip exposes a standard captcha-solver HTTP API on your local machine:

```
POST http://<host>:<port>/in.php   → submit captcha
GET  http://<host>:<port>/res.php  → poll result
```

The SDK only supports the four captcha types documented by CapSkip.

---

## Supported captcha types

| Type | SDK method | `method` (POST) |
|---|---|---|
| Image captcha | `NormalAsync()` | `post` or `base64` |
| reCAPTCHA v2 | `RecaptchaAsync(..., version="v2")` | `userrecaptcha` |
| reCAPTCHA v3 | `RecaptchaAsync(..., version="v3")` | `userrecaptcha` + `version=v3` |
| Cloudflare Turnstile | `TurnstileAsync()` | `turnstile` |
| GeeTest v3 (slide) | `GeetestAsync()` | `geetest` |

**Proxy** is supported for reCAPTCHA, Turnstile, and GeeTest — not for image captcha.

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
| `recaptchaTimeout` | `double` | `300` | Seconds to poll reCAPTCHA / Turnstile |
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

## Return value

Every solve method returns a `SolveResult`:

```csharp
public sealed class SolveResult
{
    public string CaptchaId { get; }
    public string Code { get; }
    public string? UserAgent { get; }  // Turnstile only, when the json=1 poll includes it
}
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
