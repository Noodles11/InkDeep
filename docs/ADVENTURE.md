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

- **What:** brittle stars of ink floating at her plane, always 5 a level (`PlaneLeak`, `LeakArm`). They lie in the
  middle of chambers; failing that on side routes; failing that in any open water; only as a last resort on the main
  current. At least 30 m apart if the level allows it and 24 m from her start, never in safe places or at the way down.
- **Its body:** a dark disc about 0.9 m across where the ink comes through, five soft lobes toward its arms, hot air over
  it in the post pass. Five arms reach out from it, a fifth of a turn apart, each 2.5 m + 3.5 m × Menace long (no
  longer than at Menace 1), tapering from 0.28 m wide at the disc to 0.08 m at the tip. They are black ink in little
  plates with a fringe of short spines, and a violet rim that brightens on its beat (every 3 s, twice as often with her
  within 10 m) (`leak.gdshader` the disc, `leak_arm.gdshader` the arms).
- **The arms float and wiggle:** each arm bends at a joint every staple's stretch, a little more toward its tip, on a
  slow wave of its own, and the arms bob gently as they drift. A stapled stretch cannot move.
- **The arms sting:** touching an arm where it is not stapled stings her for 6 and throws her off at 7 m/s. A stapled
  stretch is harmless.
- **Shooting an arm:** a shot that meets an arm pops on it. On a free stretch it bends the arm away from the shot (sprung
  back over about a second); a stapled stretch only stops the shot. Shots do nothing to seal it.
- **No push:** a leak does not push the water. No creature comes nearer its disc than 2.5 m, that is all.
- **The only thing that heals.** Every other creature's and boss's meter stays where her light leaves it.
- **Sealing:** her inner light does it, no shots, while she stays within 6 m of its disc, which means coming in among
  its arms. Gold light-staples go in one every 0.5 s, evenly spaced every 0.6 m along an arm (at least three an arm),
  from the disc out, one arm after another; so longer arms take more staples and longer (about 10 s at Menace 0, 25 s
  at Menace 1). Each staple snaps in with a clear note rising as the leak fills. As each arm is stapled shut all along
  it spits a ring of 8 ink drops at 60% of a throw's damage. The last staple seals it, and its arms and staples dissolve
  together.
- **The burn while sealing:** where the staples hold an arm its edges sear as her light burns the corrupted: bleached
  to a pale crust lit in her colour, crazed with bright seams, a hard bright seam where the searing ends. While she is
  sealing, the burn reaches further in and burns brighter. The disc pales toward her gold from its rim as it fills.
- **Her coming near rouses it:** healthy fish within 14 m turn at once.
- **If she leaves:** after 1.5 s it heals, a staple every 0.4 s, last in first out; the last one trembles first.
- **On the maps:** a leak cave's leak shows as a purple five-armed star (`TopoMap.DrawLeakMark`).
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
Lashjaws, Thumpsquirts, dens and murkling budding (except in the open reef's leak caves, whose only creatures are
murklings and primes: docs/RPG-PROPOSAL.md). Every level has a light spring (docs/CORRUPTION.md: a god ray onto
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

## 7. Crab Deals

Rare rooms where an old crab trades its best pearls for a share of her. The sim is in `src/Core/Plane/PlaneCrab.cs`
(`CrabTuning`), the presentation in `src/Game/TopDown/CrabView.cs`.

- **Rare:** past the first depth, 60% of depths hide one on a level short of the boss, at most one a depth.
- **Hidden behind a fake wall:** plain rock, no carving. Her shots burst on it, and she simply swims through (it gives way
  as she touches it). Its clues: a thread of bubbles from the seam and a faint clicking, both within 14 m. Nothing but her
  opens it, and the room shows on the map only once she has been in it.
- **The deal:** two pearls, one of them found only here. She stays at one for 1.5 s while a ring fills (leaving cancels),
  and the other crumbles. Its description floats by it.
- **The price:** when the pearl is absorbed the crab's claw cuts her arms: 25% of her max HP gone for the run. It cannot
  kill her, and it is refused if it would leave less than 40.
- **Her max HP is her arm's length:** 3.2 m at 100, in proportion, between 1.5 m and 6.5 m. The cut shortens the sting and
  the health arm on the gauge alike. Every skill point spent adds 6 max HP.
- **The crab's own pearls:** Severed Nerve (a strand circles her, stinging as her arm does), Molten Carapace (under half
  health her shots pierce and do 60% more), Last Light (once a level, a killing blow leaves her at 1 HP in a flood of light),
  Claw Tithe (each cut she has taken adds 25% damage).

## 8. Her light

