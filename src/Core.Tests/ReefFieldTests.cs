using System;
using OctoShoots.Core.Gen.TopDown;
using Xunit;

namespace OctoShoots.Core.Tests;

public class ReefFieldTests
{
    const long Seed = 714203;

    [Fact]
    public void The_field_is_the_prototypes_to_the_last_digit()
    {
        // (Read off maps/reef_field.gd in Godot 4.7.2 for the same points and seed.)
        Assert.Equal(0.107502936635, ReefField.Density(0, 0, 0, Seed), 10);
        Assert.Equal(0.158828797368, ReefField.Density(17, -9, 0, Seed), 10);
        Assert.Equal(-0.149916703058, ReefField.Density(-4000, 3210, 6, Seed), 10);
        Assert.Equal(0.046399766698, ReefField.Density(123456, -987654, -300, Seed), 10);
        Assert.Equal(0.048364047541, ReefField.Density(5, 77, 4, Seed), 10);
        Assert.Equal(0.046096770093, ReefField.Cutoff(ReefField.Calibration(Seed), 0.43), 10);
    }

    [Fact]
    public void A_rock_target_holds_on_independent_points()
    {
        // (As the prototype's verify.gd: its cutoff, against 2048 points it never sampled.)
        var values = ReefField.Calibration(Seed);
        foreach (double target in new[] { 0.2, 0.43, 0.75 })
        {
            double cut = ReefField.Cutoff(values, target);
            int hits = 0;
            for (int i = 0; i < 2048; i++)
            {
                long x = ReefField.Hash(i, 81, 19, 4142) % 65536 - 32768, z = ReefField.Hash(i, 92, 83, 7913) % 65536 - 32768;
                long y = (ReefField.Hash(i, 30, 50, 3197) % 256 - 128) * ReefField.Steps;
                if (ReefField.Density(x, z, y, Seed) >= cut) hits++;
            }
            Assert.True(Math.Abs(hits / 2048.0 - target) < 0.055, $"{target}: {hits / 2048.0:0.000}");
        }
    }
}
