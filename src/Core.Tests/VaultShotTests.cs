using System;
using System.IO;
using System.Linq;
using System.Numerics;
using OctoShoots.Core.Gen.TopDown;
using OctoShoots.Core.Items;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;
using Xunit;

namespace OctoShoots.Core.Tests;

/// <summary>Her shots pop the bubble vaults (PlaneVaults.cs).</summary>
public class VaultShotTests
{
    static readonly Lazy<ItemCatalog> Catalog = new(() =>
        ItemCatalog.FromJson(File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "data", "items.json"))));

    [Fact]
    public void A_shot_pops_a_vault()
    {
        PlaneVault? popped = null;
        for (int level = 1; level <= 4 && popped is null; level++)
        {
            var map = TopDownGenerator.Generate(new RunStreams(TestLevels.Seed), new LevelId(1, 1 + LevelPlan.Tutorial, level), crossover: true);
            var w = new PlaneWorld(map, new Tuning(), new PlaneRun(Catalog.Value, new Tuning()), new PlaneOptions { Adventure = true, Glow = false, Gun = true });
            w.Murklings.Clear();
            foreach (var vault in w.Vaults)
            {
                // (From open water a few metres off its face, in sight of it.)
                var face = -vault.Wall;
                var at = Enumerable.Range(0, 7).Select(k => vault.Position + Rotate(face, (k - 3) * 0.35f) * 3.5f)
                    .FirstOrDefault(p => w.Clear(p, w.Radius + 0.1f) && w.LineOfSight(p, vault.Position));
                if (at == Vector2.Zero) continue;
                var aim = Vector2.Normalize(vault.Position - at);
                for (int i = 0; i < 90 && !vault.Burst; i++)
                {
                    w.Player.Position = w.Player.PrevPosition = at;
                    w.Player.Velocity = Vector2.Zero;
                    w.Player.Hp = w.Run.MaxHp;
                    w.Step(new PlaneInput { Fire = i % 15 == 0, Aim = aim });
                }
                Assert.True(vault.Burst, "her shots did not pop the vault");
                popped = vault;
                break;
            }
        }
        Assert.NotNull(popped);
    }

    static Vector2 Rotate(Vector2 v, float a) => new(v.X * MathF.Cos(a) - v.Y * MathF.Sin(a), v.X * MathF.Sin(a) + v.Y * MathF.Cos(a));
}