Her light is a lamp in her bell, one sphere of it all round her, and it burns nothing. Her beam and her shots are
what burn the corrupted. Caves are dark. The sim is in `src/Core/Plane/PlaneHerLight.cs` (`HerLightTuning`),
`PlaneLight.cs` (the light emitters, `LightTuning`), `PlaneAsh.cs` (`AshTuning`) and the shot rules in
`PlaneCombat.cs`; the presentation in `BellView` (her light), `BeamView` and `her_beam.gdshader`,
`her_burn.gdshaderinc`, `BlightView.Burn`, `CombatView` (the shots' lamps), `ReefLook.Cave`, `GlowPlankton` and
`AshView`. Tests: `HerLightTests`, `BeamTests`, `LightEmitterTests`, `LightQueryTests`, `CaveAshTests`.

- **Light emitters** (`PlaneWorld.Lights`; docs/LIGHT-AND-GRAPH-PROPOSAL.md §1): her aura, her beam and each of her
  shots in flight are emitters in one registry. Brightness at a point is Intensity × (1 − d / Range)^1.6, × a cone's
  angle falloff, nothing through rock. Each emitter has an owner that pays when something drinks its light; her aura
  and her beam are both hers, so both drain her one pool of HP.
- **Her light is her HP** (`BellView`): her bell and her lamp dim as her HP falls (barely while she is well, fast near
  the end) and go out at 0. Nothing mends her on its own (only the Coral Polyp pearl, a haven, a sealed leak cave).
- **Her beam** (`PlaneWorld.Beam`, held on the left mouse button, the arrow keys or RT): a cone from her along her aim
  (16 m × √ her glow stat, half-angle 22°), whichever way she swims. Shining costs her nothing. Rock stops it
  (`BeamEnd`). It does what her shots' light does, continuously:
  - burns every corrupted creature some of whose body lies in its cone, in sight of her, as `BeamShots` (2) plain
    shots would, and opens a boss where a shot would;
  - dazzles a creature once as it comes into it, as a shot does (held in it, it is not dazzled again);
  - fills a sealed wall's carving its axis falls on with `BeamShots` shots' light a second;
  - clears the ink dots along its axis (`BeamDots`, 0.5 m) and the ash it falls on.

  The view draws it as a fan of her light over her plane and a spotlight from her bell along it (`BeamView`); the
  bodies it burns sear as under a shot's light (`her_beam_at`, `her_beam_dir`).
- **Her pearls on the beam** (`LightTuning`, `PlaneWorld.RollBeamPearls`, `StepArcs`; `BeamPearlTests`):
  - her range stat lengthens it and her shot size widens it (each × its square root; at most 35° either side);
  - Sunbeam makes it a searchlight: half as long again, 15° either side;
  - Rear Fin shines a fainter beam out of her back while hers shines, and Lighthouse turns a fainter beam slowly round
    her (0.6 rad/s) all the time; each burns as one shot, and the corrupted show in them too;
  - Electric Eel Tail arcs what her beam burns on to the 2 nearest corrupted within 4 m (in sight), burning them at
    half the beam's rate; the view draws the arcs;
  - her chance pearls roll once for each whole second her beam keeps burning a creature: Frost Kelp holds it still
    1.6 s, Fire Coral (3 s) and Sea Nettle (4 s) leave it burning on as one shot once out of her light, Stingray Barb
    slows it to half pace 2 s, Siren Song charms it 3 s (it does not see her light), Giant Squid Eye burns it × 3 for
    a second;
  - the Anglerfish Lure makes a shot of hers left hanging 2.5 × as bright: a decoy the murklings go for.
- **Her shots** are on the right mouse button (RB on a controller).

- **Her light** (`BellView`): a bulb's, smooth all round her from her bell's heart (DESIGN-TOPDOWN §5.3), reaching
  11 m (× √ her glow stat and the depth's lantern: Lantern Pearl reaches farther) and casting soft shadows. It lights
  the reef round her and clears the ash off a cave's walls (below), but burns no creature.
- **Her shots' light burns the corrupted** (`PlaneWorld.StepHerLight`):
  - **Where:** any corrupted creature with some of its body within a shot's light (3.4 m for a plain shot, × √ its
    share of a plain shot's damage, as its lamp is drawn), with nothing solid between it and the shot. A Hearthguard
    on its bed under her plane burns as a shot passes over it.
  - **Burn:** it loses 3 corruption a second for each shot lighting it (× her damage stat), and is freed when none is
    left. It adds to the shot's hit: what she fires at burns as the light flies past and hangs over it.
  - **Not a hit:** the burn is steady. No flash, no damage numbers, and it does not rouse an Inkwarden. A guarding
    Inkwarden turns the burn aside as it turns her shots.
  - **Bosses:** a boss burns only where it lies open to the light (its pearl, or Drippus Prime's whole body).
- **The look of the burn** (`her_burn.gdshaderinc`, in `murkling.gdshaderinc`, `corruption_orb` and `flounder`):
  - **Which lights:** the first four shots burning something (`PlaneWorld.Burners`), each sent to the shaders as
    `her_burn0`–`her_burn3`: where it hangs and how far its light reaches.
  - **Where:** only the side of a body facing a shot, within its light (a little more from above, so a body is lit over
    its top, where the camera sees it).
  - **Not fire:** the ink bleaches and boils off. Its black thins to a pale, glassy crust lit from within in her colour,
    molten in its cells and crazed by a crawling net of white-hot seams, fizzing with tiny boiling specks.
  - **The edge:** where the burn ends, the crust meets the dark ink in one hard bright seam, torn a little, with a
    scorched dark band just past it. A body half in a shot's light shows the light's edge across it; a body wholly in
    it shows the edge where its lit side turns away from the shot.
  - **Particles** (`BlightView.Burn`): from the side facing the nearest shot lighting it, white-hot specks boil off and
    rise as they cool to her colour. Flakes of bleached ink peel away and sink, and now and then a pale wisp rises.
- **It dazzles:**
  - **Who:** a murkling, an Inkwarden or a Hearthguard coming into a shot's light.
  - **The freeze:** it stops dead for 0.8 s and lets go of a throw it was winding up; an Inkwarden's spin and spikes
    and a Hearthguard's sting pause where they are. Then it carries on in the light.
  - **Again:** only once it has been out of every shot's light for 2 s.
  - **Not dazzled:** the older rooted and leaping creatures (Blightroots, Blotters, Lashjaws, Thumpsquirts) and bosses.
  - **The look:** one sharp glint on the side facing the shot and a ring of sparks off it.
- **Her shots are lanterns** (`PlaneCombatTuning`, `CombatView`):
  - **Flight:** thrown at 20 m/s, a shot eases out to about the end of her range, 8.3 m. It floats 0.12–0.22 s (× her
    bubble hover stat; with Bubble Coral 0.8–1.2 s) and pops, and its light goes with it.
  - **Its light:** each shot is a real light on the reef round it, as well as a glow on the water. A plain shot reaches
    3.4 m. Both its reach and its strength grow with the square root of its share of a plain shot's damage: a full
    Pearl Diver charge shines about 1.7 times as far, and a shot split from a five-shot volley about 0.6 times.
  - **Use:** her shots light the dark around her and burn what their light reaches, as well as striking.
- **Caves are dark** (`ReefLook.Cave`, for every leak cave):
  - **Light:** no sun, no rays or caustics, and her lantern at the Abyss's strength. Never black: a faint cold shade
    (ambient #4A5688 at 0.32) leaves the outlines of the rock and of what swims there just visible, all but hidden. The
    cave's reef wears the Abyss's colours, dulled, so what her light finds there is stone and dim life, not neon.
  - **What shows:** clearly, only what light falls on. The ink dots in the water are seen only where her light or her shots'
    reach them (`reef_dark` in `ink_dot.gdshader`), and the water's plain motes are gone.
  - **Ash on the walls** (`PlaneAsh.cs`, `AshView`, `ash_flake.gdshader`): a cave's corruption on its rock. Washed-purple
    scraps like ash cling to every wall like torn wallpaper: a ragged outline with one straighter torn cut, pinholes, a
    fibrous grain, darker edges and a faint violet sheen. They lie in patches 0.9 m apart along the walls, at three
    heights from 1.6 m under her plane to 1.6 m over it, ten scraps a patch. They are lit as the rock is, so they show
    only where light falls on them.
  - **Clearing it:** her light clears a patch within 6 m of her (× √ her glow stat), in sight of her, in 0.6 s, its
    scraps going one by one. Each peels off in a twirl of magic particles ("+" and "*") and a flake of ash that drifts
    down and fades. A shot of hers clears the patches within 0.9 m of where it pops (× √ its share of a plain shot's
    damage), all at once. Sealing the cave's leak dissolves what is left, in a wave out from the leak at 8 m/s.
  - **Over the ways in:** it lies thickest over a secret's sealed wall and a crab's fake one, patches 0.45 m apart all
    over the face she would meet it from. Under it the wall's carving is hidden (`SealView`, `PlaneWorld.SealAsh`), and
    shows again as the ash comes off, so clearing the walls is how she finds the ways in. Clearing ash earns nothing
    else: no experience, no shells, and the leak needs none of it cleared.
  - **Plankton** (`GlowPlankton`): 120 tiny specks of fluorescent plankton drift in the dark at random, mostly cyan,
    green and blue, now and then a pink one. Each glows up and fades on its own clock. They are the only light in a
    cave that is not hers.
- **Verification:** `--dbg-burn` holds her still where she starts, firing, with murklings round her: two 4 m off and a
  prime 7 m off, burning as her shots' light reaches them.
