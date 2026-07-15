using System;
using System.IO;
using System.Threading.Tasks;

namespace CapSkip.Examples
{
    internal static class ImageCaptchaExample
    {
        public static async Task RunAsync()
        {
            var solver = ExampleConfig.CreateClient();

            // Sample captcha image shipped alongside this example — copied next to the
            // built assembly so it resolves no matter which directory you run from.
            var image = Path.Combine(AppContext.BaseDirectory, "captcha.png");

            var result = await solver.NormalAsync(image);

            Console.WriteLine($"Captcha ID: {result.CaptchaId}");
            Console.WriteLine($"Solution:   {result.Code}");
        }
    }
}
