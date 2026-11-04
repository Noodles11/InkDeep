using OctoShoots.Core.Bosses;

namespace OctoShoots.Game.TopDown.Bosses;

/// <summary>The rig that draws a boss's body plan (docs/BOSS-GENERATOR.md §13).</summary>
public static class Rigs
{
    public static BossRig For(BossSpec spec) => spec.Plan switch
    {
        BodyPlan.Bivalve => new ClamRig(),
        BodyPlan.Echinoderm => new StarRig(),
        BodyPlan.Cnidarian => new JellyRig(),
        BodyPlan.Ink => new DrippusRig(),
        BodyPlan.Reptile => new TurtleRig(),
        BodyPlan.Fish => new FishRig(),
        BodyPlan.Cephalopod => new OctopusRig(),
        _ => new GastropodRig(),
    };
}
