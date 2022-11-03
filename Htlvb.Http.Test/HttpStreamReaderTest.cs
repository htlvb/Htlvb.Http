using System.Collections.Generic;
using System.IO;
using System.Text;
using Xunit;

namespace Htlvb.Http.Test
{
    public class HttpStreamReaderTest
    {
        [Fact]
        public void ReadWholeLineAtOnce()
        {
            using var stream = CreateStreamFromText("Single line\r\n", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var actual = streamReader.ReadLine();
            Assert.Equal("Single line", actual);
        }

        [Fact]
        public void ReadLineWithEndOfStream()
        {
            using var stream = CreateStreamFromText("Single line", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var actual = streamReader.ReadLine();
            Assert.Equal("Single line", actual);
        }

        [Fact]
        public void ReadLineAfterEndOfStream()
        {
            using var stream = CreateStreamFromText("Single line", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            streamReader.ReadLine();
            var actual = streamReader.ReadLine();
            Assert.Null(actual);
        }

        [Fact]
        public void ReadEmptyLine()
        {
            using var stream = CreateStreamFromText("\r\n", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var actual = streamReader.ReadLine();
            Assert.Equal("", actual);
        }

        [Fact]
        public void ReadLineWithMultipleIterations()
        {
            using var stream = CreateStreamFromText("Single line\r\n", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 3, 3);
            var actual = streamReader.ReadLine();
            Assert.Equal("Single line", actual);
        }

        public static IEnumerable<object[]> ReadMultipleLines_BufferSizes
        {
            get
            {
                for (int size = 2; size < 20; size++)
                {
                    yield return new object[] { size, size };
                }
            }
        }

        [Theory]
        [MemberData(nameof(ReadMultipleLines_BufferSizes))]
        public void ReadMultipleLines(int byteBufferSize, int charBufferSize)
        {
            using var stream = CreateStreamFromText("First line\r\nSecond line\r\nThird line\r\nThis is the fourth and last line\r\n", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, byteBufferSize, charBufferSize);
            var actual = new[]
            {
                streamReader.ReadLine(),
                streamReader.ReadLine(),
                streamReader.ReadLine(),
                streamReader.ReadLine()
            };
            var expected = new[]
            {
                "First line",
                "Second line",
                "Third line",
                "This is the fourth and last line"
            };
            Assert.Equal(expected, actual);
        }

        [Fact]
        public void ReadUnicodeText()
        {
            using MemoryStream stream = new(new byte[] { (byte)'a', 0xF0, 0x9F, 0x9A, 0x97, (byte)'b' });
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var actual = streamReader.ReadBytesAsText(1);
            streamReader.Encoding = Encoding.UTF8;
            actual += streamReader.ReadBytesAsText(4);
            streamReader.Encoding = Encoding.ASCII;
            actual += streamReader.ReadBytesAsText(1);
            Assert.Equal("a🚗b", actual);
        }

        [Fact]
        public void ReadBytesAlreadyInBuffer()
        {
            using var stream = CreateStreamFromText("First line\r\nSecond line", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var firstLine = streamReader.ReadLine();
            var secondLine = streamReader.ReadBytesAsText(2);
            secondLine += streamReader.ReadBytesAsText(4);
            secondLine += streamReader.ReadBytesAsText(2);
            Assert.Equal("Second l", secondLine);
        }

        [Fact]
        public void ReadMoreBytesThanAvailable()
        {
            using var stream = CreateStreamFromText("First line\r\nSecond line", Encoding.ASCII);
            HttpStreamReader streamReader = new(stream, Encoding.ASCII, 16, 16);
            var firstLine = streamReader.ReadLine();
            var secondLine = streamReader.ReadBytesAsText(2);
            secondLine += streamReader.ReadBytesAsText(4);
            secondLine += streamReader.ReadBytesAsText(6);
            Assert.Equal("Second line", secondLine);
        }

        private static Stream CreateStreamFromText(string text, Encoding encoding)
        {
            return new MemoryStream(encoding.GetBytes(text));
        }
    }
}
