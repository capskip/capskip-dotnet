using System.Collections.Generic;
using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>Unit tests for res.php response parsing (<c>ParsePollResponse</c>).</summary>
    public class PollParsingTests
    {
        [Theory]
        [InlineData(null, 0)]
        [InlineData(null, 1)]
        [InlineData("", 0)]
        [InlineData("", 1)]
        [InlineData("   ", 0)]
        [InlineData("   ", 1)]
        [InlineData("\n", 0)]
        [InlineData("\n", 1)]
        public void EmptyBodyIsRetryable(string? body, int jsonMode)
        {
            // CapSkip returns an empty body before a result is available; it must be
            // signalled as "not ready" (NetworkException), never a fatal ApiException.
            Assert.Throws<NetworkException>(() => ResponseParsing.ParsePollResponse(body, jsonMode));
        }

        [Fact]
        public void NotReadyMarker()
        {
            Assert.Throws<NetworkException>(() => ResponseParsing.ParsePollResponse("CAPCHA_NOT_READY"));
        }

        [Fact]
        public void OkToken()
        {
            Assert.Equal("thetoken", ResponseParsing.ParsePollResponse("OK|thetoken"));
        }

        [Fact]
        public void OkTokenIsStripped()
        {
            Assert.Equal("thetoken", ResponseParsing.ParsePollResponse("OK|thetoken\n"));
        }

        [Fact]
        public void UnrecognizedResponseThrows()
        {
            Assert.Throws<ApiException>(() => ResponseParsing.ParsePollResponse("SOMETHING_UNEXPECTED"));
        }

        [Fact]
        public void JsonReady()
        {
            var data = (IDictionary<string, object?>)ResponseParsing.ParsePollResponse(
                "{\"status\":1,\"request\":\"tok\"}", 1);
            Assert.Equal("tok", data["request"]);
        }

        [Fact]
        public void JsonNotReadyIsRetryable()
        {
            Assert.Throws<NetworkException>(() => ResponseParsing.ParsePollResponse(
                "{\"status\":0,\"request\":\"CAPCHA_NOT_READY\"}", 1));
        }
    }
}
