using Audibly.App.Services.Audio;
using Xunit;
using static Audibly.Audio.Tests.TempoSampleProviderTests;

namespace Audibly.Audio.Tests;

public class SonicSampleProviderTests
{
    [Theory]
    [InlineData(0.5, 1)]
    [InlineData(0.75, 2)]
    [InlineData(1.0, 1)]
    [InlineData(1.25, 2)]
    [InlineData(1.5, 1)]
    [InlineData(1.7, 2)]
    [InlineData(2.0, 2)]
    [InlineData(3.0, 1)]
    [InlineData(4.0, 2)]
    public void ChangesDurationButPreservesPitch(double speed, int channels)
    {
        var input = new ToneProvider(4, channels);
        var output = Drain(new SonicSampleProvider(input, speed));

        Assert.InRange(output.Length / (double)(input.WaveFormat.SampleRate * channels), 4 / speed - 0.01, 4 / speed + 0.01);
        for (var channel = 0; channel < channels; channel++)
        {
            var frequency = MeasureFrequency(output, channels, channel, input.WaveFormat.SampleRate);
            Assert.InRange(frequency, ToneProvider.Frequency(channel) * 0.99, ToneProvider.Frequency(channel) * 1.01);
        }
    }

    [Fact]
    public void ResetDiscardsBufferedAudio()
    {
        var input = new ToneProvider(2, 2);
        var provider = new SonicSampleProvider(input, 2);
        provider.Read(new float[400], 0, 400);
        input.Frame = 0;
        provider.Reset();
        Assert.Equal(Drain(new SonicSampleProvider(new ToneProvider(2, 2), 2)), Drain(provider));
        Assert.Equal(0, provider.Read(new float[512], 0, 512));
    }

    [Fact]
    public void ShortFileIsShortenedToo()
    {
        var output = Drain(new SonicSampleProvider(new ToneProvider(0.01, 1), 1.5));
        Assert.Equal(294, output.Length);
    }
}
