using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CapSkip;

namespace CapSkip.Examples
{
    /// <summary>Verify CapSkip is running and reachable.</summary>
    internal static class VerifyConnectionExample
    {
        public static async Task RunAsync(string[] args)
        {
            var host = GetOption(args, "--host") ?? ExampleConfig.Host;
            var port = int.TryParse(GetOption(args, "--port"), out var parsed) ? parsed : ExampleConfig.Port;
            var apiKey = GetOption(args, "--api-key") ?? ExampleConfig.ApiKey;

            var client = new ApiClient(host, port);
            Console.WriteLine($"CapSkip SDK : {CapSkipClient.Version}");
            Console.WriteLine($"Target      : {client.BaseUrl}");

            try
            {
                await client.ResAsync(new Dictionary<string, object?>
                {
                    ["key"] = apiKey,
                    ["action"] = "get",
                    ["id"] = "0",
                });
                Console.WriteLine("Status      : OK — CapSkip is reachable");
            }
            catch (NetworkException e)
            {
                Console.WriteLine($"Status      : FAILED — {e.Message}");
                Environment.ExitCode = 1;
            }
            catch (ApiException e)
            {
                Console.WriteLine("Status      : OK — CapSkip is reachable");
                Console.WriteLine($"Response    : {e.Message}");
            }

            Console.WriteLine("Try: dotnet run -- recaptcha");
        }

        private static string? GetOption(string[] args, string name)
        {
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (args[i] == name)
                {
                    return args[i + 1];
                }
            }

            return null;
        }
    }
}
