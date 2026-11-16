# Murklings as small black holes, and a beam that shows its strength

A proposal (reference: the user's mock-up of a murkling caught in her beam: a black sphere ringed by a thin bright rim,
ink spinning round it in a flat whirl, flecks of ink torn off it into the beam). It changes how every murkling looks
and reacts to her light, and how her beam shows its strength. Its rules are in §1–§6; what it replaces in §7; the open
questions in §8.

## 1. Her beam: as bright as it burns

- The beam's look (`BeamView`, `her_beam_volume.gdshader`) takes its brightness from what it deals: its damage a second
  against a plain murkling (her beam stat, her pearls' and synergies' boosts, Menace's armour aside).
- A base beam is drawn as it is now; one dealing twice that, about 1.6× as bright (the volume's glow and its core line);
  three times, about 2×; never more than 2.5×, so a strong late beam does not white out the screen. A weak beam (a
  curse, a debuff) dims to as little as 0.6×.
- Only the drawing changes: what the beam lights for the game (who is seen, who is burnt) is as now.

## 2. The body: a hole in the water

- **The event horizon:** its body is drawn as a sphere of flat, lightless black: no bands, no brushed rim, no sheen. Its
  liquid wobble (the springs, the dents, the slingshot) stays, so it still moves like ink.
- **The photon ring:** a thin bright rim hugs its silhouette, brightest on the side her light comes from, in her light's
  colour. It shows only while her light is on it.
- **Light bent round it:** the water behind it warps round it, a screen-space lensing like the hot-road shimmer the game
  already draws (`Swells`), pulling the picture in toward its edge: strongest just outside the rim, gone by about 1.6× its
  radius. Always on while it is drawn (seen).
- **The whirl:** two or three flat rings of ink turn round it in her plane (an accretion disc, tilted a little toward
  the camera), thin and broken, spinning faster the nearer they are, laced with the hot-air shimmer. They show
  wherever it shows.

## 3. Her light on it

- **No lit side:** her light no longer lights the body's near face (§7). The only light on the body is the photon ring
  and its core.
- **Its core burns in her colour:** while her beam (or a shot) is hitting it, its core glows in exactly the colour her
  light is (her pearls' hue, prism-tinted rays), as bright as the beam that strikes it is drawn (§1), shining out through
  the black. Once she stops, it fades over about 0.4 s back to its dim ember.
- **It sheds ink into her light:** while she burns it, flecks of black ink peel off its surface on the side her light
  comes from (more, the more of its meter a second she is taking), drift slowly toward her (about 1 m/s, swaying), and
  burn away within about 0.5 s: each one flares to an ember of her light's colour and is gone. None reach her; they
  hurt nothing.

## 4. Regrowing, unseen

- Its meter regrows as now: out of all her light for 1 s, 12% of its meter a second (`MurkSizeTuning`).
- While it regrows and no light shows it, the water round it gives it away: rings of hot-air shimmer drawn **in** from
  about 3 m toward the spot it is in, one every 0.6 s, each shrinking into it over about 0.9 s, for as long as it
  regrows. A full one shows none. (This adds to the existing outward shimmer that warns of an unseen murkling near her;
  the two read as opposite: one pulses out toward her, the other sucks in.)

## 5. Freed: it implodes

- Emptied, it no longer bursts. Its body collapses inward to a point over 0.25 s (the whirl spun in faster and pulled
  in with it, the water's warp tightening), then one bright flash of her light's colour and a single quick ring of
  shimmer outward.
- Its core of light is left where it was (as now), and its shells and ember as now.

## 6. Which creatures

All budded murklings (basic, throwers, hunters, primes) and Drippus Prime's brood. Lesser murklings (turned fish) keep
their fish's ember inside the black. Other corrupted creatures (Blightroots, Blotters, Thumpsquirts, Inkwardens,
Lashjaws) are unchanged.

## 7. What it replaces

- The murkling's painted look (`murkling.gdshaderinc`: brushed ink bands, the violet dry-brushed rim, the sheen) gives
  way to the black body and photon ring.
- Her light's lit side on its body (the near face lit as her light shows the reef) is removed; the core glow and the
  ring take its place. Her light's outline (`worn`) on murklings goes too, its job (showing it is hurt) done by its size.
- The hit burst out of its far side gives way to the ink peeling into the beam (§3); the free burst to the implosion
  (§5).

## 8. Open questions

1. **"Beam brightness tied to damage"**: is it its damage stat (§1: stronger pearls, a brighter beam all the time), or
   how much it is dealing at the moment (bright only while it burns something)?
2. **"Remove the current illumination effect from the beam side"**: is that the murkling's near face lit by her beam
   (§7), or something else (the halo the beam's light casts on the reef round it, the beam's glow where it hits)?
3. **The lensing and the whirl** (§2): always on while it can be seen, or only while her light is on it?
4. **Unseen regrowth** (§4): only while unseen, as asked, or always while it regrows (seen too)?
5. **Regrowth itself**: keep today's numbers (1 s pause, 12% a second), or change them?
6. **Scope** (§6): only the plain murklings, or primes, throwers, hunters and the boss's brood too?
