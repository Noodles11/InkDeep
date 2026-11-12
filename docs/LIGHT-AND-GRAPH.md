# Light as life, and the dive as a graph (proposal)

The plan for Ink Deep's next shape, fitted to the game as it is (docs/DIVE.md: a dive of seeded levels, havens, leak
caves, folk bosses). Light comes first: it is what the murklings hunt, what she lives on and what frees the corrupted.
The dive turns from a line into a graph after that. Values marked *(tune)* are starting points.

Decided:

- The dive becomes a graph of reefs (§6).
- The murklings' extra behaviour (throws, fish hunting, primes) is kept, but only on variants; the basic murkling does
  none of it (§3). Requests and chains that count primes keep working.
- The corrupted are invisible outside her light, at every depth, the sunlit shallows included (§4).
- The beam strikes seals, clears ink and ash, and opens bosses, as her shots do. Leaks are sealed as they are now: by
  her own light, staying close to them (§2.3).

## 1. Light emitters

Everything that shines in the sim is a **light emitter**: her aura and her beam, her shots while they last, a lure
pearl's decoy, and later glowing plants and objects. One registry holds them all (`PlaneWorld.Lights`), and every
rule that asks "what is lit here, and how brightly" asks it, not the owners one by one.

An emitter has:

| Field | Meaning |
|---|---|
| Id, Owner | a stable number, and what it belongs to (her, a shot, a plant) |
| Shape | a point (shines all round) or a cone (Direction, HalfAngle) |
| Position, Direction | where it is and, for a cone, where it points |
| Intensity, Range | how bright at the source, and how far it reaches |
| Colour | warm gold for hers; others by owner |
| Active | an inactive emitter is in the registry but lights nothing |
| Drinkable | whether a murkling can feed on it |

What a murkling drinks from an emitter is taken from its **owner** (`ILightOwner.Absorb`): her aura and her beam
belong to her, so both drain her one pool of HP; a plant withers; a decoy goes out. When the owner is spent it says
so, and the drinker lets go.

**Brightness at a point.** For a point emitter: `Intensity × (1 − d / Range)^1.6` (0 past Range). A cone multiplies
that by its angle falloff: 1 within 70 % of HalfAngle, easing to 0 at its edge, 0 outside it. Rock between the
emitter and the point blocks it (the sim's `LineOfSight`).

Starting values *(tune)*: aura reach 5.5 m, intensity 1; beam reach 16 m, half-angle 22°, intensity 1.2; a plain
shot of hers 3.4 m (as now), intensity 0.4 to 1.3 by its bubbles (as now).

## 2. Clementine: aura, beam, HP

### 2.1 Light is her HP

Her HP (`Player.Hp`, `Run.MaxHp`) stays the number the game runs on; it is now *how much light she has*. Her bell
dims as it falls and goes out at 0, which is the end of the dive (her light leaving her: docs/DIVE.md §2). Nothing refills it on its own: only cores (§4.2),
the Coral Polyp pearl (slow), havens and a sealed leak cave (full, as now).

Her aura and beam cost nothing to shine. They are what the murklings drink: a murkling feeding on either takes from
her one pool.

### 2.2 Aura and beam

- **Aura**: a point emitter on her, always on.
- **Beam**: a cone from her bell along her aim, on while the beam button is held (the left mouse button, the arrow
  keys, RT). It points where she aims, not where she swims. Turning it off is a choice: the beam is the brightest
  thing she has, and a murkling goes for the brightest light.
- While pearls move from shots to the beam (§5), shots are on the right mouse button (RB).

### 2.3 What the beam does

The beam does what her shots' light did, continuously, and **by how much of its light reaches the thing**
(`PlaneWorld.BeamLight`): `NearLight` (2) shots' worth at her bell, falling to `FarLight` (0.25) at the end of its reach
(by (1 − d / reach)^1.4), a little less toward the cone's edge. **A creature's body in the way stops it** as rock does,
so the one nearest her shades those behind; Swordfish Bill (her shots pierce) lets it through them all.

- **Burns** the corrupted it reaches, at `Burn` per second × her damage stat × the light reaching them, freeing them
  when their meter is empty.
- **Dazzles** a creature once as it comes into the light, as shots do: it holds still for `Dazzle`, then is not
  dazzled again until it has been out of all her light for `Rearm`. A creature kept in the beam is not frozen for
  ever.
- **Strikes seals, clears ink dots and ash**: a sealed wall's carving its axis falls on gains `BeamShots` *(tune: 2)*
  plain shots' light a second × the light reaching it; the ink dots along its axis and the ash it falls on clear.
- **Seals leaks** as her inner light does: `LeakBeamShare` (half) of its burn on the leak's disc, the more the nearer
  her. Her inner light near a leak works on it too, at 1 a metre inside her reach rising to 1.8 right over it; the two
  add up.
- **Opens a boss** where a shot would (its pearl, while open). Each boss gains one light rule of its own in stage 5,
  for example a hold time, the beam's angle, or a lit object in its arena.

The aura burns nothing (as her lamp does now). It reveals what is near her (§4).

### 2.4 Her glow put out

She can put her glow out and light it again (Ctrl; RB on a controller; `PlaneInput.ToggleGlow`, `PlaneWorld.GlowOff`),
each time with a lamp's pull-chain click heard under water. Out, she has no aura: her bell is all but dark and the reef
round her with it. With her beam dark too she is **hidden** (`PlaneWorld.Hidden`): the Leak's creatures lose her, as in
her Ink Cloud (`Unseen`), and a murkling beside her never finds her. But she sees little more than they do (the
murklings show only in her light), and her inner light seals no leak while it is out. Her beam gives her away the
moment it shines.

### 2.5 Charge stones

Small clusters of coloured crystal shards are set in the reef's walls at her plane (`PlaneChargeStones.cs`,
`ChargeStoneTuning`; drawn by `ChargeStoneView`): about sixteen a reef, 9 m apart at least, each in its own colour (cyan,
magenta, amber, lime, violet, ice blue). Dark, a faint glint shows where one is.

