# Contributing to CapSkip .NET SDK

Thank you for helping improve the CapSkip .NET SDK. This document explains how to set up your environment, run tests, and submit changes.

---

## Prerequisites

- .NET SDK 8.0 or newer (the library targets .NET Standard 2.0)
- Git
- CapSkip desktop app (for integration testing against a live instance)

---

## Development setup

```bash
# Clone the repository
git clone https://github.com/capskip/capskip-dotnet.git
cd capskip-dotnet

# Restore and build the whole solution
dotnet build
```

---

## Running tests

Tests use xUnit and mock the HTTP layer (plus a local in-process mock server) — CapSkip does not need to be running.

```bash
# Run all tests
dotnet test

# Verbose output
dotnet test -v normal
```

### Test structure

| File | Description |
|---|---|
| `tests/CapSkip.Tests/MockApiClient.cs` | Mock `ApiClient` + shared assertions |
| `tests/CapSkip.Tests/MockServer.cs` | Local mock CapSkip server (raw `TcpListener`) |
| `tests/CapSkip.Tests/NormalTests.cs` | Image captcha unit tests |
| `tests/CapSkip.Tests/RecaptchaTests.cs` | reCAPTCHA unit tests |
| `tests/CapSkip.Tests/TurnstileTests.cs` | Turnstile unit tests |
| `tests/CapSkip.Tests/SubmitParsingTests.cs` | `in.php` submit-response parsing |
| `tests/CapSkip.Tests/PollParsingTests.cs` | `res.php` response parsing |
| `tests/CapSkip.Tests/IntegrationTests.cs` | End-to-end tests driving the real HTTP layer |

Unit tests verify that SDK methods send the correct parameters to the CapSkip API.
Integration tests spin up a local mock server and exercise the full submit/poll
round trip — no CapSkip app or network access required.

---

## Running the examples

```bash
dotnet run --project examples/CapSkip.Examples -- recaptcha
dotnet run --project examples/CapSkip.Examples -- verify
```

(Requires the CapSkip desktop app to be running.)

---

## Code style

- Match the existing code style in `src/CapSkip/`
- Keep changes focused — one feature or fix per pull request
- Add or update tests for any behavior change
- Update documentation in `docs/` and `README.md` when adding features
- Keep the runtime dependency surface minimal (the library only relies on the BCL and `System.Text.Json`)

---

## Pull request process

1. Fork the repository and create a feature branch:

   ```bash
   git checkout -b feature/my-improvement
   ```

2. Make your changes and ensure tests pass:

   ```bash
   dotnet test
   ```

3. Update `CHANGELOG.md` under `[Unreleased]` if applicable.

4. Push and open a pull request against `main`.

5. Fill in the pull request template completely.

---

## Reporting bugs

Use the [Bug Report issue template](.github/ISSUE_TEMPLATE/bug_report.yml) and include:

- .NET version and target framework
- SDK version
- CapSkip port and captcha type
- Minimal reproduction steps
- Full error / stack trace (redact secrets)

---

## Feature requests

CapSkip only supports: **image captcha**, **reCAPTCHA v2/v3**, and **Cloudflare Turnstile**.

Before requesting a new captcha type, confirm it is supported by [CapSkip API docs](https://capskip.com/api-docs/). Use the [Feature Request template](.github/ISSUE_TEMPLATE/feature_request.yml) for SDK improvements.

---

## Project structure

```
src/CapSkip/          # Package source
examples/             # Runnable example programs
tests/                # Unit and integration tests
docs/                 # Documentation
.github/              # GitHub Actions and templates
```

---

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
