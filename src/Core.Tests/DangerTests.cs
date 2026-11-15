using OctoShoots.Core.Plane;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>The depth's danger clock: its minutes cut into five equal steps, the last held.</summary>
public class DangerTests
{
    [Theory]
    [InlineData(0f, 0)]
    [InlineData(119f, 0)]
    [InlineData(120f, 1)]
    [InlineData(250f, 2)]
    [InlineData(479f, 3)]
    [InlineData(480f, 4)]
    [InlineData(5000f, 4)]
    public void TenMinutesInFiveSteps(float seconds, int step)
    {
        float was = DangerTuning.Minutes;
        DangerTuning.Minutes = 10f;
        try { Assert.Equal(step, DangerTuning.StepAt(seconds)); }
        finally { DangerTuning.Minutes = was; }
    }
}