- **Her beam charges one**, the faster the more of its light reaches it (`PlaneWorld.BeamLight`): a second of beam at
  one shot's light gives 5 s of glow, up to 14 s.
- **Charged, it glows on its own**: full while it holds more than 5 s of glow, then dimming over those last 5 s, and
  out. Its light is round it, as hers is, fainter even at full (half her aura's), and reaches about 4.5 m over the reef.
- **It harms nothing**, but it is a light like any: a murkling may go to it instead of her (her glow out, or the
  stone brighter where it is) and drink it down. A lure, and a lamp for the reef.
- **It shows what is in its light**, as her glow does (`PlaneWorld.ChargeStoneSees`): a murkling within most of its
  drawn light is seen fully, thinning at its edge, and less as the stone dims; not round rock. So she can watch what a
  lure has drawn in.
- **A trio** (`ChargeTrio`, 70% of levels; none in a labyrinth): three charge stones of one colour in the walls round
  an open nook, at most 6.5 m from its middle and far apart in angle. On the sand in the middle lie three shards of
  their colour in a triangle, each pointing to its stone and burning while that stone glows. With all three glowing at
  once (14 s of glow each gives her time to go round them), a secret cache of 12 shells bursts up from the sand there
  with a burst of light, once; the shards then lie spent and dark.

### 2.6 Prism stones

Great standing stones, menhirs of rough grey rock taller than her plane (`PlanePrismStones.cs`, `PrismStoneTuning`;
drawn by `PrismStoneView`), each with a crystal band at her plane and a crystal crown in its colour: **red, green or
blue**. Dark, the crystal glows faintly so its colour can be read; while her light goes through it, it burns bright and
lights what is round it in its colour.

- **Coloured seals.** Up to two of a level's secret seals (85% of them, outside the tutorial) have their X carved in a
  colour, glowing faintly in it. Such a seal takes only her light of exactly its colour (`SecretSeal.Hue`); her own
  light and any other colour do nothing to it. Plain seals still take any of her light.
- **The bed.** Before each coloured seal, 4 to 12 m from its X and in sight of it, a round of open sand 3.4 m wide is
  ringed with pale pebbles. In it stand the stones that make the seal's colour, and 60% of the time one more of a colour
  it does not need, to choose among.
- **Pushing.** She pushes a stone by swimming into it. It is heavy: it gives at most 1.6 m/s and takes most of her push
  off her. It never leaves its bed, and it stops at rock and at the other stones.
- **Light through a stone.** Her light passes through a stone (within 0.8 m of its middle) and comes out of its far side
  in its colour. Through more than one it mixes as light does: red and green make yellow, red and blue magenta, green
  and blue cyan, all three white (`LightHue`, `PlaneWorld.HueAlong`). So a seal's colour is made by putting its stones,
  and only those, between her and its X; a mixed colour needs two stones in line. Each ray of a split beam has its own
  colour.
- **Drawn.** Each line of her beam that goes into a stone lands on it (the flashlight's cone stops there, its edge
  tracing the stone). Out of the stone a band of its colour goes on between the lines that carry it, brightest at the
  stone, softer at its sides and toward its end (`BeamView`). Where her own beam's axis lands, a light of that colour
  falls on the rock.

## 3. Murklings

### 3.1 The basic murkling

A ball of ink that hunts light. It has four states:

| State | What it does | Leaves for |
|---|---|---|
| Searching | holds still; scans the emitters every 0.2 s | Approaching, when it finds one above the threshold |
| Approaching | swims toward the brightest emitter it found, rescanning every 0.2 s | Attached on contact; Searching when nothing is left |
| Attached | clings to the emitter's owner and drinks at its steady rate; does not scan | Released when shaken off (dash) or when the owner is spent |
| Released | tumbles where it was shaken off for `ShakeSeconds` | Searching (it scans at once) |

- The scan looks at emitters within 20 m *(tune)*. It measures each one's brightness at the murkling, drops those
  below 0.04 and those behind rock, and picks the brightest.
- It reaches its target by swimming (the sim's navigation), never through rock.
- It drinks 2.5 HP/s *(tune)* from any owner; there is no contact hit on top.
- It does not go back to her when released: it goes for whatever is brightest then.
- It acts on its own: no packs, no merging (merging is for a later variant).

### 3.2 Variants

The current murkling's extras move to variants, rolled by depth:

- **Thrower**: winds up and throws ink, as now.
- **Hunter**: hunts fish, as now (the corrupted-fish loop).
- **Prime**: as now: big, rams, breaks into murklings when freed.

Depth 1 has basic murklings only *(tune)*. Deeper, the share of variants grows. Requests and chains that count primes
keep their meaning.

## 4. Seeing them, and freeing them

### 4.1 Invisible in the dark

The corrupted give off no light of their own (their view lights go). Their bodies are drawn only where her light
reaches: the aura round her, the beam along it, a shot's glow. A murkling half in the beam shows its lit half. This
holds at every depth, the sunlit shallows included: there the ink reads as a shimmer where her light falls on it.

The view does this with the existing sear (`her_burn`): the body's visibility at each pixel is her light's brightness
there.

### 4.2 Freed light: the core

When its meter runs out, a murkling's ink shell splashes apart and its **core**, the light it had drunk, hangs where
it was:

- It fades over 1 s *(tune)*.
- Touching it gives her `CoreLight` HP *(tune: 4 for a murkling, 20 for a prime)*, never past her max.
- The Remora Sucker (magnet) widens how far she gathers cores from, and shells too.

This is freeing, not killing: the light was hers or the reef's, and it comes back.

## 5. Pearls

The catalog stays (`data/items.json`, `ItemCatalog`, `Loadout`). 43 pearls change her shots (by a stat or by the
shape of a shot), and 20 synergies are built on them.

**Her beam is her one attack.** Her shots are off in the game (`PlaneOptions.Shots`; the sim still has them, for its
tests and for what may yet be built on them), and there is no shoot button. So a pearl that works only on her shots
(marked **Shot** below) does nothing for now, till it is carried over to the beam or taken out of the pools; the ones
marked **Beam** or **Beam too** work as written.

### 5.1 Each shot pearl (built)

| Pearl | What it does now | Proposed |
|---|---|---|
| Coral Crown, Glass Squid, Shark Tooth, Abyssal Glow, Pufferfish Pout, Cannonball; Neon Rave, Shark Mode, Pirate | + damage | **Beam too**: the beam burns faster by the same stat (it already does). |
| Whale Lung, Cuttlebone | + range | **Beam too**: the beam reaches farther (× √ of the range stat). |
| Whale Lung, Cannonball | + shot size | **Beam too**: the beam's cone widens (× √ of the size stat, at most 35°). |
| Sunbeam | her shot is a laser | **Beam**: the beam reaches half as far again and narrows to 15°: a searchlight. Its shot laser goes. |
| Lighthouse (Sunbeam + Nautilus Spiral) | the laser sweeps a spiral | **Beam**: a second, fainter beam turns slowly round her. |
| Electric Eel Tail | shots chain to 2 more foes | **Beam too**: what the beam burns arcs its burn to the 2 nearest corrupted within 4 m, at half the rate. |
| Rear Fin | a shot out of her back too | **Beam too**: a second beam behind her at half its strength (a murkling sees it too). |
| Frost Kelp, Fire Coral, Sea Nettle Sting, Stingray Barb, Siren Song, Giant Squid Eye | a chance per hit (freeze, burn, poison, slow, charm, crit) | **Beam too**: rolled once a second for each creature the beam keeps burning. |
| Anglerfish Lure | shots home | **Shot, and the decoy**: her shots still home, and a shot of hers left hanging shines 2.5 × as bright and can be drunk: murklings go for it instead of her. (Guided Inkfish and Will-o'-Wisp keep working.) |
| Plankton Swarm, Triple Tentacle, Double Helix, Hammerhead, Kraken Form | more shots, patterns | **Beam**: her beam splits into rays (docs/proposals/BEAM-PEARLS-PLAN.md) |
| Squid Ink Espresso, Plankton Swarm | fire rate | **Beam**: its tick rate (docs/proposals/BEAM-PEARLS-PLAN.md) |
| Cuttlebone; Pufferfish Pout, Cannonball (drawbacks) | shot speed | **Beam**: its carry, the light kept far out |
| Nautilus Spiral, Mirror Scale, Swordfish Bill, Ghost Jelly, Mitosis, Boomerang Shrimp, Starfish Arm, Sardine Run | the shot's path or body | **Shot** |
| Pearl Diver, Starburst Gland | a charged pearl; a burst where it pops | **Shot** |
| Cannonball, Captain's Hook | knockback | **Beam**: light pressure, pushing what is in it away |
| Bubble Gland, Anemone Pump, Twin Siphon, Bubble Coral | the shots' reserve and hover | **Shot** |
| Diver's Watch | enemy throws slower | unchanged (not hers) |
| The other synergies (Pinball Storm, Cell Bloom, Guided Inkfish, Steam Vent, Tuna Rang, Pearl Necklace, Prism Pearl, Black Tide, Big Mad Puff, Will-o'-Wisp, Toxic Bloom, Feeding Frenzy, String of Pearls, Depth Charge, Broadside) | built on shot pearls | **Shot**, as their pearls |
| Supernova, Lance of Light, Helix Lamp, Searchlight | Firefly Squid's surge | unchanged (the surge is its own) |

The new effects go on existing pearls:

- The Anglerfish Lure is the **decoy** (above).
- The Remora Sucker is the **magnet** for cores (built).
- The Brass Compass and the Sea Chart Nautilus between them are the **cartographer**, showing the graph (§6.3).
- The Coral Polyp stays the slow mend of her light.

### 5.2 One light rule per folk boss (built)

Each folk boss keeps its fight (docs/FOLK.md) and gains one rule that asks more of the beam than
holding it on the pearl:

| Boss | Its rule |
|---|---|
| Sleepy Old Tull (Depth 1) | **Hold it steady.** Her pearl burns only once the beam has stayed on it 0.5 s without leaving it: in her 2 s sigh, a sweep does nothing. She feels where the beam was last shone, as she feels shots and dashes. |
| Nine-Fin Kip (Depth 2) | **From the side.** Her pearl on her stripe burns only when the beam meets it within 45° of square-on to her flank, so the beam must come from beside her when she turns side-on. |
| Papa Fennel (Depth 3) | **Find him.** He is invisible among the kelp, as the corrupted are, until the beam falls on him; lit, he unfurls (his fight's opening), and stays lit for the round. |
| Many-Armed Ottilie (Depth 4) | **A brighter light.** Two lamp-shells in her hoard are light emitters. Lit by the beam, one glows brighter than Clementine for 3 s, and Ottilie turns to admire it, carrying the pearl round with her: her body no longer shields it. |
| The Pale Dragonet (Depth 5) | **The beam's heart.** Her ink sacs burst only under the middle of the beam (its inner half-angle), not its edge: the beam must be aimed, not swept. |

Drippus Prime and Queen Clam keep their rules (the whole body; the pearl while the shell is open).

## 6. The dive as a graph (built)

### 6.1 Shape

A dive is a layered graph of reefs, built from the dive's seed a depth at a time as it is reached (`DiveGraph`,
`DiveTuning`, in `src/Core/Run`; the same from the same seed):

- **Depths**: the five depths, as before. Each has 4 rows of reefs and ends in its folk boss's reef, one wide (row 5).
  The tutorial is two reefs, one wide; the very first row of the dive (where every dive begins) is one reef.
- **Lanes**: each row has 2 to 4 reefs. Each reef has 2 to 4 holes down to the nearest reefs of the row below (fewer
  only where that row is narrower, or is the boss's); the boss's reef leads to every reef of the next depth's first row.
  Now and then (15%) a reef has a **shortcut** past the row below to the one after (never to the boss's).
- **Menace** comes from a reef's depth alone, never from how many reefs she has crossed. A shortcut is a reef fewer
  for her pearls and shells, not a harder one.
- **Rules the generator keeps** (it retries the depth on its next sub-seed if one breaks):
  - every reef can be reached from the top, and leads on to its depth's boss;
  - no two holes of a reef lead to the same reef;
  - from every reef of a depth's first two rows, a haven of the depth can be reached (§6.2);
  - no row two wide or more is all havens.
- Past the fifth depth the loop starts again below the tutorial, as before.

### 6.2 Kinds of reef

| Kind | What it is |
|---|---|
| Reef | an ordinary seeded level |
| Haven | calm, heals and banks, all the freed folk, the Tide Fan, the shop. Its map stays hand-drawn, with a hole in its gate and in each of its two coves (as many as it has reefs below). From row 2 to row 4 of a depth: 15% by chance (never straight after one), then where most of the reefs that could reach none would reach it. |
| Boss | the bottom of each depth, one reef wide |
| Treasure | a reef with a clam room (rows 1–4, 30%; at least one a depth) |
| Den | a reef with a curse den (rows 2–4, 20%) |

Treasure and Den follow one rule each (`ReefPlaces`): the rows they may stand in, how often, how many at least a depth,
and never straight after one of their kind. A reef's template is drawn to fit its kind (none forbidding the room it
holds, none requiring one it does not; a treasure reef may hold the curse den its template asks for too). The spots
inside a reef (a secret, a shop, a shell cache, a Crab Deal on one reef of a depth) are rolled by its plan.

### 6.3 Holes and choosing

- **Opening the holes**: a reef has a hole for each of its exits: its Exit's chamber, and further chambers sunk the same
  way (`PoiKind.Hole`), far from the start and the exit and 28 m apart. Sealing the reef's leak (on a boss's reef,
  freeing the boss) opens them all at once.
- **Which leads where**: left to right across the map as the reefs they lead to lie left to right across the rows below.
- **Showing where each leads**: each hole's rim is a ring of light in the colour of the reef below (`HoleView`,
  `ReefKinds`): plain reef pale water, clam room gold, curse den red, haven green, boss violet. Faint while sealed, it
  brightens and breathes once the holes open. Over a hole the prompt says it too ("dive · a clam room below").
- **The level below**: each hole's reef is made off the main thread, the nearest first; the one under the hole she is
  nearest shows through it, and she dives into whichever she is over.
- **The chart**: the pause menu's Chart tab (`ChartView`) shows the depth she is in and the next depth's first row: the
  reefs she has been on, filled; the one she is on, ringed in gold; the reefs she can see (every hole's reef of a reef
  she has been on, and down to one row below her, two more for each cartographer pearl: Sea Chart Nautilus, Brass
  Compass), in their kind's colour; the rest unknown.

### 6.4 In the code

- **LevelId** has a **Lane**; its Level is the row. A reef's streams come from (cycle, depth, row, lane).
- **The graph** (`DiveGraph.Of(seed)`): `Node`, `Row`, `ExitsOf`, `KindOf`, `Resolve`. `LevelPlan.For` takes a reef's
  kind and its exits from it (`LevelPlan.Exits`, `ExitKinds`, left to right). There is no line of levels any more: where a reef leads is its exits.
- **The map**: `LevelMap.Holes` (each its shaft, the reef it leads to and its kind), `InShaft`, `HoleAt`;
  `PlaneWorld.HoleUnder`.
- **The save**: the dive's save keeps the reef she is on (`SuspendedRun.Lane`) and the reefs she has been on
  (`Reefs`). The graph itself is rebuilt from the seed. An older save is resumed on lane 0 of its row.
- **Unchanged**: the Tide Fan's requests ("seal 3 leaks on the way down") and the chains ("dive to Depth N") count
  depths and kinds, so they keep working.
- **Verification**: `--lane=K` (with `--depth=`, `--level=`), `--at=holeK` (over the K-th hole from the left),
  `--at=rimK` (a few metres south of it), `--pause-tab=5` (the chart).

## 7. Places and sub-levels (built)

- **One rule for each kind of place** (`PlaceRules`): its sort, the first row of a depth it may stand in, how often a
  reef holds one, and how far it keeps from other places. `LevelPlan` rolls them, `CaveLayout` spaces them and
  `LevelValidator` holds a level to them.

  | Kind | Sort | From row | How often | Apart |
  |---|---|---|---|---|
  | Secret | sub-level | 1 | 40% | 17 m (20 m from another cave) |
  | Shop | spot | 2 | 45% | 17 m (20 m) |
  | Shell cache | spot | 2 | 60% | 17 m |
  | Crab Deal | spot | 2 | one reef a depth, 60% of depths past the first | 17 m (20 m) |
  | Clam room | spot | 1 | the reef's kind (§6.2) | 17 m (20 m) |
  | Leak cave | sub-level | 2 | the reef's kind (§6.2), or the template's (a clam-room reef's, from row 2) | 17 m (20 m) |
  | Hole | spot | 1 | one for each exit past the first | 17 m (28 m from another hole) |

  Every place keeps 30 m from the start and from the exit; the early place lies within 38 m of the start. A clam room,
  and a shop are rooms in the reef's rock; a leak cave and a secret are ways into sub-levels (a leak cave; a labyrinth,
  docs/DIVE.md §6.1).
- **Sub-levels are made with their reef**, on the same thread right after it, each from its own sub-seed (its reef's
  cycle, depth, row and lane in it): `LevelShape.Caves`. A sub-level has no sub-levels of its own. A reef under a hole is
  made with its caves before she dives.
- **Coming back**:
  - The reef as it was made is kept while she is below, so nothing is made on the way back; she comes back out of the
    mouth she went in by, swimming out of it, the reef as she left it.
  - A cave she leaves unsealed keeps its state, should she go back in (till she leaves the reef). A sealed one's mouth
    closes behind her.
- **The ride in and out** is a short fade (under half a second of black), the loader showing only if the place is
  not made yet.

## 9. What stays from before

Each of the older systems was looked at once the graph and the light were in. What nothing used went: the line of
levels (`LevelPlan.NextOf`, `PreviousOf`, a plan's `Next`) and the shared stamps of rock round a hole and the start
below it (`Stamp`, the stamp seeds), which the levels no longer shared. The rest stays, each for a reason:

| System | Why it stays |
|---|---|
| Her shots, their reserve and its refill | Off in the game (§5): her beam is her one attack. The sim keeps them (`PlaneOptions.Shots`) till the shot pearls are carried over to the beam or let go. |
| Leaks and the ink's dots | Sealing a reef's leak opens its holes (§6.3): still the heart of a reef. |
| Blightroots and the creatures they raise | The dens' hearts and the templates' casts. |
| Murklings hunting fish, murklings throwing | The variants (§3.2): only they hunt and throw; the basic murkling does neither. |
| The other corrupted kinds' own lights | Until they too are seen only in her light (§8, stage 4). |
| Lantern Bay's hand-drawn map | The haven stays the same place every time (§6.2). |

## 8. Order of work

| Stage | What | Plays differently? |
|---|---|---|
| 1 | The emitter registry, brightness at a point; her aura and her shots registered — built | no |
| 2 | The beam (held), the aura emitter, light as HP (her light dims with it) — built | yes |
| 3 | The basic murkling on the registry; variants by depth — built | yes |
| 4 | Invisible in the dark (built for murklings; the other corrupted kinds follow); cores and healing; the magnet — built | yes |
| 5 | Pearls sorted and moved; one light rule per boss — built | yes |
| 6 | The dive graph, the reef kinds and their place rules, the holes, the chart — built | yes |
| 7 | The rules for the places within a reef; sub-levels made with their reef; the return; short transitions — built | yes |
| 8 | The save for the graph (and older saves migrated); the death fade and the dive's summary — built (docs/DIVE.md §2) | yes |
| 9 | What nothing uses any more goes (the line of levels, the shared stamps); the rest reviewed (§9) — built | no |

Each stage ends with the build, the tests and a play check in the game, and is committed on its own.

## 10. Her light's colours (built)

The first words of the light grammar (docs/proposals/LIGHT-GRAMMAR.md §5). Three pearls tint her light
(`ShotSpec.Ember`, `Bloom`, `Frost`; `PlaneColour.cs`, `ColourTuning`), each copy deepening its tint:

| Pearl | Colour | What it does to the corrupted it lights |
|---|---|---|
| Fire Coral | **ember** (red) | keeps burning 2.5 s after the light has left it (+1.5 s a copy) |
| Glowing Moss | **bloom** (green) | ink and ash clear ×2 as fast in it (+0.5 a copy); a murkling it frees leaves ×1.5 the light (+0.5 a copy) |
| Frost Kelp | **frost** (blue) | slows the longer it is lit (up to 85%), standing still after 2 s (sooner with copies); thaws over 1.5 s out of it |

- **Dealt across her rays** (`PlaneWorld.DealHues`): one ray takes all her colours mixed; a split beam deals them in
  turn from its left (Triple Tentacle with ember and frost: ember, frost, ember), Kraken Form's rays round her after
  it. Rear Fin's and Lighthouse's beams take them all.
- **Mixed on the creature, as light mixes** (`ApplyColour`): the colours of every ray lighting it, of the prism
  stones each passed through, and of a charge stone lit in colour near it. A mix does what its colours do, and more:

  | Mix | Also |
  |---|---|
  | **sunfire** (ember + bloom, gold) | what it frees bursts in a ring of her light 3 m wide (a light burst of one shot's damage) |
  | **shatter** (ember + frost, magenta) | a creature frost has stilled takes the ember all at once: a burst of steam 2.5 m wide (two shots' damage), its chill gone |
  | **lull** (bloom + frost, cyan) | it forgets her (it does not see her) while lit and 1.5 s after |
  | **daybreak** (all three, white) | all of the above, and her burn × 1.5 |

- **Seals**: a coloured ray opens a coloured seal of its colour with no prism stone; through a stone it mixes with the
  stone's colour.
- **Charge stones**: charged by coloured light, a stone glows in that colour and lends it to what is in its light
  (`ChargeStone.Lent`): a lamp of ember, a pool of frost.
- **Named when they meet** (synergies, with the banner): Shatter, Sunfire, Lull, Daybreak.
- **Not on bosses**: a boss takes no colours. Frost stills the creatures a dazzle holds (murklings, Inkwardens,
  Hearthguards) and slows murklings; the rooted and leaping creatures it only marks.
- **Drawn** (`LightColour`): each ray's haze, its spotlight and her light on the reef in its colour (thicker, so it
  shows); a murkling kindled at its rim in the colour on it, frosted and glinting as frost stills it; shatter a burst
  of steam and a snap, sunfire a ring of light and a chime; a charge stone lit in colour glows in it.
- Tests: `ColourTests`.

## 11. Her light's paths on rock (built)

The light grammar's second words (docs/proposals/LIGHT-GRAMMAR.md §4). Where a ray of hers lands on rock, a path pearl
gives it new rays born there (`PlanePaths.cs`, `PathTuning`): real rays of hers (in `HerBeams`) that burn, reveal,
carry her colours (the ray's, and the prism stones' it passed through), charge stones and strike seals. **Light is
conserved**: a born ray's worth is a share of the light its ray had left where it landed (its falloff there against
her bell's), so a lattice is wider and cleverer, never simply more.

| Pearl | Word | Where her light lands on rock |
|---|---|---|
| Mirror Scale | **Bounce** | it reflects off the rock as a mirror would (off a seal or a pot, straight back), 60% of its light, 60% of its reach; each copy one more bounce |
| Mitosis | **Fork** | two short rays fork off it, 70° apart either side of the way a mirror would send it, 30% of its light each, 40% of its reach, narrower |
| Boomerang Shrimp | **Return** | it comes back along its own way to where it left, 50% of its light: what stands in it is lit from both sides (×1.5 between her and the wall) |
| Ghost Jelly | **Ghost** | through a wall of the reef up to 3 m thick (not a seal, not a pot) it goes on beyond, 50% as strong; a wall it passes through is not landed on |

- **One line, each word once**: a born ray takes the other words where it lands, never its own again; Bounce as many
  times as she has Mirror Scales; a Return's ray takes none. At most 24 born rays at once.
- **Met by rule**: Ghost decides first whether the rock counts; Bounce, Fork and Return then all act at once.
- **Named when they meet** (synergies, with the banner): **Cathedral** (Mirror Scale + Mitosis: every bounce forks, a
  lattice of light) and **Hall of Mirrors** (Boomerang Shrimp + Mirror Scale: light caught between walls).
- **Drawn** (`BeamView`): each born ray a soft cone of her light's haze in its colour, without the shadow grid her own
  beams cast, ending at the rock its axis meets, drawn by the square root of its worth so even a faint one shows; a
  glint of light where it was born (a ghost's where it went into the rock). `--dbg-paths` aims her at a wall to see it.
- Her shots (a charged pearl, the surge) keep their own older paths (bounces, splits, the boomerang loop).
- Tests: `PathTests`.

## 12. Her light's rhythms (built)

The light grammar's third words (docs/proposals/LIGHT-GRAMMAR.md §6): what happens while she holds her beam
(`PlaneRhythm.cs`, `RhythmTuning`). They scale what her own beam and its rays burn (`RhythmBurn`, in `HerBeams`), and
so what the rays born of them carry; Rear Fin's and Lighthouse's beams keep their own.

| Pearl | Word | While her beam is held |
|---|---|---|
| Pearl Diver | **Focus** | Held steady (turning under 30° a second), it builds over 1.5 s to burn ×3, its cone narrowing to 45% of its width. Swung faster, it loses it four times as fast; let go, it is gone. A chime at full focus. |
| Sardine Run | **Spin-up** | Held without a break, it builds to burn ×1.8 after 3 s; let go, it spins down over 0.6 s. (It no longer narrows nor weakens her beam.) |
| Pistol Shrimp Claw | **Snap** | While her beam's axis ends on a creature's body, every 0.8 s a snap of light bursts there: 2.5 m wide, 1.5 shots' burn, pushing what it catches back from her. |
| Glow Burst *(active)* | **Pulse** | For 5 s her beam beats twice a second, each beat ×2 at its peak, fading to its next; recharges in 10 s. |

- **Named when they meet** (synergies, with the banner): **Pearl Necklace** (Pearl Diver + Mitosis: at full focus her
  beam's tip opens into a ring of 8 short rays, 12% of the light left there each), **Depth Charge** (Pistol Shrimp +
  Pearl Diver: at full focus the snap is twice as wide and half again as strong), **String of Pearls** (Sardine Run +
  Pearl Diver: spun right up, a swing does not break her focus).
- **Drawn** (`BeamView`): focus turns her own beam's haze and spotlight toward a white-hot thread as it narrows (×1.9
  bright at full focus); spin-up thickens the haze (×1.6 spun up); the pulse flashes it on each beat (×2.2); a snap is
  the bang's shockwave, snap and shake (as Depth Charge's, bigger).
- Her shots (when on) keep the old charged pearl, stream and bang.
- Tests: `RhythmTests`.

## 13. Her light's paths in the open (built)

The light grammar's fourth words (docs/proposals/LIGHT-GRAMMAR.md §4, step 4): what her rays do on their way and
where they end (`PlaneOpenPaths.cs`, `OpenPathTuning`). They act on her own beam's rays (not Rear Fin's, Lighthouse's,
nor the rays born on rock).

| Pearl | Word | What her light does |
|---|---|---|
| Anglerfish Lure | **Lean** | Each ray turns toward the nearest corrupted creature within its reach and near its cone (in sight), up to 10° off her aim, easing at 90° a second. |
| Starfish Arm | **Flare** | Each ray widens along its length, half as wide again at its reach: a trumpet (`LightEmitter.Flare`, `HalfAt`). |
| Bubble Coral | **Linger** | Where each ray ends (rock, a body, its reach), a pool of her light 0.8 m wide stays 2.5 s, burning on at 35% of what her light burned there, in its colours, fading; laid at least 0.55 m apart, so a sweep paints a trail. A creature in a pool shows, burns and takes its colours. At most 90 pools. |
| Electric Eel Tail | **Arc** | Arcs off what any light of hers burns (§5.1), the rays born on rock too; now offered. |

- **Named when they meet** (synergies, with the banner): **Will-o'-Wisp** (Ghost Jelly + Anglerfish Lure: it leans
  twice as far, toward creatures behind rock too), **Pinball Storm** (Mirror Scale + Electric Eel Tail: each glint
  where her light bounced arcs to the nearest creature within the arcs' reach), **Aurora** (Bubble Coral and any two
  colour pearls, the first synergy to need "any two of": her pools hang twice as long, overlapping in their colours).
- **Drawn** (`BeamView`): a leaning ray simply points where it turned (the cone stays straight); a flaring one's haze,
  hull and shadow grid open into a trumpet (her_beam_volume's `flare`); pools are soft discs of her light on the water
  in their colours, swelling as they are laid, fading out (larger and brighter under Aurora); arcs as before.
- Bubble Coral's old shot effects (hover, merging) stay on it for her shots (off in the game) and are not captioned.
- Tests: `OpenPathTests`.

## 14. Her light's echoes (built)

The light grammar's fifth words (docs/proposals/LIGHT-GRAMMAR.md §7): what the light a creature had drunk does when
her light frees it (`PlaneEcho.cs`, `EchoTuning`). Any light of hers that frees it counts (her beam, its born rays,
its pools); a creature freed by an echo (a flare, a light burst) does not echo again: one generation deep. **An echo
carries her colours**: the mix on the creature and hers, lent to what it catches (ember left burning, frost's chill
half-way, bloom and sunfire marked, lull); and **its rays take her path words**.

| Pearl | Word | When her light frees a creature |
|---|---|---|
| Starburst Gland | **Flare** | A ring of her light 3 m wide races out from it: one shot's burn, clearing ink as a light burst does; a chime. |
| Fire Urchin Spine | **Spines** | 6 short rays of hers (3.2 m) shine out from where it was for 0.5 s, half a shot's light each, turned by where it was; they bounce, fork, return and ghost as her light does (rays born on rock, `PathWord.Spine`). |
| Lamprey Mouth | **Gather** | A murkling's core flies to her at once (16 m/s, through anything, never fading on the way), worth 3 more (+1 a copy). |

- **Named when they meet** (synergies, with the banner): **Supernova** (Starburst Gland + Fire Urchin Spine: each
  spine ends in a small flare, 1.4 m wide, half a shot's burn).
- **Drawn**: a flare a gold shockwave ring (smaller and silent at a spine's tip); spines as born rays, with a glint
  where they start; a gathered core streaking home.
- Fire Urchin Spine and Lamprey Mouth are open from the first run (their old unlocks were never earnable on the plane).
  `--dbg-murks --dbg-weak` buds a worn-down row to see it.
- Tests: `EchoTests`.
