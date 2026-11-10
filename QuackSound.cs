using System.Buffers.Binary;

namespace DesktopDuck;

internal static class QuackSound
{
    public static Stream OpenStream(int volumeDb = 0)
    {
        ArgumentOutOfRangeException.ThrowIfGreaterThan(volumeDb, 0);
        using Stream recording = typeof(QuackSound).Assembly.GetManifestResourceStream("DesktopDuck.Assets.quack.wav")
            ?? throw new InvalidOperationException("The embedded duck recording is missing.");
        using var buffer = new MemoryStream();
        recording.CopyTo(buffer);
        byte[] wave = buffer.ToArray();

        // The bundled recording is mono 16-bit PCM with a 44-byte WAV header.
        const int headerLength = 44;
        double gain = Math.Pow(10, volumeDb / 20.0);
        for (int offset = headerLength; offset < wave.Length; offset += sizeof(short))
        {
            Span<byte> sampleBytes = wave.AsSpan(offset, sizeof(short));
            short sample = BinaryPrimitives.ReadInt16LittleEndian(sampleBytes);
            BinaryPrimitives.WriteInt16LittleEndian(sampleBytes, (short)Math.Round(sample * gain));
        }

        return new MemoryStream(wave, writable: false);
    }
}
