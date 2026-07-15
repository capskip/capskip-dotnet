using CapSkip;
using Xunit;

namespace CapSkip.Tests
{
    /// <summary>Unit tests for in.php submit-response parsing (<c>ParseSubmitResponse</c>).</summary>
    public class SubmitParsingTests
    {
        [Fact]
        public void OkForm()
        {
            Assert.Equal("12345", ResponseParsing.ParseSubmitResponse("OK|12345"));
        }

        [Fact]
        public void OkFormIsStripped()
        {
            Assert.Equal("12345", ResponseParsing.ParseSubmitResponse("OK|12345\n"));
        }

        [Fact]
        public void JsonForm()
        {
            // CapSkip returns this shape when the submit carried json=1.
            Assert.Equal("12345", ResponseParsing.ParseSubmitResponse("{\"status\":1,\"request\":\"12345\"}"));
        }

        [Fact]
        public void UnrecognizedResponseThrows()
        {
            Assert.Throws<ApiException>(() => ResponseParsing.ParseSubmitResponse("SOMETHING_UNEXPECTED"));
        }

        [Fact]
        public void JsonWithoutSuccessStatusThrows()
        {
            Assert.Throws<ApiException>(() => ResponseParsing.ParseSubmitResponse("{\"status\":0,\"request\":\"NOPE\"}"));
        }
    }
}
