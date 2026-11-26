using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen;
using Xunit;

namespace OctoShoots.Core.Tests;

public class PoissonTileTests
{
    [Theory]
    [InlineData(1.6f)]
    [InlineData(3.5f)]
    public void Its_points_keep_their_spacing_across_the_tiles_seams_and_fill_it(float spacing)
    {
        // Across a seam (a box over four tiles' corners): no two closer than the spacing.
        var near = PoissonTile.In(spacing, new Vector2(PoissonTile.Size - 12f, -12f), new Vector2(PoissonTile.Size + 12f, 12f)).ToList();
        Assert.NotEmpty(near);
        for (int i = 0; i < near.Count; i++)
        for (int j = i + 1; j < near.Count; j++)
            Assert.True(Vector2.Distance(near[i], near[j]) >= spacing * 0.999f);
        // Filled: Bridson's leaves about 0.6–0.75 points to a spacing's square.
        Assert.InRange(PoissonTile.Density(spacing) * spacing * spacing, 0.55f, 0.8f);
        // Always the same.
        Assert.Same(PoissonTile.Of(spacing), PoissonTile.Of(spacing));
    }
}
