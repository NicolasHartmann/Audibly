using System;
using NAudio.Wave;

namespace Audibly.App.Services.Audio;

/// <summary>Changes tempo without pitch change; resettable after a seek.</summary>
internal interface ITempoSampleProvider : ISampleProvider
{
    void Reset();
}

/// <summary>Which time stretching the player uses; set from the user's settings.</summary>
internal static class TimeStretching
{
    /// <summary>Sonic (speech, as on Android) when true, SoundTouch when false.</summary>
    public static bool UseSonic { get; set; } = true;

    public static ITempoSampleProvider Create(ISampleProvider source, double speed) =>
        UseSonic ? new SonicSampleProvider(source, speed) : new TempoSampleProvider(source, speed);
}

/// <summary>
///     Speech time stretching after the speed path of Sonic by Bill Cox (Apache License 2.0), which
///     Android's ExoPlayer uses: it finds the pitch period of the voice and drops or repeats whole
///     periods, blended by a short cross-fade, so speech stays clear at high speeds.
/// </summary>
internal sealed class SonicSampleProvider : ITempoSampleProvider
{
    private const int MinimumPitch = 65;
    private const int MaximumPitch = 400;
    private const int AmdfFrequency = 4000;

    private readonly ISampleProvider _source;
    private readonly double _speed;
    private readonly bool _passthrough;
    private readonly int _channels;
    private readonly int _minPeriod;
    private readonly int _maxPeriod;
    private readonly int _maxRequired;
    private readonly float[] _downSample;
    private readonly float[] _read;

    private float[] _input;
    private int _inputFrames;
    private float[] _output;
    private int _outputFrames;
    private int _outputStart;
    private int _remainingToCopy;
    private int _prevPeriod;
    private float _prevMinDiff;
    private bool _endOfInput;

    public SonicSampleProvider(ISampleProvider source, double speed)
    {
        if (!double.IsFinite(speed) || speed <= 0)
            throw new ArgumentOutOfRangeException(nameof(speed));

        _source = source;
        _speed = speed;
        _passthrough = speed == 1.0;
        _channels = source.WaveFormat.Channels;
        var sampleRate = source.WaveFormat.SampleRate;
        _minPeriod = sampleRate / MaximumPitch;
        _maxPeriod = sampleRate / MinimumPitch;
        _maxRequired = 2 * _maxPeriod;
        _downSample = new float[_maxRequired];
        _read = new float[4096 * _channels];
        _input = new float[(_maxRequired + 4096) * _channels];
        _output = new float[(_maxRequired + 4096) * _channels * 4];
    }

    public WaveFormat WaveFormat => _source.WaveFormat;

    public int Read(float[] buffer, int offset, int count)
    {
        if (_passthrough) return _source.Read(buffer, offset, count);
        count -= count % _channels;
        var written = 0;

        while (written < count)
        {
            var available = _outputFrames - _outputStart;
            if (available > 0)
            {
                var frames = Math.Min(available, (count - written) / _channels);
                Array.Copy(_output, _outputStart * _channels, buffer, offset + written, frames * _channels);
                _outputStart += frames;
                written += frames * _channels;
                continue;
            }

            _outputFrames = _outputStart = 0;
            if (_endOfInput) break;

            var read = _source.Read(_read, 0, _read.Length);
            if (read == 0)
            {
                _endOfInput = true;
                Flush();
                continue;
            }

            AppendInput(_read, read / _channels);
            ChangeSpeed();
        }

        return written;
    }

    public void Reset()
    {
        _inputFrames = _outputFrames = _outputStart = 0;
        _remainingToCopy = 0;
        _prevPeriod = 0;
        _prevMinDiff = 0;
        _endOfInput = false;
    }

    /// <summary>
    ///     Pushes the last frames through with silence behind them and keeps only as much output as
    ///     they stand for at this speed, as Sonic does.
    /// </summary>
    private void Flush()
    {
        var expected = _outputFrames + (int)(_inputFrames / _speed + 0.5);
        AppendInput(new float[2 * _maxRequired * _channels], 2 * _maxRequired);
        ChangeSpeed();
        _outputFrames = Math.Min(_outputFrames, expected);
        _inputFrames = 0;
        _remainingToCopy = 0;
    }

    private void ChangeSpeed()
    {
        if (_inputFrames < _maxRequired) return;
        var position = 0;
        do
        {
            if (_remainingToCopy > 0)
            {
                var frames = Math.Min(_remainingToCopy, _maxRequired);
                AppendOutput(_input, position, frames);
                _remainingToCopy -= frames;
                position += frames;
            }
            else
            {
                var period = FindPitchPeriod(position);
                position += _speed > 1.0 ? period + SkipPitchPeriod(position, period) : InsertPitchPeriod(position, period);
            }
        } while (position + _maxRequired <= _inputFrames);

        Array.Copy(_input, position * _channels, _input, 0, (_inputFrames - position) * _channels);
        _inputFrames -= position;
    }

