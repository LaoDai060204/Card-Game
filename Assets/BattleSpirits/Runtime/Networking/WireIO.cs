using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LaoDai.BattleSpirits.Networking
{
    internal sealed class WireReader
    {
        private static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);
        private readonly Stream stream;
        private readonly byte[] buffer = new byte[4096];
        private int position;
        private int available;

        public WireReader(Stream stream) { this.stream = stream; }

        public async Task<string> ReadLineAsync(CancellationToken cancellationToken)
        {
            using (var line = new MemoryStream())
            {
                while (true)
                {
                    if (position == available)
                    {
                        available = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false);
                        position = 0;
                        if (available == 0)
                        {
                            if (line.Length != 0) throw new EndOfStreamException("Incomplete protocol line.");
                            return null;
                        }
                    }
                    int end = Array.IndexOf(buffer, (byte)'\n', position, available - position);
                    int count = (end < 0 ? available : end) - position;
                    if (line.Length + count > Protocol.MaxLineBytes)
                        throw new InvalidDataException("Protocol line exceeds the size limit.");
                    line.Write(buffer, position, count);
                    position += count;
                    if (end < 0) continue;
                    position++;
                    byte[] bytes = line.ToArray();
                    int length = bytes.Length;
                    if (length > 0 && bytes[length - 1] == '\r') length--;
                    return Utf8.GetString(bytes, 0, length);
                }
            }
        }
    }

    internal static class WireWriter
    {
        public static async Task WriteAsync(Stream stream, string message, CancellationToken cancellationToken)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(message + "\n");
            if (bytes.Length - 1 > Protocol.MaxLineBytes)
                throw new InvalidDataException("Protocol line exceeds the size limit.");
            var write = stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken);
            var completed = await Task.WhenAny(write, Task.Delay(5000, cancellationToken)).ConfigureAwait(false);
            if (completed != write) throw new IOException("Network write timed out.");
            await write.ConfigureAwait(false);
        }
    }
}
