using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using LaoDai.BattleSpirits.Visuals;

internal static class Program
{
    private static int passed, failed, samples, summonSamples;
    private static void Assert(bool condition, string message)
    { if (!condition) throw new Exception(message); }
    private static void Near(float actual, float expected, float tolerance = 0.00001f)
        => Assert(Math.Abs(actual - expected) <= tolerance, "Expected " + expected + ", got " + actual + ".");
    private static void Case(string name, Action test)
    {
        try { test(); passed++; Console.WriteLine("PASS " + name); }
        catch (Exception error) { failed++; Console.WriteLine("FAIL " + name + ": " + error.Message); }
    }
    private static void Invalid(byte[] data)
    {
        try { PcmWave.Read(data); }
        catch (InvalidDataException) { return; }
        throw new Exception("Malformed/unsupported WAV was accepted.");
    }
    private static byte[] Wave(byte[] data, ushort channels = 1, int rate = 22050,
        ushort bits = 16, ushort format = 1, bool oddChunk = false)
    {
        using var stream = new MemoryStream();
        using var writer = new BinaryWriter(stream, Encoding.ASCII, true);
        void Tag(string tag) => writer.Write(Encoding.ASCII.GetBytes(tag));
        Tag("RIFF"); writer.Write(0); Tag("WAVE");
        if (oddChunk) { Tag("JUNK"); writer.Write(1); writer.Write((byte)0xaa); writer.Write((byte)0); }
        Tag("fmt "); writer.Write(16); writer.Write(format); writer.Write(channels); writer.Write(rate);
        writer.Write(rate * channels * bits / 8); writer.Write((ushort)(channels * bits / 8)); writer.Write(bits);
        Tag("data"); writer.Write(data.Length); writer.Write(data);
        if ((data.Length & 1) != 0) writer.Write((byte)0);
        stream.Position = 4; writer.Write((int)stream.Length - 8);
        return stream.ToArray();
    }
    private static void Finite(BattleVisualSample value)
    {
        foreach (float number in new[] { value.height, value.scale, value.advance, value.tilt, value.wing, value.glow })
            Assert(!float.IsNaN(number) && !float.IsInfinity(number), "Animation sample is not finite.");
        samples++;
    }
    private static float[] SummonValues(BattleSummonSample value) => new[] {
        value.backdropAlpha,value.cardAlpha,value.cardScale,value.cardFlipY,value.cardTilt,
        value.illustrationAlpha,value.illustrationScale,value.illustrationRise,value.flashAlpha,
        value.sweep,value.glow,value.nameAlpha,value.landing
    };
    private static void Finite(BattleSummonSample value)
    {
        Assert(SummonValues(value).All(number=>!float.IsNaN(number)&&!float.IsInfinity(number)),"Summon sample is not finite.");
        summonSamples++;
    }
    public static int Main(string[] arguments)
    {
        string root = Path.GetFullPath(arguments.Length > 0 ? arguments[0] : ".");
        foreach (BattleVisualPose pose in Enum.GetValues(typeof(BattleVisualPose)))
        {
            var selected = pose;
            Case("Frame-independent bounded animation " + pose, () => {
                float duration = BattleVisualMotion.Duration(selected);
                Assert(duration >= 0 && duration <= 2, "Unexpected animation duration.");
                for (int frame = 0; frame <= 500; frame++)
                {
                    float age = -1 + frame * 0.01f;
                    var sample = BattleVisualMotion.Sample(selected, age, frame * 0.013f);
                    Finite(sample);
                    Assert(sample.scale >= 0.009f && sample.scale <= 1.001f, "Scale outside its intended range.");
                    Assert(Math.Abs(sample.advance) <= 3.001f && Math.Abs(sample.tilt) <= 65.001f,
                        "Motion exceeds its intended stage bounds.");
                    Assert(Math.Abs(sample.wing) <= 46.001f && sample.glow >= -0.00001f && sample.glow <= 1.001f,
                        "Wing/glow curve exceeds its intended range.");
                }
                var end = BattleVisualMotion.Sample(selected, duration, 0);
                var later = BattleVisualMotion.Sample(selected, duration + 100, 0);
                Near(end.height, later.height); Near(end.scale, later.scale); Near(end.advance, later.advance);
                Near(end.tilt, later.tilt); Near(end.wing, later.wing); Near(end.glow, later.glow);
            });
        }
        Case("Summon/attack/death curves reach visible expected key poses", () => {
            Near(BattleVisualMotion.Sample(BattleVisualPose.Summon, 0, 0).scale, 0.02f);
            Near(BattleVisualMotion.Sample(BattleVisualPose.Summon, 1.1f, 0).scale, 1);
            Near(BattleVisualMotion.Sample(BattleVisualPose.Attack, 0.325f, 0).advance, 3);
            Near(BattleVisualMotion.Sample(BattleVisualPose.Attack, 0.65f, 0).advance, 0);
            Near(BattleVisualMotion.Sample(BattleVisualPose.Death, 0.7f, 0).scale, 0.01f);
        });
        Case("NaN and infinite animation clocks use a finite fallback", () => {
            foreach (BattleVisualPose pose in Enum.GetValues(typeof(BattleVisualPose)))
                foreach (float input in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
                {
                    Finite(BattleVisualMotion.Sample(pose, input, 0));
                    Finite(BattleVisualMotion.Sample(pose, 0, input));
                }
        });
        Case("Extreme finite animation clocks remain finite", () => {
            foreach (BattleVisualPose pose in Enum.GetValues(typeof(BattleVisualPose)))
                foreach (float input in new[] { float.MaxValue, float.MinValue, 1e20f })
                    Finite(BattleVisualMotion.Sample(pose, float.MaxValue, input));
        });
        Case("Summon presentation reveals artwork before landing on the field",()=>{
            var start=BattleSummonMotion.Sample(0);var reveal=BattleSummonMotion.Sample(1);var landed=BattleSummonMotion.Sample(BattleSummonMotion.Duration);
            Finite(start);Finite(reveal);Finite(landed);
            Near(start.illustrationAlpha,0);Near(start.landing,0);Near(start.cardScale,0.18f);
            Assert(reveal.cardAlpha>0.99f&&reveal.illustrationAlpha>0.99f&&reveal.nameAlpha>0.99f,"Face and illustration never become fully visible.");
            Near(reveal.landing,0);Near(landed.landing,1);Near(landed.cardAlpha,0);Near(landed.illustrationAlpha,0);Near(landed.backdropAlpha,0);
        });
        Case("Summon landing fits the projected card without double foreshortening",()=>{
            foreach(float projectedPixels in new[]{67f,102f})
            {
                BattleSummonMotion.GetLanding(96f/1600,projectedPixels/900,1600f/900,675f/983,out float height,out float tilt);
                Near(height*900,96f/(675f/983));
                Near((float)(height*900*Math.Cos(tilt*Math.PI/180)),projectedPixels);
                Assert(tilt>=0&&tilt<90,"Landing tilt outside visible range.");
            }
            BattleSummonMotion.GetLanding(float.NaN,0.1f,float.PositiveInfinity,0,out float invalidHeight,out float invalidTilt);
            Near(invalidHeight,0);Near(invalidTilt,0);
            BattleSummonMotion.GetLanding(0.1f,0.1f,float.NaN,float.NaN,out float fallbackHeight,out float fallbackTilt);
            Assert(!float.IsNaN(fallbackHeight)&&!float.IsInfinity(fallbackHeight)&&!float.IsNaN(fallbackTilt),"Invalid layout did not use finite defaults.");
        });
        Case("Summon presentation remains bounded across its complete timeline",()=>{
            for(int frame=0;frame<=500;frame++)
            {
                var value=BattleSummonMotion.Sample(-1+frame*0.01f);Finite(value);
                foreach(float alpha in new[]{value.backdropAlpha,value.cardAlpha,value.illustrationAlpha,value.flashAlpha,value.glow,value.nameAlpha,value.landing,value.sweep,value.illustrationRise})
                    Assert(alpha>=-0.00001f&&alpha<=1.00001f,"Summon opacity/progress escapes [0,1].");
                Assert(value.cardScale>=0.179f&&value.cardScale<=1.036f&&value.illustrationScale>=0.879f&&value.illustrationScale<=1.026f,"Summon scale grows outside its layout.");
                Assert(Math.Abs(value.cardFlipY)<=82.001f&&Math.Abs(value.cardTilt)<=12.001f,"Summon rotation exceeds intended turn.");
            }
        });
        Case("Summon sampling is independent of previous frames or viewing order",()=>{
            var before=SummonValues(BattleSummonMotion.Sample(0.95f));
            foreach(float time in new[]{100f,0f,0.2f,1.7f,-10f,0.95f}) Finite(BattleSummonMotion.Sample(time));
            var after=SummonValues(BattleSummonMotion.Sample(0.95f));
            for(int index=0;index<before.Length;index++) Near(before[index],after[index]);
        });
        Case("Summon invalid and extreme clocks resolve to finite safe endpoints",()=>{
            var start=SummonValues(BattleSummonMotion.Sample(0));var end=SummonValues(BattleSummonMotion.Sample(BattleSummonMotion.Duration));
            foreach(float time in new[]{float.NaN,float.NegativeInfinity,float.MinValue,float.PositiveInfinity,float.MaxValue})
            {
                var value=BattleSummonMotion.Sample(time);Finite(value);
                var expected=float.IsPositiveInfinity(time)||time==float.MaxValue?end:start;
                var actual=SummonValues(value);for(int index=0;index<actual.Length;index++) Near(actual[index],expected[index]);
            }
        });
        Case("PCM16 signed samples decode correctly", () => {
            var wave = PcmWave.Read(Wave(new byte[] { 0, 0, 255, 127, 0, 128, 255, 255 }));
            Assert(wave.channels == 1 && wave.sampleRate == 22050 && wave.samples.Length == 4, "Wrong format.");
            Near(wave.samples[0], 0); Near(wave.samples[1], 32767 / 32768f);
            Near(wave.samples[2], -1); Near(wave.samples[3], -1 / 32768f);
        });
        Case("Odd unknown chunks are skipped with word alignment", () => {
            var wave = PcmWave.Read(Wave(new byte[] { 0, 0, 255, 127 }, oddChunk: true));
            Assert(wave.samples.Length == 2, "Odd chunk consumed PCM bytes.");
            Near(wave.samples[1], 32767 / 32768f);
        });
        Case("PCM parser rejects bad headers and truncated chunks", () => {
            Invalid(null); Invalid(new byte[43]); Invalid(new byte[16 * 1024 * 1024 + 1]);
            var bytes = Wave(new byte[] { 0, 0 });
            var riff = (byte[])bytes.Clone(); riff[0] = (byte)'X'; Invalid(riff);
            var type = (byte[])bytes.Clone(); type[8] = (byte)'X'; Invalid(type);
            var truncated = bytes.Take(bytes.Length - 1).ToArray(); Invalid(truncated);
            var shortFormat = (byte[])bytes.Clone(); shortFormat[16] = 15; Invalid(shortFormat);
        });
        Case("PCM parser rejects unsupported encodings and invalid sample frames", () => {
            foreach (var bytes in new[] {
                Wave(new byte[]{0,0}, format:3), Wave(new byte[]{0,0}, channels:0), Wave(new byte[]{0,0}, channels:3),
                Wave(new byte[]{0,0}, rate:7999), Wave(new byte[]{0,0}, rate:96001), Wave(new byte[]{0,0}, bits:8),
                Wave(Array.Empty<byte>()), Wave(new byte[]{0}), Wave(new byte[]{0,0}, channels:2)
            }) Invalid(bytes);
        });
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "Sound/BattleSpirits/manifest.json")));
        var entries = manifest.RootElement.GetProperty("files").EnumerateArray().ToArray();
        Assert(entries.Length == 9, "Expected nine original sound-bank files.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var entry in entries)
        {
            string cue = entry.GetProperty("cue").GetString();
            string relative = entry.GetProperty("path").GetString();
            string hash = entry.GetProperty("sha256").GetString();
            double seconds = entry.GetProperty("seconds").GetDouble();
            Case("Original sound bank integrity/headroom " + cue, () => {
                Assert(seen.Add(cue), "Duplicate cue.");
                string path = Path.GetFullPath(Path.Combine(root, relative));
                Assert(path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal), "Manifest escapes project.");
                byte[] bytes = File.ReadAllBytes(path);
                using var sha = SHA256.Create();
                string actual = BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
                Assert(actual == hash, "Generated audio bytes disagree with provenance manifest.");
                var wave = PcmWave.Read(bytes);
                Assert(wave.sampleRate == 22050 && wave.channels == 1, "Unexpected shipped PCM format.");
                Assert(wave.samples.Length == (int)Math.Round(seconds * 22050, MidpointRounding.ToEven), "Wrong duration/frame count.");
                Assert(wave.samples.Any(sample => Math.Abs(sample) > 0.01), "Silent cue.");
                Assert(wave.samples.All(sample => !float.IsNaN(sample) && Math.Abs(sample) <= 0.651), "Clipped/nonfinite sample.");
                Assert(Math.Abs(wave.samples.Average(sample => (double)sample)) < 0.0001, "Excessive DC offset.");
                Near(wave.samples[0], 0); Near(wave.samples[wave.samples.Length - 1], 0);
            });
        }
        Console.WriteLine("RESULT " + passed + " passed, " + failed + " failed; " + samples + " animation samples; " + summonSamples + " summon samples. Pure production curves/PCM only; no Unity audio playback or rendering.");
        return failed == 0 ? 0 : 1;
    }
}
