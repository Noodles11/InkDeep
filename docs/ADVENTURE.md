# The adventure

How the adventure (branch `m3-adventure`) plays: the reef is being poisoned from **leaks** floating in its chambers; the poison
spreads as ink dots through the water and turns the reef's fish one by one into the creatures she fights; she wins a
level by **sealing the leaks**, and the fish she frees are fish again (freed, not killed: THEME-BIBLE).

It is on with `PlaneOptions.Adventure` (the game's default). The simulation is in `src/Core/Plane/PlaneAdventure.cs`
(`AdventureTuning`, `InkDots`, `PlaneVent`, the fish gauges, the three murklings, her oral arm),
`src/Core/Plane/PlaneFlounder.cs` (flounders and Hearthguards) and
`src/Core/Gen/TopDown/AbyssReef.cs` (the abyss); the presentation in `src/Game/TopDown/AdventureView.cs` (dots, their
bracket marks, leaks), `BlightView` (the murklings' bodies: `murkling.gdshaderinc` spikes and tadpole tail),
`BlightView.Flounders` (flounders and Hearthguards), `ReefFishView` (anemone pillars) and `HudView` (the gonad gauge, the leak dots). Tests: `AdventureTests`.

All numbers are first guesses (`AdventureTuning`).

## 1. Leaks

- **What:** X-shaped tears floating in the water at her plane, always 5 a level, a little bigger than she is (about 2.6 m
  across) and always facing the camera. They lie in the middle of chambers; failing that on side routes; failing that in
  any open water; only as a last resort on the main current. At least 30 m apart if the level allows it and 24 m from her
  start, never in safe places or at the way down.
- **Nearly invisible:** hot air in the shape of an X (bending the picture in the post pass), with a purple shimmer running
  through it every 3 s, twice as often with her within 10 m. Once she has been within 12 m of it, its outline stays
  faintly there (`leak.gdshader`). The ink thickening round it is how she finds it.
- **It pushes:** a light push outward, within 6 m: 1.5 m/s + 0.5 × Menace at the middle, fading out, dying with it.
- **The only thing that heals.** Every other creature's and boss's meter stays where her light leaves it.
- **Sealing:** 15 hits + 5 × Menace of her light, in five stages. Each of the first four ties one arm with golden thread,
  stitched from the middle out (a rising note, and it spits a ring of 8 ink drops at 60% of a throw's damage). The
  last pulls all four tight: the arms close, and tear and thread dissolve together.
- **Her first hit rouses it:** healthy fish within 14 m turn at once.
- **If she stops:** after 1.5 s it heals, one tie's worth every 1.2 s. The thread trembles first, and each tie snaps in
  reverse as the leak goes back past it.
- **What counts:** a shot's light is its share of a full shot (a volley's small shots are parts of one), once per shot.
  Firefly Squid's surge counts as one shot every shot's time, while it runs through the leak.
- **Each one sealed** throws 5 shells out of it, as well as its experience. Sealing 3 opens the way on and wakes the main
  current (DESIGN-TOPDOWN §4.7). The other two go on leaking until she seals them or the boss swallows them.
- **Inkwardens** still guard the leaks in their reach (§4).

## 2. The ink

- **Seen** as tiny plain purple specks like the plankton, with no glow, on all the water's layers, thicker the nearer the
  leak (`ink_dot.gdshader`). In the sim it is one grid of dots at her plane (`InkDots`).
- **Every level starts part inked:** 15% + 80% × Menace of the water the ink can reach, at most 95% (15% at Depth 1, 95% at
  Depth 6; `AdventureTuning.StartInk`). It grows out of every leak at once (`PlaceInk`), through open water and through
  sealed walls, and each cell belongs to the leak that reached it. The safe places (her start, the rooms, the light
  springs, the way down) stay clean.
- **It no longer spreads,** and creatures leave no trail. All the ink there is comes from the leaks.
- **Clearing is only for the moment.** Her light, her shots and hungry fish clear it, but while its leak is open it
  comes back after 1 s, one cell further out from the leak every 0.2 s along the way it first came, so cleared water
  fills from the leak's side. A clearing buys a safe stretch to fight or rescue fish in.
- **Sealing is what ends it:** a leak's ink dissolves outward from it at 12 m/s, and never returns.
- **Freed fish** stay freed: they eat the ink harmlessly.

## 3. Fish that turn

- **Many fish:** a level of the Kelp Jungle has the Shallows' numbers of reef fish three times over — clownfish at
  their anemones, blue tangs, chromis in schools of 12–24 — and no corrupted creatures at its start.
- **Each fish has its own depth:** there it lives in its full numbers, above it not at all, and on every deeper depth
  at 60% of them (`AdventureTuning.Rate`), so the deeper depths keep the earlier fish, fewer. The Shallows' fish and
  the pufferfish count the Kelp Jungle as their own too (runs start there); the flounder's own is the Kelp Jungle.
- **Each fish's gauge** (0..1): it fills 0.12 a second with a dot within 1 m, 0.2 for each dot it eats, and drains
  0.05 a second over clean water. Full, the fish **turns** (the ink creeps over it) into the murkling of its kind
  (§4); a chromis turns with its whole school. A spiked murkling's throw that strikes a fish adds 0.6 to the gauges in
  its splash. A freed fish never turns again: it swims off away from her and is gone (once 22 m from her, or after
  40 s wherever it is).
- **Flounders** (3–5 a level) lie on their own patches: each on a reef shelf (§5) no anemone takes (elsewhere on
  open sand by the rock, clear of coral), at least 12 m apart. A flounder is drawn as the clownfish are (its own
  mesh and shader, `FlounderMesh`, flounder.gdshader): a flat oval mottled in sand with orange spots ringed in cream,
  a fin fringe round both edges that ripples, a rayed tail fan, both gold-ringed eyes on its top. Healthy, it lies
  still, shimmying itself into the sand every 6–12 s, throwing sand off its flanks; freed, it swims off as a flatfish
  does, waves running down its body. It fills 0.1 a second with a
  dot within 1.2 m (0.2 for each it eats), draining 0.04; full, it is a Hearthguard (§4).
- **Pufferfish** (12–18 a level, in ones and twos) fill too: 0.06 a second with a dot within 1.4 m, 0.2 for each dot
  they eat, draining 0.03; full, a pufferfish is an Inkwarden (§4). Anemones are only the reef's: they never turn.

## 4. The corrupted fish

All of them are fish underneath; freed, they are themselves again and swim off. Her light frees them as
it frees any corrupted creature: a corruption meter each, emptied by her shots.

| Mob | From | Core | Body | Behaviour |
|---|---|---|---|---|
| **Spiked murkling** | blue tang | blue | 0.38 m, short blowfish spikes all over | Keeps 6–9 m from her, circling, at 3.4 m/s; throws ink at her (the slingshot wind-up); never clings. Touching it hurts and knocks her off. |
| **Tadpole murkling** | clownfish | orange | 0.22 m, a short wiggling tadpole tail | Only chases her (within 16 m), at 1.1 times her cruise; clings on and drains her; never throws. |
| **Shoal murkling** | chromis | silver | 0.2 m, a whole school | Very fast (9 m/s). Within 14 m it rings her 1.8–2.6 m out, spread evenly all round in three layers, and the ring circles her (1.1 rad/s, each layer a little faster than the one inside it; each school its own way), never tied to her aim — she times her shots through the gaps, and a shot that meets one is spent on it. Each holds its own place on the ring until it is freed: a freed one leaves a gap, and the rest do not close up. Harmless otherwise. |
| **Hearthguard** | flounder | sand gold | a plain flat oval of ink (1.1 × 0.8 m) lying on its patch, no head, no fins, the sand gold showing through, ink drops lifting off it | Never leaves its patch and never chases her. It rests 2.5–4 s (÷ 1 + 0.4 × Menace), then shimmies left and right for 1.4 s as if burying itself: ten swings, one flank then the other, each throwing a spray of sand off the flank it swings to and that swing's ink drops of its pattern off that flank, lobbed up from its patch to her plane (3.5 m/s, falling after 2.4 m and leaving a dot where they land, 5 damage). The patterns: a fan of three off four of its swings, one drop at every swing sweeping from its head to its tail, or a burst of five off each side at the height of the shimmy, never the same pattern twice running. Within 2.8 m of it (and in sight), its back draws up into a cone leaning at her over 0.45 s, its ember waking in it (the telegraph); then that cone stabs out as one continuous sting to 3.2 m at her plane in 0.12 s while the oval stays where it lies (rock stops it): 12 damage and a shove along it; it holds a moment, draws back over 0.45 s, and waits 1 s before it can stab again. Meter 26 (+ per Menace as a murkling's). Lying under her plane, it shows south of its place on the plane (0.577 m per metre below), so her shots strike it anywhere from its place to where it shows (`PlaneWorld.FlounderSeen`). Freed, it is a flounder again, lifting off its patch and swimming away. |
| **Inkwarden** | pufferfish | yellow | an inflated ring of ink, eight spikes folded in toward its hole | Wards the corrupted creatures round it (8 m), and the open leaks (§1). Calm, it drifts slowly about its spot (within 5 m), turning lazily. Struck (by her light, her arm or a burst), it rolls its spikes out over its top (0.5 s), spins up (0.6 s) and fires them one by one, one every 0.15 s, each in a random direction (11 m/s, 6 damage, an ink spike: it falls as a throw does and her shots burst it); then it slows and grows them back inward (3 s), calm until struck again. Freed, it is a pufferfish, and swims off. |

Each depth's boss level holds its boss in its arena, Drippus Prime (docs/CORRUPTION.md); its brood are murklings like
any other. On a boss level the way down opens once the boss is freed.

**It gathers the level's ink.** No title screen: the moment she is wholly inside the arena, every leak still open is drunk
dry and shut (a puff, no shells, no experience), and every dot left on the level streams to the middle of the arena over
3.4 s, the nearest first, those from beyond the room coming in over its rim, where they swell up into its ball of ink
(4 s in all, `BeginGather`). What it finds there makes it: for each share of the level's water still inked it stands up
to 60% bigger, with up to 150% more health and up to 100% harder blows. The more she cleared on her way, the less it is.
Not a dot is left on the level afterwards.

Switched off in the adventure (their code kept, to bring back one by one): pufferlings, Blightroots, Blotters,
Lashjaws, Thumpsquirts, dens and murkling budding. Every level has a light spring (docs/CORRUPTION.md: a god ray onto
the seabed, a store of healing she may come back to, too bright for any murkling), a second by her luck
(docs/PEARLS.md, "Luck"); each keeps the seabed under it (§5).

## 5. The abyss reef

- **The seabed sinks with the menace:** the open floor between the walls drops away below her plane (`AbyssReef`),
  the deeper the greater the menace: 4 m down at Menace 0, sinking evenly to 26 m at Menace 1 and no further (about
  4.5 m deeper each depth). The walls carry on down, steepening, to the floor's depth 4 m out from the rock on the
  shallowest floor, 8 m on the deepest. Caves and tunnels (and their mouths) keep their floors; the way down keeps its
  own drop. The flora, kelp and buried coins of the dropped floor go with it.
- **Sand dunes** cover the dropped floor, taking over as the walls' feet give onto it: irregular ridges across a wind
  rolled per level, 7 m from crest to crest, each with a long gentle back and a short steep face (a quarter of it),
  up to 1.8 m from crest to trough on the shallowest floor (half that on the deepest). Their lines wander and their
  crests rise and die away patch by patch, and a finer set (a quarter as high, 3.5 m apart) runs across them at an
  angle. Seen clearly on the first depths; the deep water's blur takes them further down.
- **Reef shelves:** here and there a rounded ledge of rock juts from a wall just under her plane (1.2 m down, 3 m
  across, its rim falling away into the deep), at least 12 m apart, none within 20 m of her start. The anemones grow
  only on these.
- **Everything at her plane:** the dots, the fish and the murklings swim there.
- **Life in the deep** (`AbyssView`, cosmetic, seeded per level), seen only through the water's blur and murk:
  - **Kelp fields** rise from the deep floor (where it lies 19 m down or more, clear of the way down: only on the
    deepest depths): up to 14 a level, 15 m apart at least, 3.5–7 m across, their strands 11–17 m tall (tallest in the middle), their tips never
    higher than 8.5 m under her plane. Fronds up either side, swaying with a slow swell that rolls through each field.
  - **Big fish** cruise round her as dark shadows 12–17 m down, only where the floor lies deep enough to clear them:
    five at a time, long fish (5–8 m, the tail swinging)
    and rays (3.5–5 m long, their wings beating). Each comes in 18–30 m from her over the deep, crosses near her, turns
    away from rock ahead, and fades out after 20–40 s or once 40 m away; another comes in elsewhere.

## 6. Her oral arm and the HUD

- **Her oral arm** stings: it trails her along the way she swam (her last 1.5 s of it, so it gathers in when she
  stops), 3.2 m long and 0.2 m longer for every skill point she spends (at most 6 m). A corrupted creature it brushes
  (a murkling, an Inkwarden) takes 30% of a shot's damage, once every 0.4 s.

- **No stamina:** she shoots, dashes and surges whenever she likes; only the dash's own cooldown holds it back.
- **Her gauge** (top left): Clementine seen from above. Her bell, a disc of glass with her four gonads and her glowing
  rim (static); holding an active pearl, its colours and pattern fill the glass (no pearl is drawn), whitening for a
  beat when she uses it. Under it three of her oral arms hang, one stat each, left to right by urgency, each as long
  as its stat is full, over a faint track of its whole length, with a small sign at its foot:
  1. **health** (warm white-gold; a heart) — a hit frays the lost piece off the arm, drifting away; low (under a
     quarter), it turns ragged and pulses rose;
  2. **the active pearl's charge** (the pearl's own colour; a pearl) — breathing when ready; without one, the arm is
     curled up short and dim;
  3. **experience** (amber; her level at its foot) — growing toward the next level, pulsing full with a point to
     spend.
  Beside the bell **her shells** (the shell and its count — shown only once she has collected at least one).
- **Maps:** the minimap and the whole level's map (Tab / Select) both hidden unless she has absorbed **Sea Chart Nautilus** ("Remembers every current"), which she starts every run with for now.
  Never an experience ring or health bezel round it: a thin outline and the north mark only. The cleansing meter is
  gone.
  The whole level's map is cut out of its square along the level's outline — all its water and a few metres of rock
  round it, seen or not (the unseen under fog) — its edges fading softly into a dimming of the game, framed as large as
  it fits; no legend. Over it, the level's
  template name, its depth and level, and the run's clock.
- **Vent dots:** top centre, one per leak — dark with a violet glint while it pours, gold once sealed. The level's
  title and the banners sit under them.
