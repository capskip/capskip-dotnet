using System.Collections.Generic;
using System.Threading.Tasks;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    public class NormalTests
    {
        [Fact]
        public async Task Base64()
        {
            var (solver, api) = MockSolver.Make();
            var body = new string('A', 60);

            var result = await solver.NormalAsync(body);

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "base64",
                ["body"] = body,
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task DataUri()
        {
            var (solver, api) = MockSolver.Make();
            var body = new string('A', 60);

            var result = await solver.NormalAsync("data:image/png;base64," + body);

            MockSolver.AssertSent(api, new Dictionary<string, object?>
            {
                ["method"] = "base64",
                ["body"] = body,
            });
            MockSolver.AssertResult(result);
        }

        [Fact]
        public async Task InvalidFileThrows()
        {
            var (solver, _) = MockSolver.Make();
            await Assert.ThrowsAnyAsync<CapSkipError>(() => solver.NormalAsync("lost_file.png"));
        }

        [Fact]
        public async Task RejectsUnsupportedParams()
        {
            var (solver, _) = MockSolver.Make();
            await Assert.ThrowsAnyAsync<CapSkipError>(() =>
                solver.NormalAsync(new string('A', 60), new Dictionary<string, object?> { ["numeric"] = 1 }));
        }

        [Fact]
        public async Task RejectsProxy()
        {
            var (solver, _) = MockSolver.Make();
            await Assert.ThrowsAnyAsync<CapSkipError>(() =>
                solver.NormalAsync(new string('A', 60), new Dictionary<string, object?>
                {
                    ["proxy"] = new Proxy("HTTP", "1.2.3.4:3128"),
                }));
        }
    }
}