    private int SkipPitchPeriod(int position, int period)
    {
        int frames;
        if (_speed >= 2.0)
        {
            frames = (int)(period / (_speed - 1.0));
        }
        else
        {
            frames = period;
            _remainingToCopy = (int)(period * (2.0 - _speed) / (_speed - 1.0));
        }

        EnsureOutput(frames);
        OverlapAdd(frames, _outputFrames, position, position + period);
        _outputFrames += frames;
        return frames;
    }

    private int InsertPitchPeriod(int position, int period)
    {
        int frames;
        if (_speed < 0.5)
        {
            frames = (int)(period * _speed / (1.0 - _speed));
        }
        else
        {
            frames = period;
            _remainingToCopy = (int)(period * (2.0 * _speed - 1.0) / (1.0 - _speed));
        }

        EnsureOutput(period + frames);
        Array.Copy(_input, position * _channels, _output, _outputFrames * _channels, period * _channels);
        OverlapAdd(frames, _outputFrames + period, position + period, position);
        _outputFrames += period + frames;
        return frames;
    }

    /// <summary>Cross-fades from the frames at rampDown to those at rampUp into the output.</summary>
    private void OverlapAdd(int frames, int outputFrame, int rampDown, int rampUp)
    {
        if (frames <= 0) return;
        for (var channel = 0; channel < _channels; channel++)
        {
            var o = outputFrame * _channels + channel;
            var d = rampDown * _channels + channel;
            var u = rampUp * _channels + channel;
            for (var t = 0; t < frames; t++)
            {
                _output[o] = (_input[d] * (frames - t) + _input[u] * t) / frames;
                o += _channels;
                d += _channels;
                u += _channels;
            }
        }
    }

    private int FindPitchPeriod(int position)
    {
        var sampleRate = WaveFormat.SampleRate;
        var skip = sampleRate > AmdfFrequency ? sampleRate / AmdfFrequency : 1;

        DownSample(position, skip);
        var period = FindPitchPeriodInRange(_minPeriod / skip, _maxPeriod / skip, out var minDiff, out var maxDiff);
        if (skip != 1)
        {
            period *= skip;
            var min = Math.Max(period - (skip << 2), _minPeriod);
            var max = Math.Min(period + (skip << 2), _maxPeriod);
            DownSample(position, 1);
            period = FindPitchPeriodInRange(min, max, out minDiff, out maxDiff);
        }

        var result = PreviousPeriodBetter(minDiff, maxDiff) ? _prevPeriod : period;
        _prevMinDiff = minDiff;
        _prevPeriod = period;
        return result;
    }

    /// <summary>Mixes the channels and averages every <paramref name="skip" /> frames.</summary>
    private void DownSample(int position, int skip)
    {
        var samples = _maxRequired / skip;
        var start = position * _channels;
        var perValue = skip * _channels;
        for (var i = 0; i < samples; i++)
        {
            float sum = 0;
            for (var j = 0; j < perValue; j++) sum += _input[start + i * perValue + j];
            _downSample[i] = sum / perValue;
        }
    }

    /// <summary>Average magnitude difference: the period whose shifted copy matches best.</summary>
    private int FindPitchPeriodInRange(int minPeriod, int maxPeriod, out float minDiff, out float maxDiff)
    {
        int bestPeriod = 0, worstPeriod = 255;
        float best = 1, worst = 0;
        for (var period = minPeriod; period <= maxPeriod; period++)
        {
            float diff = 0;
            for (var i = 0; i < period; i++) diff += Math.Abs(_downSample[i] - _downSample[i + period]);
            if (bestPeriod == 0 || diff * bestPeriod < best * period)
            {
                best = diff;
                bestPeriod = period;
            }

            if (diff * worstPeriod > worst * period)
            {
                worst = diff;
                worstPeriod = period;
            }
        }

        minDiff = best / bestPeriod;
        maxDiff = worst / worstPeriod;
        return bestPeriod;
    }

    private bool PreviousPeriodBetter(float minDiff, float maxDiff)
    {
        if (minDiff == 0 || _prevPeriod == 0) return false;
        if (maxDiff > minDiff * 3) return false; // a clear pitch was found
        return minDiff * 2 > _prevMinDiff * 3;
    }

    private void AppendInput(float[] samples, int frames)
    {
        var needed = (_inputFrames + frames) * _channels;
        if (needed > _input.Length) Array.Resize(ref _input, needed * 2);
        Array.Copy(samples, 0, _input, _inputFrames * _channels, frames * _channels);
        _inputFrames += frames;
    }

    private void AppendOutput(float[] samples, int startFrame, int frames)
    {
        EnsureOutput(frames);
        Array.Copy(samples, startFrame * _channels, _output, _outputFrames * _channels, frames * _channels);
        _outputFrames += frames;
    }

    private void EnsureOutput(int frames)
    {
        var needed = (_outputFrames + frames) * _channels;
        if (needed > _output.Length) Array.Resize(ref _output, needed * 2);
    }
}
