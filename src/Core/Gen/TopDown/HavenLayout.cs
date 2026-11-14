using System;
using System.Collections.Generic;
using System.Linq;
using OctoShoots.Core.Plane;
using OctoShoots.Core.Run;

namespace OctoShoots.Core.Gen.TopDown;

/// <summary>
/// A haven (docs/DIVE.md §4): a small safe sub-level behind a reef's mouth, made from its own seed as a hand-drawn map is
/// (HandLayout, TopDownGenerator.FromHand), all of it haven water. A gate chamber where her way in is cut, a lagoon in its
/// middle with the shop's cave off it, and two nooks either side; the mirror in the lagoon, the Tide Fan in one nook, and
/// a home for each neighbour round the water. Which of them are open there is the game's to say (what she has unlocked,
/// a pick of it a haven: TopDownMain.HavenOffers); the shop always is. Bright and awake, nothing harms her in it.
/// </summary>
public static class HavenLayout
{
    /// <summary>Its square, metres a side.</summary>
    public const int Size = 80;

    /// <summary>A haven's layout, from its id and the run's seed (each reef's haven its own).</summary>
    public static HandLayout Make(SeedCode seed, LevelId id)
    {
        var rng = new Rng(seed.Value ^ 0x4A7E2UL ^ ((ulong)id.Depth << 20) ^ ((ulong)id.Level << 28) ^ ((ulong)id.Lane << 36) ^ ((ulong)id.Cycle << 44));
        float J(float r) => rng.Range(-r, r);
        // (Mirrored at random: the shop's market west or east, the Fan's nook the other way.)
        bool flip = rng.NextFloat() < 0.5f;
        float X(float x) => flip ? Size - x : x;
        var post = new HandChamber { Id = "post", X = X(40f + J(2f)), Y = 66f + J(1.5f), R = 8f, Role = CaveLayout.Role.Start };
        var lagoon = new HandChamber { Id = "lagoon", X = X(40f + J(2f)), Y = 42f + J(2f), R = 12f, Role = CaveLayout.Role.Main };
        var market = new HandChamber { Id = "market", X = X(19f), Y = 42f + J(3f), R = 7f, Role = CaveLayout.Role.Branch };
        var nook = new HandChamber { Id = "nook", X = X(61f), Y = 40f + J(3f), R = 7f, Role = CaveLayout.Role.Exit };
        var hand = new HandLayout
        {
            Name = $"haven {id}", Title = "A haven", Seed = SeedCode.FromValue(rng.NextU64()).ToString(), Depth = 1, Menace = 0f,
            Size = Size, Hole = false, Number = 9,
            Haven = new HandBox { X0 = 0f, Y0 = 0f, X1 = Size, Y1 = Size },
            Chambers = { post, lagoon, market, nook },
            Tunnels =
            {
                new HandTunnel { A = "post", B = "lagoon", Width = 8f },
                new HandTunnel { A = "lagoon", B = "market", Width = 6f, Kind = CorridorKind.Side },
                new HandTunnel { A = "lagoon", B = "nook", Width = 6f, Kind = CorridorKind.Side },
            },
            // The shop's cave off the lagoon's far side.
            Rooms = { new HandRoom { Kind = PoiKind.Shop, From = "lagoon", Angle = 270f + J(15f), Distance = 8f, Width = 6f } },
            Mirror = new HandPoint { X = lagoon.X + (flip ? 5f : -5f), Y = lagoon.Y + 4f },
            Fan = new HandPoint { X = nook.X, Y = nook.Y },
        };
        // A home for each neighbour, round the water: two in the lagoon, the rest by the market and the post.
        var spots = new List<(float X, float Y)>
        {
            (lagoon.X + (flip ? -6f : 6f), lagoon.Y + 5f),
            (lagoon.X, lagoon.Y - 7f),
            (market.X, market.Y + 2f),
            (post.X + (flip ? 4f : -4f), post.Y - 2f),
            (lagoon.X + (flip ? 8f : -8f), lagoon.Y - 3f),
        };
        // (Tull at the Fan she tends.)
        var folk = Folk.All.Select(n => n.Id).ToList();
        int k = 0;
        foreach (string who in folk)
        {
            if (who == "tull") hand.Homes.Add(new HandPoint { X = nook.X + (flip ? 3f : -3f), Y = nook.Y + 3f, Folk = who });
            else if (k < spots.Count)
            {
                hand.Homes.Add(new HandPoint { X = spots[k].X, Y = spots[k].Y, Folk = who });
                k++;
            }
        }
        return hand;
    }
}
