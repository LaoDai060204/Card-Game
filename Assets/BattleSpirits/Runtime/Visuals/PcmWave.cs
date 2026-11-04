using System;
using System.IO;

namespace LaoDai.BattleSpirits.Visuals
{
    public sealed class PcmWave
    {
        public readonly int channels, sampleRate;
        public readonly float[] samples;
        private PcmWave(int channels, int sampleRate, float[] samples)
        { this.channels = channels; this.sampleRate = sampleRate; this.samples = samples; }

        // Our generated assets use uncompressed PCM16. Parse chunks instead of
        // assuming every WAV has exactly a 44-byte header.
        public static PcmWave Read(byte[] bytes)
        {
            if (bytes == null || bytes.Length < 44 || bytes.Length > 16 * 1024 * 1024)
                throw new InvalidDataException("Invalid Battle Spirits PCM WAV size.");
            using (var stream = new MemoryStream(bytes, false))
            using (var reader = new BinaryReader(stream))
            {
                if (new string(reader.ReadChars(4)) != "RIFF") throw new InvalidDataException("Missing RIFF header.");
                reader.ReadUInt32();
                if (new string(reader.ReadChars(4)) != "WAVE") throw new InvalidDataException("Missing WAVE header.");
                int channels = 0, rate = 0, bits = 0, format = 0;
                byte[] data = null;
                while (stream.Position + 8 <= stream.Length)
                {
                    string chunk = new string(reader.ReadChars(4));
                    uint length = reader.ReadUInt32();
                    long end = stream.Position + length;
                    if (end > stream.Length) throw new InvalidDataException("Truncated WAV chunk.");
                    if (chunk == "fmt ")
                    {
                        if (length < 16) throw new InvalidDataException("Truncated WAV format.");
                        format = reader.ReadUInt16(); channels = reader.ReadUInt16(); rate = reader.ReadInt32();
                        reader.ReadUInt32(); reader.ReadUInt16(); bits = reader.ReadUInt16();
                    }
                    else if (chunk == "data") data = reader.ReadBytes((int)length);
                    stream.Position = Math.Min(stream.Length, end + (length & 1));
                }
                if (format != 1 || channels < 1 || channels > 2 || rate < 8000 || rate > 96000
                    || bits != 16 || data == null || data.Length == 0 || data.Length % (2 * channels) != 0)
                    throw new InvalidDataException("Expected mono/stereo PCM16 WAV audio.");
                var samples = new float[data.Length / 2];
                for (int index = 0; index < samples.Length; index++)
                    samples[index] = (short)(data[index * 2] | data[index * 2 + 1] << 8) / 32768f;
                return new PcmWave(channels, rate, samples);
            }
        }
    }
}
