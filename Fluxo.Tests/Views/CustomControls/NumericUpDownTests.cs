using System;
using System.Threading;
using Xunit;

namespace Fluxo.Tests.Views.CustomControls;

public sealed class NumericUpDownTests
{
    [Fact]
    public void Constructor_UsesExpectedDefaults()
    {
        RunOnStaThread(() =>
        {
            var control = new NumericUpDown();

            Assert.Equal(0m, control.Value);
            Assert.Equal(0m, control.LowerLimit);
            Assert.Equal(decimal.MaxValue, control.UpperLimit);
            Assert.Equal(1m, control.Step);
        });
    }

    [Theory]
    [InlineData(5, 1, 0, 10, 6)]
    [InlineData(9.75, 0.5, 0, 10, 10)]
    [InlineData(0, 1, 0, 10, 1)]
    public void IncrementValue_AddsStepWithinLimits(
        decimal value,
        decimal step,
        decimal lowerLimit,
        decimal upperLimit,
        decimal expected)
    {
        var actual = NumericUpDown.CoerceValueWithinLimits(value + step, lowerLimit, upperLimit);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(5, 1, 0, 10, 4)]
    [InlineData(0.25, 0.5, 0, 10, 0)]
    [InlineData(10, 2.5, 0, 10, 7.5)]
    public void DecrementValue_SubtractsStepWithinLimits(
        decimal value,
        decimal step,
        decimal lowerLimit,
        decimal upperLimit,
        decimal expected)
    {
        var actual = NumericUpDown.CoerceValueWithinLimits(value - step, lowerLimit, upperLimit);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData(-3, 0, 10, 0)]
    [InlineData(13, 0, 10, 10)]
    [InlineData(4.25, 0, 10, 4.25)]
    [InlineData(4.25, 10, 0, 10)]
    public void CoerceValueWithinLimits_ClampsToNormalizedRange(
        decimal value,
        decimal lowerLimit,
        decimal upperLimit,
        decimal expected)
    {
        var actual = NumericUpDown.CoerceValueWithinLimits(value, lowerLimit, upperLimit);

        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("4.25", 0, 10, 4.25)]
    [InlineData("13", 0, 10, 10)]
    [InlineData("-3", 0, 10, 0)]
    [InlineData(" 7.5 ", 0, 10, 7.5)]
    public void TryParseValueText_ParsesDecimalTextAndClampsToLimits(
        string text,
        decimal lowerLimit,
        decimal upperLimit,
        decimal expected)
    {
        var parsed = NumericUpDown.TryParseValueText(text, lowerLimit, upperLimit, out var actual);

        Assert.True(parsed);
        Assert.Equal(expected, actual);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("4.2.1")]
    public void TryParseValueText_ReturnsFalseForInvalidText(string text)
    {
        var parsed = NumericUpDown.TryParseValueText(text, 0, 10, out _);

        Assert.False(parsed);
    }

    private static void RunOnStaThread(Action action)
    {
        Exception? exception = null;
        var thread = new Thread(() =>
        {
            try
            {
                action();
            }
            catch (Exception caughtException)
            {
                exception = caughtException;
            }
        });

        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();

        if (exception is not null)
            throw exception;
    }
}
