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

### 2.5 Light crystals

Small shards of coloured crystal are set in the reef's walls at her plane (`PlaneCrystals.cs`, `CrystalTuning`; drawn
by `CrystalView`): about sixteen a reef, 9 m apart at least, each in its own colour (cyan, magenta, amber, lime,
violet, ice blue). Dark, a faint glint shows where one is.

- **Her beam charges one**, the faster the more of its light reaches it (`PlaneWorld.BeamLight`): a second of beam at
  one shot's light gives 5 s of glow, up to 14 s.
- **Charged, it glows on its own**: full while it holds more than 5 s of glow, then dimming over those last 5 s, and
  out. Its light is round it, as hers is, fainter even at full (half her aura's), and reaches about 4.5 m over the reef.
- **It harms nothing**, but it is a light like any: a murkling may go to it instead of her (her glow out, or the
  crystal brighter where it is) and drink it down. A lure, and a lamp for the reef.
- **It shows what is in its light**, as her glow does (`PlaneWorld.CrystalSees`): a murkling within most of its drawn
  light is seen fully, thinning at its edge, and less as the crystal dims; not round rock. So she can watch what a lure
  has drawn in.

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
| Squid Ink Espresso, Plankton Swarm, Triple Tentacle, Double Helix, Hammerhead, Kraken Form | fire rate, more shots, patterns | **Shot** |
| Nautilus Spiral, Mirror Scale, Swordfish Bill, Ghost Jelly, Mitosis, Boomerang Shrimp, Starfish Arm, Sardine Run | the shot's path or body | **Shot** |
| Pearl Diver, Starburst Gland | a charged pearl; a burst where it pops | **Shot** |
| Cannonball, Captain's Hook | knockback | **Shot** |
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

Each folk boss keeps its fight (docs/NPC-BOSSES-PROPOSAL.md) and gains one rule that asks more of the beam than
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
  | Secret | spot | 1 | 40% | 17 m (20 m from another cave) |
  | Shop | spot | 2 | 45% | 17 m (20 m) |
  | Shell cache | spot | 2 | 60% | 17 m |
  | Crab Deal | spot | 2 | one reef a depth, 60% of depths past the first | 17 m (20 m) |
  | Clam room | spot | 1 | the reef's kind (§6.2) | 17 m (20 m) |
  | Leak cave | sub-level | 2 | the reef's kind (§6.2), or the template's (a clam-room reef's, from row 2) | 17 m (20 m) |
  | Hole | spot | 1 | one for each exit past the first | 17 m (28 m from another hole) |

  Every place keeps 30 m from the start and from the exit; the early place lies within 38 m of the start. A clam room,
  a secret and a shop are rooms in the reef's rock; the leak cave is the one way into a sub-level.
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
