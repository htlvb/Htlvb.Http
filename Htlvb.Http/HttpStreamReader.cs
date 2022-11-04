using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Htlvb.Http
{
    public class HttpStreamReader
    {
        private readonly Stream stream;
        private Encoding encoding;
        private Decoder decoder;
        private readonly byte[] byteBuffer;
        private int byteBufferEnd;
        private readonly char[] charBuffer;
        private int charBufferEnd;

        public HttpStreamReader(Stream stream) : this(stream, Encoding.ASCII, 1024, 1024)
        {
        }

        internal HttpStreamReader(Stream stream, Encoding encoding, int byteBufferSize, int charBufferSize)
        {
            this.stream = stream ?? throw new ArgumentNullException(nameof(stream));
            if (byteBufferSize < 2)
            {
                throw new ArgumentException("Byte buffer size must be >= 2");
            }
            byteBuffer = new byte[byteBufferSize];
            if (charBufferSize < 1)
            {
                throw new ArgumentException("Char buffer size must be >= 1");
            }
            charBuffer = new char[charBufferSize];
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
        }

        public Encoding Encoding
        {
            get
            {
                return encoding;
            }
            set
            {
                encoding = value;
                decoder = encoding.GetDecoder();
                decoder.Convert(byteBuffer, 0, byteBufferEnd, charBuffer, 0, charBuffer.Length, false, out var bytesConverted, out charBufferEnd, out var completed);
            }
        }

        public string ReadLine()
        {
            StringBuilder line = new();
            while (!ReadLineFromBuffer(line))
            {
                var bytesRead = stream.Read(byteBuffer, byteBufferEnd, byteBuffer.Length - byteBufferEnd);
                if (bytesRead == 0)
                {
                    return line.Length == 0 ? null : line.ToString();
                }
                decoder.Convert(byteBuffer, byteBufferEnd, bytesRead, charBuffer, charBufferEnd, charBuffer.Length - charBufferEnd, false, out var bytesConverted, out var charsConverted, out var completed);
                byteBufferEnd += bytesRead;
                charBufferEnd += charsConverted;
            }
            return line.ToString();
        }

        public byte[] ReadBytes(int count)
        {
            List<byte> result = new(count);

            int numberOfBytesToRead = count;

            // Can read all bytes from buffer
            if (byteBufferEnd >= numberOfBytesToRead)
            {
                int numberOfChars = encoding.GetCharCount(byteBuffer, 0, numberOfBytesToRead);
                result.AddRange(byteBuffer.Take(numberOfBytesToRead));
                ResetBuffer(numberOfChars);
                return result.ToArray();
            }

            // Empty buffer
            result.AddRange(byteBuffer.Take(byteBufferEnd));
            numberOfBytesToRead -= byteBufferEnd;
            ResetBuffer(charBufferEnd);

            // Read remaining bytes directly from stream
            while (numberOfBytesToRead > 0)
            {
                var bytesRead = stream.Read(byteBuffer, 0, Math.Min(byteBuffer.Length, numberOfBytesToRead));
                if (bytesRead == 0)
                {
                    throw new EndOfStreamException($"{numberOfBytesToRead} more bytes couldn't be read from the stream.");
                }
                result.AddRange(byteBuffer.Take(bytesRead));
                numberOfBytesToRead -= bytesRead;
            }
            return result.ToArray();
        }

        public string ReadBytesAsText(int numberOfBytes)
        {
            byte[] content = ReadBytes(numberOfBytes);
            return encoding.GetString(content);
        }

        private bool ReadLineFromBuffer(StringBuilder line)
        {
            var charsConsumed = 0;
            while (charsConsumed < charBufferEnd)
            {
                // TODO this is totally wrong when using *weird* encodings or systems (see https://en.wikipedia.org/wiki/Newline#Representation)
                // it might be better to have the user decide what he considers a Newline
                // and then we could also rename the method to something like
                // `bool ReadFromBufferUntil(List<byte> content, byte[] endMarker, bool includeEndMarkerInContent)`

                if (charBuffer[charsConsumed] == '\r')
                {
                    if (charsConsumed + 1 >= charBufferEnd)
                    {
                        ResetBuffer(charsConsumed);
                        return false;
                    }
                    else if (charBuffer[charsConsumed + 1] == '\n')
                    {
                        charsConsumed += 2;
                        ResetBuffer(charsConsumed);
                        return true;
                    }
                }
                line.Append(charBuffer[charsConsumed]);
                charsConsumed++;
            }
            ResetBuffer(charsConsumed);
            return false;
        }

        private void ResetBuffer(int charsConsumed)
        {
            var consumedBytes = encoding.GetByteCount(charBuffer, 0, charsConsumed);

            charBufferEnd -= charsConsumed;
            Array.Copy(charBuffer, charsConsumed, charBuffer, 0, charBufferEnd);

            byteBufferEnd -= consumedBytes;
            Array.Copy(byteBuffer, consumedBytes, byteBuffer, 0, byteBufferEnd);
        }
    }
}
