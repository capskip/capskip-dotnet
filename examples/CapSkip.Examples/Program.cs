using System;
using System.Threading.Tasks;
using CapSkip.Examples;

var command = args.Length > 0 ? args[0].ToLowerInvariant() : "help";
var rest = args.Length > 1 ? args[1..] : Array.Empty<string>();

switch (command)
{
    case "image":
        await ImageCaptchaExample.RunAsync();
        break;
    case "recaptcha":
        await RecaptchaExample.RunAsync();
        break;
    case "turnstile":
        await TurnstileExample.RunAsync();
        break;
    case "geetest":
        await GeetestExample.RunAsync();
        break;
    case "async":
        await AsyncExample.RunAsync();
        break;
    case "verify":
        await VerifyConnectionExample.RunAsync(rest);
        break;
    default:
        Console.WriteLine("CapSkip examples — usage: dotnet run -- <example>");
        Console.WriteLine();
        Console.WriteLine("  image      Solve the bundled image captcha (captcha.png)");
        Console.WriteLine("  recaptcha  Solve a reCAPTCHA v2 (Google's public test key)");
        Console.WriteLine("  turnstile  Solve a Cloudflare Turnstile (public test key)");
        Console.WriteLine("  geetest    Solve a GeeTest v3 slider (fetches a fresh gt/challenge)");
        Console.WriteLine("  async      Solve several captchas concurrently");
        Console.WriteLine("  verify     Check CapSkip is running (accepts --host --port --api-key)");
        Console.WriteLine();
        Console.WriteLine("Configure via env vars: CAPSKIP_API_KEY, CAPSKIP_HOST, CAPSKIP_PORT");
        break;
}
