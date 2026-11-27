# Ink Deep — the game

The one current design of the whole game, as it is built, and the decided plan for what is not built yet. Start here.
Every other document goes into one part of it in detail. Where two documents disagree, the order of trust is: **this
page**, then the system documents (§0), then DESIGN-TOPDOWN.md, then anything in `archive/`.

> **The `inkdeep-crossover` branch** is rebuilding the game as the crossover of
> [proposals/CROSSOVER.md](proposals/CROSSOVER.md). What is built of it so far is in §14; the rest of this page still
> describes the dive the crossover starts from.

## 0. The documents

| Document | What it holds |
|---|---|
| **GAME.md** (this page) | The whole game in brief, and the decisions that bind the rest. |
| [THEME-BIBLE.md](THEME-BIBLE.md) | The guideline: theme, pillars, lore, look, sound, voice, and the theme tests every idea must pass. |
| [LIGHT-AND-GRAPH.md](LIGHT-AND-GRAPH.md) | Light as life: her aura and beam, what light does, charge and prism stones, murklings hunting light; the dive as a graph of reefs; places and sub-levels. |
| [DIVE.md](DIVE.md) | Jellies (save slots), what she keeps and loses, the camera, havens, folk as depth bosses, leak caves, labyrinths, requests and chains. |
| [ADVENTURE.md](ADVENTURE.md) | Leaks, the ink, fish that turn, corrupted fish, the abyss reef, her oral arm and the HUD's gauge, Crab Deals. |
| [CORRUPTION.md](CORRUPTION.md) | The corruption war: every corrupted creature, meters and corruption rings, Drippus Prime, Queen Clam, light springs, her corruption meter. |
| [FOLK.md](FOLK.md) | The five neighbours: who they are, their boss fights, their homecoming, what each opens in the haven, their chains. |
| [PEARLS.md](PEARLS.md) | Every pearl, synergy and transformation, how a pearl's look is derived, the crab's pearls. |
| [BOSS-GENERATOR.md](BOSS-GENERATOR.md) | The rules every boss keeps, and the generator that rolls bosses. |
| [TITLE-MENU.md](TITLE-MENU.md) | The title screen, its cards, statistics, the Sea-pedia, saves and settings. |
| [VISUAL-SHOWCASE.md](VISUAL-SHOWCASE.md), [ART-LOOKS.md](ART-LOOKS.md) | The painted look (the Blue Hollow), her beam's light, and the earlier looks. |
| [DESIGN-TOPDOWN.md](DESIGN-TOPDOWN.md) | The detailed rules and numbers of the plane: movement, camera, the level generator, lighting, UI. Parts are out of date (§13). |
| [STREAMING.md](STREAMING.md) | Playing on a phone: why there is no web build, the launcher (`tools/play.bat`), Steam Link and its alternatives. |
| `proposals/` | Ideas not built: the deeper fish, the reef redo, the beam pearls plan, the project review. |
| `archive/` | Superseded designs and the reasoning behind built features (the 2D and 3D designs, the open-reef RPG, the old proposals). Read for history, never for rules. |

## 1. The game in one breath

A top-down action roguelite in a darkening ocean. Clementine, a small bioluminescent jellyfish, the last of her bloom,
dives after her family through a reef poisoned by the Leak. Her light is everything: it is how she sees, how she frees
the corrupted, how she seals the leaks, and it is her life. Every pearl she swallows becomes part of her light; every
creature she defeats is freed, not killed; every depth ends with a neighbour to bring home.

The pillars, the tests and the voice are in the Theme Bible. The game in numbers: a dive is a tutorial and five depths
of five rows of reefs; a reef takes a few minutes; a dive is saved at every reef.

## 2. The story

Told in full in THEME-BIBLE §4. In brief: the reef's oldest tale is of the jellyfish who failed the Dragon King's errand
and lost her bones; jellyfish have drifted ever since. When the Crack opened and warm light rose out of it, the reef
believed the Dragon Palace was lit again, and its folk swam to it and came back corrupted. Clementine's bloom went in one
current. She dives to finish the errand. The truth at the bottom is a human home aquarium joined to the sea by a pipe:
the reef was never being called home. It was being kept.

## 3. Clementine

- **Her body** (THEME-BIBLE §5): a clear glass bell with four glowing gonads and a glowing rim, four oral arms and eight
  hair-thin tentacles. She swims in pulses, lurch then glide, and drifts and bobs when left alone.
- **Moves** (DESIGN-TOPDOWN §2.2): free swimming on her plane; the **ink dash** (a burst, briefly untouchable, leaving a
  slowing ink cloud), and the **fast swim** while the dash is held. A dash reads fast: she leaves afterimages, copies of
  her bell every 0.55 m along her way in her light's colour, fading over 0.32 s (`BellView`, bell_ghost.gdshader), and
  the camera leads 2.4 m the way she dashes, easing back after.
- **Her light** (LIGHT-AND-GRAPH §2):
  - her **aura**, always on round her: it shows the corrupted near her and seals a leak she stays close to. **Not in
    the crossover** (since 0.57.0, `PlaneOptions.Glow`): no glow round her, her bell casts no light; her beam is her light;
  - her **beam**, a flashlight cone along her aim, **turned on and off** (since 0.65.0: the right bumper or Ctrl, with
    its own switch sounds since 0.109.0, `light_on`/`light_off`; it stays as it is left, apart from her shots): it burns the corrupted, dazzles them, seals
    leaks, opens seals, clears ink and ash, charges stones and opens bosses, by how much of its light reaches. It is a
    real light: rock, the seabed and every creature's body stop it and cast shadows (VISUAL-SHOWCASE). In the
    crossover it is mostly a light to see by: it shows what the glow showed (the creatures in the dark, what a bubble
    vault holds), dazzles what it falls on and burns creatures at a fifth of its strength (`PlaneOptions.BeamBurn`,
    `HerLightTuning.LightOnlyBurn`), but burns a boss's open pearl in full;
  - her **bubble shots** (since 0.65.0 the right trigger, a mouse click, or the arrow keys, which aim and shoot at once;
    back since 0.57.0, as before 0.13.0): thrown
    bubbles of light that ease out, rest and merge, each a small lantern burning the corrupted it lights; her attack;
  - her **glow can be put out**: dark, she is hidden from the Leak's creatures and sees almost nothing (not in the
    crossover, which has no glow: its toggle has no default button).
- **Her life is her light.** What hurts her corrupts her; her corruption meter is the minimap's bezel and the gauge on
  the HUD. Murklings drink her light. It comes back from cores (the light freed creatures leave), light orbs, light
  springs, healing pearls, havens and a sealed leak cave. Full of corruption, she dims and the dive ends (DIVE §2).
- **Her active pearl** is used with its own button and recharges; she carries one at a time.
- **Controls:** move, aim, shoot (RT / click / arrows), beam on/off (RB / Ctrl), glow (unbound by default), dash, dive,
  active pearl, review pearls, pause. (0.65.0 renamed the beam's and the shot's actions, so bindings saved for the old
  ones are left behind.) Every one can be rebound, on the
  keyboard and mouse and on a controller (`InputSetup`). Twin-stick aim on a controller.

## 4. The dive

- **A jelly is a save slot** (three on the title). Choosing one continues her dive or starts a new one (DIVE §1).
- **The tutorial**, Depth 0, the Sunlit Shallows: two set reefs holding every kind of place, nothing explained in words.
  Passed once, dives start in Depth 1.
- **The depths:**

  | Depth | Reef | Its boss | State |
  |---|---|---|---|
  | 0 | Sunlit Shallows | none (the open rift) | built (the tutorial) |
  | 1 | Kelp Jungle | Grandmother Tull | built |
  | 2 | Sunken Galleon | Kip | built |
  | 3 | Coral Carnival | Fennel | built |
  | 4 | Twilight Trench | Ottilie | built |
  | 5 | The Abyss | Bubbles | built; its own creatures and the keystone's boss planned (§12) |
  | 6 | The Tank | The Hand | **planned**: the finale and the ending (§12) |

  Once a neighbour is freed, her depth's boss from then on is the Leak's own, Drippus Prime. Until the Tank is built,
  the dive loops after Depth 5 into the next cycle, Menace still rising; after the ending, the loop stays as the endless
  mode.
- **A depth is a graph of reefs** (LIGHT-AND-GRAPH §6): four rows of two to three reefs, then the boss's reef. Each reef
  has two to three holes down, each ringed in the colour of the reef it leads to (a plain reef, a clam room, a curse
  den, the boss). The pause menu's chart shows what she has seen of the graph.
- **Havens** (DIVE §4): small safe sub-levels behind a third of the reefs' mouths, always bright, each with its shop
  and a pick of the services she has unlocked. Entering one heals her whole and banks everything she carries.
- **What she keeps** (DIVE §2): her level, skills and freed folk always; what is banked (pearls and shells) between
  dives; what she carries since the last haven is lost if she dims.

## 5. A reef

- **Its leak** (ADVENTURE §1): a brittle star of ink over the shut hole, seen only in light. Its ink spreads as dots
  through the water, and murklings bud out of it (ADVENTURE §4). Sealing it with her light opens every hole of the reef, sends
  a wave of light out that dissolves the ink and briefly shows the corrupted it touches, and pays her experience.
- **Its places** (LIGHT-AND-GRAPH §7): secrets (sealed walls with labyrinths behind), shops, shell caches, Crab Deals,
  clams, leak caves (sub-levels with their own leak, harmful until sealed) and the holes. A template gives each reef
  its subject and cast (DESIGN-TOPDOWN §4.8).
- **Its light**: light springs where she heals, charge stones her beam lights (lures, lamps, and a trio that hides a
  cache), prism stones she pushes into line to colour her light for coloured seals.
- **Clams** (0.93.0, `PlaneClams.cs`, `ClamRoomView`): a giant clam lying shut on the path (a place, as often as the
  clam rooms were, but in the open water rather than a cave). Any light touching it opens it, once and for good: its
  upper valve swings back with an overshoot and shows what it kept, a pearl (90%) with its light pouring out of the
  clam's mouth, or a murkling (10%) that comes out at her. Every pearl lying loose anywhere glows the same way: its
  light in its colours and pattern on the floor all round it, and magic particles rising.
- **Open levels** (0.96.0, phase 1; `OpenSea`, `TopDownMain.Sea.cs`): in the crossover every level but a boss's is an
  endless sea, made only as she swims, new every run. It is laid in 2 × 1 chunks in a herringbone (lying and standing
  chunks of two 18 m cells), the streaming unit. Every chunk is joined to a neighbour nearer the sea's first cell (so
  every chunk is reachable and has a way out), the one whose way lies in the most open water, and to the others
  whenever their way lies in open water (seldom through a reef: `ChunkLink`); two joined chunks meet in open water, and
  their one way (`WaySide`, on the side they share where the reef is lowest, `SideMid` along it) is where a passage
  runs if a reef stands between them. Chunks not joined meet at a wall.
  - **Its places** (0.106.0, `OpenSea.PlaceRules`): a rule for each kind says how often it lies (about one chunk in
    `OnceIn`), how far apart two of a kind must be (`Apart`: 1, never side by side; more, no two whose first cells are
    that few cells apart either way), how far from her start it may first be (`From`, cells), and which rooms drawn by
    hand it may stand in (`Templates`, `TemplateShare` of the time): a hole down about one in 9, a shop 8, a clam 8, a
    shell cache 9, a light shrine 12, a crab's deal 15, a relic 35. Every chunk rolls for every kind (a share worked out
    from `OnceIn` and `Apart`, and from what the kinds above it take) and holds it where none of those round it that
    rolled for it rolled higher: decided from the chunks round it alone, so the same from wherever it is asked and
    nothing of the sea made first. One place to a chunk (the kind higher in the list has it); her first chunk and those
    round it hold none. A place stands in its chunk's roomiest cave (or its template's mark); a hole in a pool at the
    chunk's middle.
  - **Area templates** (0.106.0, `AreaTemplates`): small rooms drawn by hand, a metre a character (a pillared hall for
    a shrine, a grotto with an alcove for a shop or a crab's deal, a chamber ringed by rocks for a clam, a crypt with
    niches for a relic), pressed into the reef round their place, turned any of four ways and blended into the rock
    over their last 2 m; the chunk's passages are carved on to the place, so a template needs no openings of its own.
  - **Places come in with their chunks** (`PlaneWorld.AddSeaPlace`): each is set up as on any level (a clam with its
    pearl or murkling, a cache's shells, a shop's, a shrine's or a crab's stands, a relic or a pearl), seeded by where
    it lies in the sea; its views pick it up the next frame (about 1 ms more a frame in all; no hitch measured over
    1,500 frames). Everything on the level moves with the window. The nearest shop she has not been to shows its
    emblem from the start; every other place once she has found it (or a school has shown it her).
  - **Made as she goes:** a level starts (or a dive lands) with only her chunk and those within two cells of it made, off
    the main thread. Each time she comes into a chunk, those round it are made on workers and each, when done, is shown
    in a frame of its own (about 1.5 ms of the main thread: its mesh, and its heights in the sim's window). Showing the
    level below at a dive costs about 10 ms and arriving about 30 ms (a whole level's 200 ms and 300 ms before).
  - **The sim's window:** the sim's map is a 110 m window onto the sea that follows her: once she is a cell from its
    middle it moves on a cell and the whole world moves back with it (about 3 ms), so no coordinate grows large; the
    sun's shade is cast again for it off the main thread, read from where it lies till then by everything that reads it
    (the ink's relief and rock rims too, 0.96.1: they had read it a cell off, a flicker as she crossed into a chunk),
    and her light's afterimage moves with the window.
  - **One reef, sliced; the reef slide** (0.110.0): every open level is the next slice of the run's one reef (`OpenSea`
    takes the run's seed for its reef and its chunks' grid, and its level's `Slice`: a step down for every level from
    the run's start, `TopDownMain.SliceOf`), so the reef under her is the one above's, a step deeper. On an open level
    whose way on is another she may **dive anywhere the next slice is water under her** (`PlaneWorld.DiveHere`,
    `OpenSea.OpenBelow`), or down any of its holes; the level below is made round that spot (and every chunk then on
    the screen) and she lands straight under it. The dive is the prototype's **reef slide** (`TopDownMain.Slide.cs`):
    she gathers, then over 0.9 s this level's reef morphs where it stands into the next slice's shape (each chunk mesh
    carries the level below's height, normal and colour at its vertices, `LevelView.MorphSea`; `topdown_ground`'s
    `slide` blends them), its life, decoration, arches and roofs fading, and the level below takes over exactly there.
    A dive into the lair (to the depth's boss arena) and out of an arena keeps the iris. The tutorial's rift is an
    open level too (since 0.110.0): its holes and its lair lead on into the first real depth, as the Crack did.
  - **Its lair** (0.98.0, `OpenSea.Lair`): every open level has one way to its boss, a hole down in a chunk five cells
    from her start (in a direction of the sea's own) that holds no other place, in a pool at the chunk's middle.
    It leads into the depth's boss arena (its last level, as before: the boss is met there alone), so
    she may go to the boss from any level of the depth. Its emblem (the boss's "X") shows from the start.
  - **Its creatures** (0.98.0, `PlaneWorld.AddSeaLife`): each chunk, once it lies whole in the window, brings the
    reef's own fish, each kind by its chance: an anemone with its clownfish (50%), tangs (50%), a chromis school of
    12–24 (20%), pufferlings (35%), at open spots apart from its place and from her, seeded by where it lies (the same
    chunk, the same fish). Fish beyond the window leave the sim and come back with their chunk, so about as many swim
    in the window as on a template level (35–120 reef fish), and the sim costs about what it did.
  - **Its corruption, hazards and specks** (0.99.0, phase 3): the corruption is a crossover level's own, murklings out
    of the dark round her (PlaneDanger.cs: the first ones in the window away from her start, then more the longer she
    stays, the danger's marks as ever); no leaks, no ink. Those left beyond the window leave the sim (but one clinging
    to her). Each chunk brings its biome's traps (the Ruins' dart traps, the Shipwreck's cannons) by a chance that gives
    the window about a level's number (`SeaChunks`, 12 chunks to the window), and they come and go with it; the
    Shipwreck's debris falls round her as anywhere. Each chunk brings its share of a tidy level's light specks the first
    time only: those she gathers never come back, and those left beyond the window are gone.
  - **Its light features** (0.100.0, phase 4, PlaneSeaFeatures.cs): the first time a chunk lies whole in the window,
    by a chance (`SeaFeature`, a half) it holds one of its biome's light features (mirrors, panes, stones, haloclines,
    pockets, a light room, plants or animals) at half a level's count of it (at least one), placed in its own water as
    on any level; besides, its share of a level's charge stones, and by one chance in 12 a light spring (about one to
    the window, where she grows). A chunk out of the window altogether takes them with it, lights and all, and brings
    them back as she left them (a shattered pane stays so, a spring spent, an anemone's shells given).
  - **The rest of a level's world** (0.101.0): each chunk also brings its share of the bubble vaults (on spots by its
    rock, in place of a template's marks), of the Ruins' amphorae, and by a chance shared out over the chunks the one
    charge stones' trio of the whole level. **Secret rooms**: a chunk holding a treasure (a clam, a shell cache, a relic)
    is one by `OpenSea.SecretChance` (0.3, never the lair or her first): a sealed wall stands across each of its ways,
    raised in its own heights (and in its neighbours' padding, so they meet), with the sim's seal over it; now and then
    (`SeaPrism`, 0.4) it takes only coloured light, its prism stones bedded out before it. One seal opened opens the
    whole room (0.101.1: its walls are one set of its heights): its chunk takes its heights without them, the window is
    filled again and its ground redrawn, the chunks round it too (their padding, now or when made later); seals and stones come and go with
    their chunk, kept as she left them. **Kelp beds** are made with each chunk (a quarter of them; the Kelp Forest's
    more), copied into the window as its holes are, so the Kelp Forest tangles her in them; and a fifth of its ways
    between chunks hold a **kelp curtain** (`SeaCurtain`; none on a secret room's).
  - **The reef's decoration** (0.102.0): each chunk grows its own on its worker as it is made, on its own ground in
    sea metres (`DecorGround`, so it grows the same on every visit and meets its neighbours'): the coral gardens, the
    seagrass meadows and lone coral heads (`LevelView.Gardens`, `Scatter`) and its biome's things (`BiomePropView`), off
    its holes, its place and a secret room's walls (nothing grows on them, so nothing floats when they open); drawn under
    its chunk, so they move with the window. A template level's square is grown the same way (`DecorGround.Of(map)`).
    All of it stands on **Poisson disks** (`PoissonTile`: one 64 m tile per spacing, wrapping round, made once): no two
    things closer than their spacing (big coral heads and sponges 1.6 m in a garden, the rest 1 m; grass 0.9 m, small
    corals 1.6 m and lone heads 3.5 m on the sand; the biomes' things 2 m, their larger pieces 13 m), no grid to show,
    and as many to the square metre as the old jittered grid gave. (`--dbg-closed`: every level a template level, to
    compare.)
  - **Arches and roofed rooms** (0.103.0): about a third of the chunks (`OpenSea.ArchChance`) carry an arch, a span
    of rock 6–14 m across one of their passages, springing from the rock tops either side (`ArchFooting`), all of it
    inside its chunk; half the rooms holding a clam, a cache or a relic (`RoofChance`; a secret room's always) are
    roofed, the reef's rock carried on over the room as a low dome (`Chunk.Lid`, out to a little past its water and
    never past its chunk). Each is drawn with its chunk as a template level's is (`LevelView.ShowSeaCanopies`) and fades
    while she is under it; a sealed secret room's roof stays whole till its seal opens. The window takes them as it
    takes the holes (`Fill`: `Canopies` and `Lid`), for the sun's shade only (it never draws them).
  - **No lines along the chunks' edges** (0.103.0): the sun's map, whose relief the ink look draws, is cast 36 m past
    the window on every side (`SunMap.Wide`), so its edge (on the window's, a chunk's) never shows as a line across the
    reef; and a chunk's ground is coloured in sea metres with no rim.
  - **Open sea and reefs** (0.106.0, 0.107.0: `OpenSea.Plan`, `RockAt`): the sea is open (about two thirds of it
    water) but for its reefs, and every chunk's ways always meet:
    - the **sea's own reefs** (0.108.0, `ReefField`, the Reef Survey prototype's field, maps/reef_field.gd, ported
      to the digit): one seeded density over the whole sea (2 m cells): coarse shelf noise for the reefs' footprint,
      masses, lobes and detail, all bent by a slow domain warp; rock past a cutoff read off the seed's own sample of it
      (`Calibration`, `Cutoff`) for a share of rock over many seeds (`OpenSea.RockFraction`, 0.34), rising about a metre
      a metre in from its edge (`ReefDepth`). (The prototype's marching-tetrahedra mesher, depth bands, atlas and POI
      quotas are not taken: the game draws its heights as before, and its places keep their rules.)
    - each chunk's **skeleton** (`Plan`): from each of its ways a passage straight in and on to its meeting point (where
      the reef is lowest of a few places in it; its middle for a hole or the lair), and on to its place; every passage
      bowed and meandering (`Meander`), 3.4–5 m wide, cutting through any reef in its way (and through a wall's swell);
      no reef over a place or a hole (a pool 5.5 m round it);
    - **walls between chunks** not joined (and round a secret room): lumpy (`WallReef`: 1.5–4.5 m from where they
      meet, round at their ends), their middle always rock; two chunks are joined unless their way would lie deep in a
      reef, so a wall stands within the reefs;
    - **filler chunks** (`OpenSea.FillOf`, never her first chunk or those round it, the lair or a chunk with a place):
      about one chunk in seven no other takes as its parent is **filled whole** (a block of rock, joined to none,
      bulging lumpily past its square: `SolidBulge`); a **half** has one of its cells rock, a **quarter** that cell's far
      half, their passages running on through it as canyons;
    - a **secret room** is a grotto in a block of rock of its own (its passages and the pool round its place carved in
      it), its ways shut by seals: rounded, lumpy plugs rising to their middle (`SealLump`);
    - the rock is a function of the sea's coordinates alone, so neighbours meet without a seam; about 8.5 ms a chunk on
      a worker. Heights: the floor, rising into the rock over
      3 m and on into mountains 2–10 m in (`MountainPeak`).
  - **Still apart from a template level:** no shop gates, crab's fake walls or tunnels (the chunks have none). The holes she has found keep their emblems (an arrow pointing down).
  - **The debug chunk map** (F3): the chunks round her, the one she is in, those she has visited, those made and not
    yet visited, the ways between them, each one's template and its holes.
- **Holes down** (0.95.0, `TopDownGenerator.OpenHoles`, `LevelPlan.DiveHoles`; on open levels since 0.96.0, a chunk's own): a template level but a boss's has one
  to three holes down (one always, a second half the time, a third on about one level in six), each a place on the
  path. She may dive down any of them at any time, and nowhere else (no dive clock). Each is an opening in the reef
  floor: the floor sags into it over 5 m, its rim wavers (no circle), the lip rounds over into the drop, the slope
  eases off at a wall's foot so the hollow follows the rock, and the ground fades into the dark of the deep as it falls
  (`topdown_post`'s `holes`, `hole_shade`). No light of its own, no ring; once she has found one (or a school has
  shown it her), its emblem shows with an arrow pointing down (`EdgeArrows.Down`).
- **Its life**: healthy reef fish (clownfish in anemones, blue tangs, schools of chromis, pufferfish, flounders) going
  about their day; a school in her light gathers into a shining arrow pointing at the nearest place she has not found,
  and lit ten seconds on end it shows her that place for good (its emblem; `ReefFishTuning.RevealSeconds`); the ink never turns them; currents and geysers; the reef director that keeps the reef acting when she is not
  there.
- **Where she is shown**: the minimap (her surroundings, the unseen filled as rock), the Tab map (the whole level under
  fog), the chart (the dive).

## 6. The corrupted

The Leak's creatures give off no light: they are seen only where light falls on them. Each carries a corruption meter;
emptied, it is freed, leaving a core of light.

- **Murklings only** (ADVENTURE §4): on every reef, at every depth, the only creatures are murklings budded out of the
  leaks' ink: basic ones that cling and drink her light, throwers, shoals (a whole school at once, ringing her) and
  primes (which break into murklings). The deeper (the Menace), the bigger and the more of them. Their size is their
  corruption; they wear no ring.
- **Kept, switched off** (CORRUPTION; ADVENTURE §3–4): the war's creatures (Blightroots, Blotters, Thumpsquirts,
  Inkwardens, Lashjaws, their dens) and the fish that turn (spiked, tadpole and shoal murklings, Hearthguards).
- **Planned** (proposals/DEEP-FISH-PROPOSAL.md): two fish of their own for each depth.

## 7. Bosses

- **Every boss holds a corrupted pearl**, its only weak spot; beating it means finding the pearl and lighting it, often
  only at certain moments. On defeat the pearl is cleansed, the arena washed clean, and the pearl becomes hers
  (BOSS-GENERATOR §0, CLAUDE.md).
- **The folk** (FOLK.md): the five neighbours as the Leak made them, one a depth, each fight built round finding her
  pearl (Tull's sigh, Kip's flank, Fennel hiding in the kelp, Ottilie shielding hers, Bubbles's ink sacs). Freed, each
  goes home to the haven and opens something there.
- **Drippus Prime** (CORRUPTION): the Leak's own boss, a murkling prime grown huge; every birth costs it some of its body.
- **The boss generator** (BOSS-GENERATOR): rolls bosses within hard fairness budgets, with Queen Clam as its reference.
- Each boss is met in a short cut-in (`BossIntro`), real animals, no cartoon faces.

## 8. Pearls and builds

- **Only the shot pearls are in play (0.77.0).** The pearls offered anywhere (`PlaneRun.InPlay`, from `ShotPearls`) are
  only those that change her shots:
  - how many fly at once and their spread: Triple Tentacle, Hammerhead, Double Helix, Plankton Swarm;
  - their size, damage, speed and range: Shark Tooth, Pufferfish Pout, Cuttlebone, Cannonball, Whale Lung;
  - their shove: Captain's Hook;
  - critical hits: Giant Squid Eye;
  - how the gun fires: Sardine Run, a machine gun; Pearl Diver, a rifle;
  - how many they pass through or bounce off: Swordfish Bill, Mirror Scale.

  Every other pearl stays in the code, dormant: never offered, found or dealt (the F1 menu can still switch one on).
  Her light's own characteristics grow from light specks instead (below).
- **Light specks and growth (0.77.0)** (`PlaneSpecks.cs`, `PlaneGrowth.cs`; `PlaneOptions.Specks`, on in the
  crossover):
  - **Specks:** motes of light worth 0.5–1.5 energy each.
    - Ten lie on a level, two more a depth, in its open water at least 6 m from her start.
    - A freed creature drops one 70% of the time (worth 0.4–1.2); a freed boss drops six.
    - Each glows faintly with its own light, drawn after the ink look (`SpeckView`), so it shows from across the level.
    - It is a light like any other in the registry (a 4 m reach): it shows the murklings near it (`SpeckSees`), and
      they come to drink it.
    - She gathers one by touching it; within 2.6 m it drifts to her. Its worth is added to her **energy** (the HUD
      shows it under her bell).
  - **Murklings and specks:** a murkling that drinks a speck keeps what it drank and grows stronger by it. Each unit
    raises its corruption by 30%, its size by 7% (to 1.6 × a murkling's) and its drain by 15%. Freed, it throws it all
    out again as specks.
  - **Growth:** her energy is laid, any amount (a float), on what she would grow, for good (`PlaneRun.Grow`,
    `GrowthStat`).
    - Her light: power (+8% a unit), coherence (+0.04), pulse (+0.3 Hz), beam width (+5%), and her red, green and blue
      bands (+0.12 a unit, the mix only). These are laid over the light her pearls make, or the F1 gauges', and so
      reach her beam, her shots and her colours.
    - Her body: max health (+4), dash cooldown (−3%, to no less than 35%), swim speed (+3%).
    - She may grow only while she has energy and is at a light spring (her healing pool), spent or not. Every crossover
      level short of a boss's now has one.
  - **The upgrade drawer** (`GrowthMenu`) slides in when she swims into a spring with energy to lay:
    - At its top a bubble of light, its specks swirling inside as many as the energy left, the number beside it.
    - Below, every characteristic with its bar. A click sends a speck's worth (one, or what is left) flying from the
      bubble into the bar; the bar fills and its number climbs (gold until it is kept) as the specks arrive.
    - **Reset** sends what is not kept back into the bubble. **Mutate** keeps it, and the energy is spent. Leaving
      unkept is a reset.
  - Her energy and growth are saved with her dive and with her jelly, as her skills are.
  - `--dbg-grow=N` opens the drawer with N energy; `--dbg-lay=Power,Blue,…` clicks its rows. Tests: `GrowthTests`.

- **Pearls are swallowed, for good**, in a short ritual, and circle her bell (PEARLS). 72 are designed and 41 work on the
  plane; 13 are actives (she carries one). A pearl looks like what it does: its colour from its strongest effect, an
  icon engraved for its kind (`PearlKey`).
- **Found** in clams, shops, labyrinths, boss pearls, Crab Deals (paid in health), request rewards and the folk's
  chains. **Achievements** unlock pearls into the pools (DESIGN-TOPDOWN §8).
- **On her beam**: fire rate is its tick rate, shot speed its carry, knockback its light pressure; pearls split it into
  rays, a helix, a floodlight (proposals/BEAM-PEARLS-PLAN.md has the rest of the carry-over).
- **Her light's colours** (LIGHT-AND-GRAPH §10): Fire Coral, Glowing Moss and Frost Kelp tint her light ember, bloom and
  frost; a split beam deals them across its rays, and where they meet on a creature they mix as light does (sunfire,
  shatter, lull, daybreak), each mix doing more. Coloured light opens coloured seals and colours charge stones.
- **Her light's paths on rock** (LIGHT-AND-GRAPH §11): Mirror Scale bounces it, Mitosis forks it, Boomerang Shrimp
  sends it back along its way, Ghost Jelly takes it through thin walls; the rays born take each other's words where
  they land (Cathedral, Hall of Mirrors), always with less light than arrived.
- **Her light's rhythms** (LIGHT-AND-GRAPH §12): held steady her beam focuses (Pearl Diver), held long it spins up
  (Sardine Run), held on a creature it snaps (Pistol Shrimp Claw), and Glow Burst makes it beat; Pearl Necklace, Depth
  Charge and String of Pearls name where they meet.
- **Her light's paths in the open** (LIGHT-AND-GRAPH §13): it leans toward creatures (Anglerfish Lure), flares
  (Starfish Arm), leaves pools of light that burn on, painting the reef (Bubble Coral), and arcs (Electric Eel Tail);
  Will-o'-Wisp, Pinball Storm and Aurora name where they meet.
- **Her light's echoes** (LIGHT-AND-GRAPH §14): what her light frees flares (Starburst Gland), shines out spines that
  take her paths (Fire Urchin Spine) or sends its core flying home (Lamprey Mouth), in her colours, one generation
  deep; Supernova names where they meet.
- **Her glow's second voice** (LIGHT-AND-GRAPH §15): fireflies of her light orbit her (Firefly Squid, Bubble Gland,
  Anemone Pump), revealing and burning a little in her colours, lures for the murklings; her glow takes her colours.
- **Her great lights** (LIGHT-AND-GRAPH §16): Sunbeam's Lance, Nautilus Spiral's turning beam and Lighthouse where
  they meet; the lantern forms (three pearls of a tag) re-theme her light: Kraken Form's crown, Neon Rave's seven
  colours in turn, Coral Reef's mending afterlight, Shark Mode's hot beam and Frenzy, Pirate's gilded light; and the
  crab's Black Sun, whose shadows burn.
- **The catalogue** (docs/PEARLS.md, generated from it): 61 pearls in play, each a word of her light's grammar
  (shape, path, colour, rhythm, echo, glow), a plain stat, an active or the crab's; 14 constellations and 5 lantern
  forms, each announcing itself. The pause menu's Stats tab reads her light back in those words above her numbers
  (beam reach and width, tick rate, carry); the Sea-pedia lists the pearls by kind.

## 9. Growth and the jelly

- **Experience** (DESIGN-TOPDOWN §2.6) comes from setting things right: cleansing a level's corruption (a step every
  tenth), freeing fish, freeing bosses. Each level is a **skill point**, laid on one of her modifiers in the pause menu.
- **Her look**: a new jelly is made on the title's jelly card (`LookForm`): her name and the shape of her four gonads
  (`JellyLook`); the shape can be changed at the haven's mirror, once Kip is home. **Her colours are not picked
  (0.76.0):** every jelly's colours follow her light, as her beam and her shots do (`HerColor.FollowLight`, each frame
  from `PlaneWorld.HerSpectrum`: her colour words, or her light set by hand in the F1 menu). Her light is the
  spectrum's colour; her bell and arms a deeper, richer shade of its hue; her tentacle tips a paler one; her glow and
  the warm pool under her follow. Her plain warm gold light gives the tangerine she always had. Old saves' colours are
  not worn, and `data/clementine_color.cfg` and the F1 menu's colour pickers are gone.

## 10. The folk, the requests, the chains

- **The folk** (FOLK.md): Grandmother Tull opens the Tide Fan, Kip the mirror, Fennel the Brood Bed (shells left with him
  are never lost), Ottilie the shell shop, Bubbles the Dusk Dance (skill points laid anew). Freed folk never fight for
  her. Each speaks a line or two from her home; Clementine never speaks.
- **Requests** (DIVE §7): the Tide Fan ties asks for the depth she is diving (seal leaks, free a prime, seal a
  leak cave, wash ash); she carries two and unties them at the next haven.
- **Chains**: each freed neighbour's short quest, its last step paying her active pearl.

## 11. On the screen

- **The HUD** (DESIGN-TOPDOWN §9, ADVENTURE §6): top left, Clementine seen from above with her four oral arms as her
  stats (health, inner light, the active pearl's charge, experience) and her shells beside her; top right the minimap,
  its bezel her corruption meter; top centre a dot per leak. Messages slide down from the top edge. Nothing else stays on
  the screen.
- **On the reef**: corruption rings round the corrupted she can see; the pearl she is absorbing on its own card.
- **Readable at a glance** (VISUAL-SHOWCASE, "The order of the eye"): outside her light the reef is grey and quiet and
  never outshines her; what she
  cannot see near her troubles the water and is heard (CORRUPTION, "Sensing the unseen"). A wholly cleansed reef gets
  its full colour back.
- **The pause menu**: Level up, Stats, Pearls, Achievements, Requests, Chart.
- **The title** (TITLE-MENU): the three jellies over a live sea; statistics; the Sea-pedia; settings.
- **The codex** (0.89.0, TITLE-MENU §4.4): the Sea-pedia's places, creatures, bosses, reef life, plants, waters and
  stones, and biomes, each entry locked until she first finds it in play (enters the place, faces the creature, sees
  the fish or plant, reaches the biome), which unlocks its details.
- **A tidy level** (0.91.0, LIGHT-AND-GRAPH.md §23): each level holds only 2–3 of its biome's light features, about a
  third of its water is left quiet (no features or specks), loose things stand in chambers not passages, and specks,
  wall glows and lantern shoals give off less light. A light spring lies faint and dormant on the floor until she swims
  over it, then lights up (CORRUPTION.md, light springs).
- **The spectral survey and the keystones** (0.90.0, LIGHT-AND-GRAPH.md §22): every plant and animal that answers light
  fills in as she lights it by ember, bloom, frost and white light, watches it in the dark and lights it with a pulse; a
  complete entry gives its lore in the Sea-pedia, and a biome surveyed whole raises its depth's Atlantean keystone piece
  (four, in the Sea-pedia's Keystones tab).

## 12. Decided and not yet built

- **The ending.** Depth 6, the Tank: flat fluorescent light, neon gravel, plastic plants that do not sway, and The Hand
  in three phases; the bloom found there; the true ending of THEME-BIBLE §4.5. The Abyss's own content before it: its
  creatures, and the old catfish of the keystone as the last boss of the sea.
- **The lore's folklore frame** (THEME-BIBLE §4): the folk's lines, the Sea-pedia and the bosses' cut-ins carry the tale
  of the jellyfish's errand and the Palace, piece by piece across many dives.
- **The rest of the pearls** on the beam, status effects, transformations (proposals/BEAM-PEARLS-PLAN.md).
- **The next steps of the project review** (proposals/MASTERPIECE-REVIEW.md §9): playtests, the light grammar for
  pearls, the tide clock and a 30-minute mode, sound as a system, co-op.

## 13. Known gaps between the documents and the game

- **DESIGN-TOPDOWN.md** still carries rules from before the beam and the dive: bubbles as her attack and their ammo
  orbs (§2.3), sand dollars and hearts (§7), the inherited item and hub rules, seven depths counted to the Tank as built
  (§8). The beam, cores, light orbs, shells and the dive in this page and the system documents replace them. It should be
  rewritten into the system documents and then archived.
- **Code from the open reef** is still in the game (`PortalView`, the region and current-ride code in `TopDownMain`,
  `PlaneReef`, the three active slots in `PlaneRun`); its comments point to archive/RPG-PROPOSAL.md. It should be removed
  or brought under the dive.

## 14. The crossover, built so far

The crossover (proposals/CROSSOVER.md) is being built step by step (its §15). The game runs with its switch
(`Run.Crossover.On`); the sim takes it as a parameter (`LevelPlan.For`, `TopDownGenerator.Generate`: `crossover`), so
the tests of the dive still test the dive.

- **Step 1 (0.49.0), shelving:** the game's camera is straight down and orthographic (`CameraRig.Flat`), framing
  the same ground the tilted one did at her (`--dbg-tilted` brings the tilted view back for comparison); the title's
  sea keeps the tilted view. Its levels have no havens and no leak caves or curse dens (and so no Tide Fan,
  requests, mirror or folk homes to reach). Skill points are shelved: no level-up message, no pulse on her bell or the
  HUD, and the pause menu has no Level up, Requests or Chart tab. Still to shelve: experience itself, and the hole
  rims that name a curse den's reef (the dive graph goes in step 2).
- **Step 2 (0.50.0), template caves and the dive clock:** every level but the tutorial's and a boss's is built from
  cell templates (`LevelPlan.Templated`, `CaveLayout.FromTemplates`, `CaveTemplates`): a 5 × 5 grid of 18 m cells
  (18, not the proposal's 22, so the grid fits inside the level's rim), a Spelunky walk from one row to the far one,
  side cells up to two deep and the odd loop, the whole grid turned or mirrored any of eight ways. Each used cell takes
  a template that opens where it is joined (sockets 6 m wide in the middle of a side), turned and mirrored, its chunk
  slots filled at random; then the walls are pushed back 3 m from the water (0.85.0: roomier passages and chambers than
  the cells draw, `CaveLayout.Widen`; only rock joined to the level's edge gives way, so rock islands, the rooms' cells
  and the arena keep their size, the arena's doorways 12 m wide; its water band ×1.45, `WidenedWater`) and the rock
  edges are eroded with noise, and the generator's own stages (walls, passes,
  arches, decoration) build on it. Ten Sunlit Reef cells and one room cell so far. A room cell holds a place that
  lives in a cave (a Crab Deal; a clam lay in one until 0.93.0, now it takes a POI mark) behind a short spur; an open place (a shell cache) takes a POI mark;
  a place that finds neither is left out. Mob marks fill the spawn table by tag (lone, pack, thrower, ambusher,
  guard), and the marks are kept on the map (`LevelMap.MobMarks`, `PoiMarks`) for the rosters and places of later
  steps; the ink creatures still come from the corruption's Blightroots as before. No secret rooms (a labyrinth is a
  second layer). A template level has no shaft and no holes: after **30 seconds** on it (`DiveClock`; 2 minutes before 0.66.0) she may dive
  anywhere, and nothing else is asked (not even that no fight is on). A ring round her bell on the HUD fills over the
  30 seconds; when it is full a soft chime and a message say so, the prompt shows for a few seconds and the ring
  breathes. The level below is shown under her only once she dives. `--dbg-diveclock=s` starts each level's clock
  that far on (verification).
- **Step 3, first part (0.51.0), shells, bubble vaults, shops:**
  - **Shells by worth** (`ShellKind`, `ShellWorth`): cowrie 1, scallop 3, conch 8, nautilus 25. On a template level
    each shell of a find is rolled by the find's richness (a freed creature's, a pot's, a cache's, a vault's) and the
    depth (the menace), so the rarer shells lie deeper and better hidden; elsewhere every shell is a cowrie, as before.
    A shell's size and glow grow with its worth (one shell model for all four until the ink look); the counter counts
    each in by its worth as it lands.
  - **Bubble vaults** (`PlaneVault`, `VaultView`): templates carry vault marks by their walls (`v`, `LevelMap.VaultMarks`);
    a level takes most of them (at most 6), each a big air bubble snug against its rock holding 2–4 shells (rolled
    as a vault's), a healing orb 40% of the time and a pearl 8%. What it holds shows inside it only in her aura; her
    beam fills it with her light till it bursts (about a second), and her dash bursts it too; its contents tumble out
    away from the wall. It is fixed to the rock. `--at=vault --dbg-vault` shines at one (verification).
  - **Shops:** with no havens, a template level from its second row on holds a shop 45% of the time
    (`LevelPlan.CrossoverShop`), in a room cell like a Crab Deal, with the shop's stands as before.
- **Step 3, second part (0.52.0), light pips, pools by place, the light shrine:**
  - **Light pips** (`LightPips`, `PlaneRun.Pips`, on for the crossover's runs): her health counts in half pips of
    100/12, so 6 pips at the start (more as pearls raise her most). A hit takes whole half pips, one to four by how
    hard it was after the menace (`HitHalves`: one per 12), so hits read and count; a drain (a murkling clinging, the
    curse's dark) still drinks steadily, and her pips show it rounded up. A healing orb gives back a whole pip, the
    shop's top-up two. On the HUD the pips are beads of her warm light strung down her first arm, a half pip a lit
    half bead, the empty ones dark; the last pip breathes, and the arm flashes as one is lost.
  - **Pools by place** (`PlaneRun.Offer`): on a template level a clam offers from the treasure pool, a shop from
    the shop pool (its pearl priced by quality, 50 + 25 a quality step: 50 to 150 shells), a bubble vault from the
    secret pool with the plainer treasures (quality 2 and under), a light shrine the finest (quality 3 and up). A
    pool with nothing left falls back to every pearl she may be offered.
  - **The light shrine** (`PoiKind.Shrine`, `StandKind.Shrine`; the proposal named it but not what it does, so this
    is a first design): an old pale stone on a POI mark of a template level (35% of them, `LevelPlan.CrossoverShrine`),
    a fine pearl resting on it. Touching it gives two pips of her light for the pearl, which she begins to absorb; it
    never takes her last light (with two pips or less it refuses: "Too little light to give"). It differs from the
    Crab Deal, which cuts her most health. `--at=shrine` / `--at=shrine-on` (verification).
  - Step 3 is now built but for the active pearl charging by freeing creatures (§6), which comes with the rosters.
- **Step 4 (0.53.0), the boss level:** a boss level is a template level too (`LevelPlan.Templated` now holds for
  every crossover level but the tutorial's rift; since 0.85.0 the tutorial's first level is a template level too, free
  swimming with no leak to seal). Its walk ends in the **arena**: the 2 × 2 block of the last two rows
  round where the walk would end, carved as one round cave (`CaveLayout.ArenaRadius`, 15 m, its wall wavering a
  little) with a doorway to each cell joined to it; the walk's cells past where it first enters the block are left
  out. The generator's own boss stages work on it unchanged: the sealed boss walls across each doorway, the Crack in
  the middle. Its clock opens no dive: when it has run, **the current rises** (`PlaneTide.cs`, `TideTuning`): the
  water flows toward the arena from everywhere along the level's own ways (down the distance through the water to
  the arena's middle), 1 m/s at first and 0.5 m/s more every 5 s, to 9 (faster than she swims within about a
  minute). It carries her, the free-swimming creatures (murklings not budding or clinging, mobs, Thumpsquirts,
  Blotters), the reef's fish, shells, orbs, freed light, loose pearls and ink clouds; rooted things stay. Inside the
  arena it is still. A creature it brings within 3 m of the arena's rim **dissolves into ink** there (a dark bloom
  and a gurgle) and adds 4% of the boss's own light to its corruption, to at most 60% more; a fish just leaves. It
  stops when the boss is freed; the Crack then opens as before and is the way down (the **vortex**, restyled with the
  ink look). The HUD's ring turns to a swirl of ink once the current runs, and she is told ("The current rises").
  **Queen Clam** is the first depth's boss (`BossRoster.For(…, crossover)`; no neighbour waits there), so the first
  depth plays through: four template levels with the dive clock, then her arena, then the next depth. Not done: the
  current is not drawn yet (only what it carries moves), and the second vortex at a branch waits for the depths'
  branches (step 7).
- **Step 5 (0.54.0), treasures:** the treasures are listed in `data/treasures.json` (`TreasureCatalog`, checked on
  load like the achievements: ids unique and known to the game, `Treasures.Known`, one a depth). The first three:
  the **Pearl Altar** (depth 1, the Sunlit Reef), the **Spare Bell** (the Kelp Forest) and the **Shell Urn** (the
  Shipwreck Graveyard; since step 7 both of depth 2, one for each branch, found by biome). Each depth hides its
  treasure on one of its levels from the second to the one before the boss's (`LevelPlan.RelicLevel`, from the
  depth's own stream): a **relic place** (`PoiKind.Relic`) on the POI mark farthest from the walk's own cells. It
  pushes out a shell cache or the Crab Deal if the level is full; it is never named under the minimap and never shown
  by the Brass Compass. There the treasure lies as the real old object (a worn stone block with three hollows, a
  ship's bronze bell gone dull green, a sealed clay urn), glinting now and then with a small warm light. She takes it
  by touching it: its banner ("Treasure", what it does every run), and it is the **jelly's for good**
  (`Jelly.Treasures`, saved at once). Once she has it, its place holds a pearl from the secret pool instead. From the
  next run on, the treasures she has wait in the **start cave**, round where the run's first template level starts
  (`PlaneRun.BeginWithTreasures`, `PlaneWorld.PlaceStartCave`): the **Pearl Altar** offers three pearls of quality 1
  to 3 on the altar ("altar" pool); touching one fades the other two (`PlanePearl.Choice`). The **Shell Urn** lies on
  its side with 15 shells spilled before it (a conch, two scallops, a cowrie). The **Spare Bell** gives her one more
  light pip for the run (`PlaneRun.ExtraHp`). A run continued from a save keeps the bell's pip but has no start cave.
  Not done: the guarded and light-puzzle hiding places (a relic lies in a side cell for now), the other treasures of
  §5.2, and treasures on the title screen's jelly card.
- **Step 6 (0.55.0), the ink look** (CROSSOVER.md §12): a stage at the end of the water pass (`topdown_post`'s
  `ink_look`, `CameraRig.SetInk`), on for crossover runs (`--no-ink` shows the painted look to compare). It works on
  what is drawn before that pass (the reef, the rock, the floor's things); Clementine, her shots and the particles,
  drawn after it, keep their own look. Outside her light the world is **flat washes**: the rock in ash greys, the water
  and the floor under it near-black blue ink, the light stepped into four tones with a **halftone** (a 45° dot screen)
  at each step's soft edge, so a shadow is a flat fill, never a gradient. **Dithered (0.74.0; softer in 0.74.1; everywhere since 0.74.2):**
  the whole picture under the pass (the rock and what stands on it, the water and the floor, her light on them) is
  drawn in nine tones of brightness, the steps between them an ordered 8 × 8 Bayer dither fixed to the world (0.05 m a
  cell, so it never crawls as the camera moves), blended in at 75%, its hue kept so the biome's tint never speckles;
  the washes' soft edges dither too, in place of the dots (`topdown_post`'s `dither`, `dither_scope` 2, `bayer8`,
  `dither_world`; `CameraRig.SetDither`). `--no-dither` turns it off, `--dither-rock` and `--dither-water` narrow it,
  to compare. Clementine, her shots and the HUD are drawn after the pass and stay clean. **Brush lines** in ink: the caves' outline
  where the rock rises through her plane (a fine pen's, about a pixel wide since 0.101.0, its weight barely wavering,
  fixed to the rock so it never crawls, with dry-brush gaps), a lighter floor contour a few metres down, and a line round whatever lies at her plane. In **her
  light** (and what she has cleansed) the world keeps its colour, stepped a little, and the caves' outline is traced
  in **coral and magenta light**; only the rock's is (the terrain's height from the sun's map tells it apart): a
  creature or a boss in her light keeps its ink line, never a neon one. Her glow in the water is a **sunset**: white at
  her, gold, hot coral, a magenta rim at the edge of her reach. The **paper's grain** (a fine tooth, fibres, a sheet's
  unevenness) lies over the whole picture, her included, at 7% (`Fisheye.InkGrain`). This uses magenta as the warm end
  of her light, the accent CROSSOVER.md §12 allows on this branch. Not done: her bell's own colours, the cyan of the
  deep biomes' machines, and the interface's ink frames.
- **Step 7, first part (0.56.0), the route, the first branch, bosses and rosters by biome** (CROSSOVER.md §8–10):
  - **Biomes** (`Run.Biome`, `Biomes.Of`): depth 1 is the **Sunlit Reef**; depth 2 the **Kelp Forest** or the
    **Shipwreck Graveyard**; depth 3 the **Sunken Ruins** (and the depths below it too, until step 8 builds theirs).
    Every crossover level carries its biome (`LevelPlan.Biome`, `LevelMap.Biome`), and its name is the place's name
    on arriving.
  - **The first branch:** Queen Clam's arena opens **two ways down**: the Crack across it is two vortices end to end
    (`TopDownGenerator.SplitCrack`), both opening when she is freed; the prompt over each names the biome below ("dive ·
    the Kelp Forest below"), and each glows in that biome's colour (green, rust; only in the painted look so far: the ink
    look keeps colour only in her light). The way she takes is kept down the whole depth (`LevelId.Branch`, saved with a
    run, `SuspendedRun.Branch`); the two branches are different levels (their own streams), and both bosses' arenas
    lead on to the Ruins. The crossover's way on is its own route (`Biomes.Exits`): an ordinary level's next is the
    graph's first exit with her branch kept, a boss's the next depth's first level, once or one way for each biome below.
  - **Bosses by biome** (`BossRoster.For(…, biome)`): the neighbours the Leak made move in as the biomes' bosses,
    **Sleepy Old Tull** (the Kelp Forest), **Nine-Fin Kip** (the Shipwreck Graveyard) and **Many-Armed Ottilie** (the
    Sunken Ruins), each with her corrupted pearl as before, at her depth's Menace; in the crossover she is nobody's
    neighbour (freeing her sends no one home: the folk's homes are shelved).
  - **Rosters by biome** (`Biomes.Roster`): each mob mark's tag takes the biome's own creatures: the Sunlit Reef's
    the common reef's; the Kelp Forest's barracuda, moon jellies and nudibranchs, its urchins in wait; the Shipwreck's
    morays and crabs in its holds; the Ruins' morays in its walls. A place's guardians come from the roster too. The
    Kelp Forest has the kelp's dense beds (the Sunlit Reef now the common few).
  - **Treasures by biome** (`TreasureCatalog.InBiome`): a biome's levels hide its own treasure, so the Spare Bell lies
    in the Kelp Forest and the Shell Urn in the Shipwreck Graveyard, both at depth 2; the Ruins have none yet.
    `data/treasures.json` is checked for known biomes, one treasure a biome.
  - `--branch=1` (with `--depth=`) starts on the second branch (verification).
  - Not done: the biomes' own cell templates, chunks, hazards and looks (every biome still uses the Sunlit Reef's
    cells and the depth's palette); the war's creatures in the rosters (the ink creatures still come from the
    Blightroots); the vortices' colour in the ink look; the Ruins' treasure. Tull's, Kip's and Ottilie's fights are their
    folk fights as they were, not tuned for the crossover's arena.
- **Her light and her shots (0.57.0):** her **glow is gone** in the crossover (`PlaneOptions.Glow`): no aura, her bell
  casts no light round her (no bulb, no halo, no warm pool, no glow in the water: `BellView.CastsLight`, the post pass's
  `her_glow_on`), and the glow's toggle has no default button. Her **beam is her light**: it shows the creatures in the
  dark and what a bubble vault holds, dazzles, and burns creatures at a fifth (`HerLightTuning.LightOnlyBurn`), a boss's
  open pearl in full. Her **bubble shots are back** as her attack (right mouse, right bumper; `--shoot` for
  verification). In the ink look **her light is clean**: what her beam lights shows in its own colour, smooth, never
  stepped or halftoned, coming in quickly at the beam's edge; outside it the washes and dots are as they were. (The
  caves' outline is still traced in coral and magenta light where she lights it.)
- **Her light's afterimage, her shots' light, the vaults in the dark (0.58.0):**
  - **The afterimage** (`LightMemory`): where her beam and her shots' light have fallen, the reef keeps its colour
    for 0.1 s after the light has moved on (0.3 s before 0.76.1), then fades back to the muted ash over 0.5 s (was 1.3 s), its edge softened over about a metre. A grid of quarter-metre cells
    over the level, each the light it last had and how long ago, traced as the post pass draws her beam (its fan to
    where it stops, cut by rock) and her shots' light; the post pass takes the larger of it and her light now
    (`her_after`, `her_after_rect`).
  - **Her shots' light** shows the reef round them in its colour, as her beam does: the harder a shot hits, the wider
    and stronger (their lights' cap raised, `CombatView.LightMax`, so a hard shot outshines a plain one).
  - **Bubble vaults lie in the water's dark**: out of her light (her beam, her shots', their afterimage) a vault's film
    all but sinks from sight (`plane_bubble`'s `dark`), only a faint cold-white rim and a faint pale light of its own
    (`VaultView.OwnGlow`) showing it from afar; lit, it shows as before, and what it holds.
  - `--dbg-sweep` turns the auto-fire's aim round her (verification of the afterimage).
  - (0.58.1) Her beam lights its cone alone, right up to her bell: no lit disc round her (reef_show's `rs_beam`).
  - (0.71.1) Her light reaches only so high: the floor below her and the rock rising a little above her plane are
    lit, but it fades out 2.6–4.0 m above her plane (`rs_height`; 1.8–3.2 m in 0.71.1, raised in 0.71.2), just under the
    lowest arch (5 m up), so an arch or a cave's roof over her is never lit (her beam, her aura, her shots' light and
    their afterimage alike).
- **Step 7, second part (0.59.0), the biomes' cells and looks:**
  - **Cells by biome** (`CaveTemplates.KelpForest`, `Shipwreck`, `SunkenRuins`, `VariantsOf`, `ChunksOf`): five cells
    each, one of every shape a cell can take (a cross, a T, a straight, a bend, a dead end; turned and mirrored they open
    every way), with chunk lists of their own. The Kelp Forest's are wide open water among a few rounded rocks (its kelp
    beds stand in them); the Shipwreck's square holds and decks, a beam or a stack of crates in them, a keel down a long
    hull and ribs along its walls; the Ruins' a forum, an atrium round its pool, a colonnade, a temple's cella over its
    steps, a shrine's niche. Every passage is at least 4 m wide (narrower ones close up under the reef's walls and its
    corridors), so their levels are made as readily as the Sunlit Reef's (about 1.1–1.6 attempts). A level takes its
    biome's cells (`CaveLayout.FromTemplates(…, biome)`); a shape a biome lacks falls back to the Sunlit Reef's.
  - **Looks by biome** (`ReefLook.Of`, `ForBiome`): a biome's look is its depth's painted look blended halfway to its own
    palette, the Kelp Forest's greens, the Shipwreck's rust and old timber (the drawn Galleon's), the Ruins' marble,
    warm sand, terracotta and faded fresco blue (`ReefLook.Ruins`); so her light finds each biome's own colours. Used for
    the level's environment, its flora, the dive's look from one to the next, and the map.
  - A pickup placed along a template level's straight two-point corridor no longer divides by zero
    (`PlacePickups`), and a failed attempt is logged with its reason.
  - Not done (then): the biomes' own dressing and hazards (the third part, below), and the Ruins' treasure.
- **Step 7, third part (0.60.0), the biomes' dressing and hazards:**
  - **Gardens by biome** (`LevelView.Gardens`): the Kelp Forest grows sea grass and a few sponges under its kelp, no
    reef corals; the Shipwreck and the Ruins only a thin growth of sponges, fans and sea grass (a third and two fifths of
    the reef's).
  - **Their own things** (`BiomePropView`, for the look only): the Shipwreck's planks, barrels, crates and now and then a
    rusted cannon, on its floor and its wall tops; the Ruins' standing column drums, fallen columns, cut blocks and
    amphorae lying on their sides. A scatter from the level's seed, off the start, the ways down and the places; drawn in
    the scene, so the ink look greys them out of her light like the reef.
  - **Hazards** (`PlaneHazards.cs`, `HazardTuning`), one a biome, none in a boss's arena, none the corruption's (her
    light does nothing to them):
    - the Kelp Forest's **tangles**: inside a kelp bed (within 85% of its radius) she swims at 60% of her pace;
    - the Shipwreck's **falling debris**: every 3.5–6.5 s a timber falls from the wreck above where she is heading
      (her velocity a second on, up to 3 m astray); its shadow and a pale ring show for 1.3 s, then it lands across her
      plane (a thud, a small shake) and sinks; under it (within 1.2 m) she is hit (14, before the menace);
    - the Ruins' **dart traps**: six a level, set in walls (a carved stone face, its mouth a faint pale glow) facing a
      clear run of water; when she crosses one's line (within 0.9 m of it, up to 11 m out, in its sight) it clicks,
      glints warm and 0.35 s later fires a bronze dart at 12 m/s down its line (10 on a hit), then rests 2.5 s.
  - The traps' mouths, the darts and the debris ring are drawn after the ink pass with a faint light of their own, so a
    hazard shows in the dark as a vault does. `--at=dart` stands in front of a trap (verification).
  - Not done: kelp walls her beam burns through, urchin beds as a hazard of their own, the Shipwreck's cannons that
    fire when a creature passes, amphorae to break, light-locked doors; the biomes' rock itself (its shapes and its
    surface are still the reef's); the Ruins' treasure.
- **The biomes' ink (0.61.0, 0.63.0):** in the ink look each biome's muted reef (out of her light) is washed in its own
  colour, always dark, never quite black (`CameraRig.SetInkBiome`, topdown_post's `ink_tint`): the Sunlit Reef blue, the
  Kelp Forest green, the Shipwreck red, the Ruins yellow; its water leans the same way. Every biome has the reef's own
  pattern, fixed to the reef so it never crawls: a 45° dot screen at the soft edge of each tone (`ink_screen`) and
  scattered polyps drawn darker into its unlit rock (`ink_motif`). (0.61.0 tried a pattern a biome; 0.63.0 went back to
  the one.) Her light shows the reef in its own colours over all of it.
- **Relief and things on the reef (0.63.0):** the unlit rock has **relief** (`ink_relief`, from the reef's surface height
  in the sun's map): its slopes lit from the north-west and shaded away from it, crests lighter, the hollow at a wall's
  foot darker on the rock and on the floor before it, the higher rock a little lighter, and a fine grain of stone through
  it all; so the walls stand up out of the floor. **What stands on the floor or the rock** (higher than the terrain under
  it by half a metre: the biomes' things, kelp, sponges and fans) is washed as rock is, and outlined in ink out of her
  light (`art_outline_in`), so it reads in the dark. **Larger pieces** (`BiomePropView.Pieces`), a few a level, far
  apart, on the open seabed under her plane: the Shipwreck's broken hull sections (a keel, ribs curving up both sides,
  planks on them) and fallen masts with their yards; the Ruins' crumbled building walls (runs of cut blocks broken down
  unevenly, some fallen beside them, on the floor and on the rock tops) and fallen arches (two column stumps and a
  lintel). Known: a straight seam in the terrain at the edge of some levels shows as a dark band.
  (0.61.1) The wash kept as dark as the plain ink look (its tint a lean, not a lift). Every crossover level records the
  cells it was built from (`LevelMap.Cells`), and each biome's levels are checked to be built only from its own (and the
  room cell): no two biomes share a cell.
- **Step 7, fourth part (0.62.0), the rest of the biomes' hazards and the Ruins' treasure** (`PlaneHazards.cs`):
  - the Shipwreck's **rusted cannons** (four a level, against walls, a faint glow at the muzzle): anything that swims
    across the line before one sets it off, her or a creature; 0.6 s later it booms (a shake) and fires a heavy ball at
    7 m/s, 14 m out; the ball hits whatever it meets first, her (16) or a creature (40), and rests 4 s. Lure the
    creatures across them;
  - the Kelp Forest's **kelp curtains** (two a level, across the middle of side passages): a close row of tall strands,
    a faint green glow along it; it stops her, her shots and her light, till her beam holds on it 0.6 s: then it parts
    (its strands sink aside) for 3.5 s and closes again behind her (never on her);
  - the Ruins' **amphorae** (its own pots, the breakable vases of old, holding shells) and a **light-locked door** on a
    side passage (a seal her light opens, `TopDownGenerator.RuinsDoors`);
  - the Ruins' treasure, the **Oil Lamp** (`Treasures.OilLamp`, a bronze temple lamp): her beam reaches a quarter
    farther every run (`PlaneRun.BeamReach`).
  - `--at=curtain` stands before a curtain (verification).
  - Not done: urchin beds as a hazard of their own (urchins are the Kelp Forest's ambushers), the doors' own look (a seal
    looks like the reef wall), and the war's ink creatures in the rosters.
- **Step 8, first part (0.64.0), depths 4 and 5:** the **Sunken Ruins'** arena branches again (`Biomes.WaysBelow`), to
  the **Alien Wreckage** (4a) or the **Drowned Temple** (4b); both lead on to **Atlantis** (depth 5, and the depths below
  it till the Crack is built). Each has five cells of its own (`CaveTemplates.AlienWreckage`, `DrownedTemple`,
  `Atlantis`: hexagonal halls, conduit runs and pods; a cenote's stepped court, a causeway, an altar court, a shrine's
  stair; an agora round its fountain, an avenue, a palace's corner, a gate, a sanctum), its chunks, its roster, its look
  (`ReefLook.AlienWreck`, `Temple`, `AtlantisLook`: dark glass and cold cyan; carved grey-green stone, jade and ochre;
  gold-veined marble and orichalcum) and its ink's tint (cyan, orange, violet), its vortex's colour, its gardens; the
  Temple and Atlantis take the Ruins' stone things. Their bosses: **Bubbles** in the Alien Wreckage, **Fennel** in the
  Drowned Temple; Atlantis's machine-fused beast is still to come (Drippus Prime stands in). Not done: their hazards
  (conduits that carry her beam, field pulses, rolling boulders, pressure plates, glyph doors, Atlantis's machines),
  the Alien Wreckage's own things, the Crack, the keystone pieces, new jellies.
- **No way down to find (0.65.1):** a dive-anywhere level (`LevelMap.DiveAnywhere`: a template level but a boss's) shows
  no exit: no "way down" sign on the maps or in their legend, and none named as she swims into it (`LevelMap.Shown`).
  Its cave's walk still ends there (the generator's own), and a boss level shows its Crack as before.
- **A 30-second dive clock, one light (0.66.0):** the dive opens after **30 s** on a level (`DiveClock.Seconds`; gone
  in 0.95.0: she dives down a level's holes at any time, §5); a boss
  level's current keeps its own two minutes (`TideTuning.After`). **All her light is one light:** her shots' light shows
  the reef in its colour now and lingers after as her beam's does (0.1 s, fading over 0.5 s), by the same numbers in the
  post pass and the light memory (`LightMemory.ShotSpread`, `ShotShown`, `CameraRig.SetShotLight`); and her shots light
  a wider, stronger area than before (`CombatView.GroundRadius` 2.1 m, `GroundStrength` 0.45; shown about √3 × as wide).
- **The danger clock and murklings out of the dark (0.67.0)** (`PlaneDanger.cs`, from the cube reef's danger clock):
  a crossover level has no leaks for the corruption to bud from, so its **murklings come out of the dark** instead.
  A level starts with as many as an inked reef would (4 + 8 × Menace, in its dark, at least 18 m from where she comes
  in); more come out of any dark spot 9–20 m from her, one every 9 s ÷ (1 + Menace), while fewer than 4 + 5 × Menace
  are within 26 m of her: **more the deeper**. Each level's **danger clock** climbs in five steps over 2.5 minutes
  (`DangerTuning`): each step after the first, they come 35% more often and 30% more may stay round her: **more the
  longer she stays**. None come during a boss's fight or in its arena, and a boss level's clock stops once its boss is
  freed. The HUD shows the step as trembling red exclamation marks beside her bell (none at first, four at the most).
  (0.67.1) **They hunt by light alone** where she has no glow: every murkling (seekers, throwers, primes, shoals) goes
  for the brightest light it sees, her beam (then her) or a shot's light (then that bubble); with her beam off and no
  shot in the water she is all but invisible to them. A light-seeker sees her beam as the shaft it makes in the water,
  from the side too, by how near it is to the beam's axis at 60% of its strength (`LightQuery.Seen`,
  `LightTuning.SideSeen`), so the beam draws them from outside it. (The 0.67.0 presence, a faint light of hers only
  the creatures felt, is gone.) Where she has a glow (the dive), they sense her near them lit or not, as before.
- **The reef's soft edge (0.68.0)** (`PlaneGoo.cs`, `GooTuning`): the rock is not a hard wall to her. Swimming into it
  she goes on past its line, the water thickening round her like goo: it slows her (drag × how deep) and pushes her
  back out (a spring × how deep), so she stops within about a metre and can go no further than 1.1 m; let go, it eases
  her back out to the water. The deeper she is, the higher she rises (her bell lifted up to about a metre, as a
  jellyfish swims up against the reef). Only she has it (`PlaneWorld.Move(…, soft: true)`, for her swim and the
  currents, the tide and a lashjaw's pull on her); the creatures keep the hard line. Sunk in it, her glow, her beam and
  her shots start from the water's edge she came in from (`PlaneWorld.HerOrigin`), never from inside the rock.
- **The murklings' looks and the wakes, brought over from the cube branch (0.69.0)** (cherry-picked from
  `inkdeep-cubes`; its own docs came with them, in CORRUPTION.md, ADVENTURE.md and VISUAL-SHOWCASE.md):
  - **wakes**: everything that swims leaves a V of disturbed water behind it, bent and shimmering, longer the faster,
    gone within a moment of stopping (`WakeField`, baked round her each frame for the post pass); in the dark it is
    where the creatures are. In the ink look the washes, the dots and the lines are read where the water bends the
    picture, so a wake ripples the reef as a hot-air shimmer even out of her light, and its disturbed water catches a
    little light;
  - **murklings as small black holes**: a lightless body, a photon ring and the water bent and whirling round it while
    her light is on it, its core in her light's colour, its ink peeling off toward her and burning away, the water drawn
    in as it regrows, an implosion when freed (`murk_hole.gdshader`);
  - her beam strips the corruption off what it lights in layers of violet brackets (no longer shimmering the water);
  - a murkling's birth shows only where light falls on it, rising with a hot-air ripple of the water.
  (Not brought over: the cube branch's camera changes and its own cube-reef systems.)
- **The camera, and the lit edges' colour (0.70.0):**
  - from the cube branch: the camera **holds its distance with creatures round her**; only a boss's battle draws it in
    (`CameraRig.Tense`, `BossZoom`); dashing, it leads a little the way she dashes, and fading copies of her bell trail
    the dash (`bell_ghost.gdshader`);
  - where her light traces the reef's edges in the ink look, the line is the **biome's own colour** (its ink's tint
    made bright, wavering a little toward white): the Sunlit Reef blue, the Kelp Forest green, the Shipwreck red, the
    Ruins yellow, the Alien Wreckage cyan, the Drowned Temple orange, Atlantis violet; no longer always coral and magenta.
- **Her shots as a gun (0.71.0)** (`PlaneGun.cs`, `GunTuning`, `PlaneOptions.Gun`, on in the game), her light its
  ammunition: a shot flies straight and fast (2.3× the bubbles' speed) to its range (1.6× theirs) and goes out there,
  no easing, hovering or merging; it shoves what it hits hard (4× a bubble's shove) and kicks her back against her aim
  (2.6 m/s for a plain shot). **Her pearls set how it fires:** a plain shot is a pistol; more shots a throw (Multishot,
  a cone) make a **shotgun**, its pellets scattered ±4° and ±12% in speed, short (55% of the range), its kick 2.2× a
  plain shot's; a charged throw (Pearl Diver) is a **rifle** round, faster and farther (× 1 + 0.8 × the charge), shoving
  harder (× 1 + 2 × the charge), through what it hits once fully charged, its kick × 1 + 2.5 × the charge; the stream
  (Sardine Run) a **machine gun**, each shot's kick a third of a plain one's. The look is the same bolt of light,
  stretched along its flight the faster it goes, its trail a tracer; each shot flashes her light at the muzzle and
  kicks the view a touch.
- **Her light as physics (0.72.0, phase 1 of proposals/LIGHT-WORLD.md):** her light has a spectrum (red 640, green
  530, blue 465 nm), so a wavelength, a frequency and a photon energy; a coherence; and each biome's water and rock are
  matter (`PlaneOptics.cs`, `BiomeOptics`; `PlaneOptions.Optics`, on in the crossover). The full rules are in
  LIGHT-AND-GRAPH.md §17.
  - Her colour words move her power along the spectrum (0.6 a copy, total kept).
  - Each biome's water drinks each band at its own rate and scatters (Beer–Lambert). Her **reach** is how far her light
    gets before 35% is left, against her plain light in the reef's water (0.55–1.5). Her beam's length takes it, and
    her beam's burn takes her **punch** (potency × what the water left at mid reach, against the reef's).
  - Her shots take her potency (blue burns harder per unit of light, red less), fly slower in dense water (the
    Temple's brine), and lose power to the water as they fly (at least a quarter kept).
  - Her gun's shots meet the biome's rock by its material:
    - they ring off a mirror (the wreck's iron most often);
    - they pass through a thin wall bent by Snell's law and come out its far side (the Alien Wreckage's crystal,
      Atlantis's quartz, the Temple's black glass);
    - or the wall drinks them and glows with the share it stores (the Ruins' gilt sandstone most). A wall glow is a
      light: the murklings see it.
  - The view draws a spark where a shot bounces, a flash where it leaves a wall, and the stored glow on the water.
  - Tests: `OpticsTests`.
- **The lit world, phase 2 (0.73.0): her beam divided at walls, mirror plates, lightglass, the light card.** The rules
  are in LIGHT-AND-GRAPH.md §18.
  - **Her beam is divided where it meets the biome's rock** by its material: a mirror's share springs off as a ray of
    hers (the wreck's iron, the Temple's black glass, Atlantis's orichalcum). A crystal's share goes into a thin wall,
    bends and comes out the far side as a ray (Alien Wreckage, Atlantis, the Temple). The share a wall stores glows on
    after her light leaves (the Ruins, the kelp).
  - **Mirror plates** stand in the Shipwreck's (3), the Temple's (4), Atlantis's (4) and the Alien Wreckage's (1)
    water. They throw her beam and her shots back whole, and they turn when she pushes their end.
  - **Lightglass panes** stand across the Alien Wreckage's passages (4), Atlantis's (2) and the Temple's (1), red, green
    or blue. She and the creatures cannot pass them. Only the pane's own colour of her light goes through (her beam
    tinted the pane's colour; a shot passes that share of the time). Rung by that colour long enough, a pane shatters,
    leaving two shells: her plain light does it slowly, a light of its colour fast.
  - **The light card** heads the pause menu's Stats: her light's wavelength and frequency in its colour, her reach,
    punch and halo in this water, and the water and rock she is in.
  - `--at=optic` starts her facing the first plate or pane. Tests: `OpticWallTests`.
- **Her light by hand, and the F1 menu a drawer (0.75.0).** The debug menu (F1, any time in a run) slides in from
  the right as the pause menu does (`DrawerStyle`), all of it one scroll (the wheel, or the right stick). Its
  **Clementine's light** section sets her light by hand (`LightSettings`, `PlaneWorld.ManualLight`, PlaneOptics.cs):
  - one gauge for each characteristic: the red, green and blue bands (0–2: their mix sets her colour, the total
    kept), power (× 0.25–3: her beams' burn and her shots' damage, and her beam's brightness), coherence (0–1), beam
    width (× 0.3–2.5 of its half-angle) and pulse (0–8 Hz: her light swelling and ebbing ± half);
  - a swatch of its colour, and a read-out of its wavelength and frequency, and its reach, punch and halo in this
    level's water (`PlaneWorld.Preview`);
  - moving a gauge sets it by hand ("Set by hand" off gives it back to her pearls; "From her pearls" starts the
    gauges again from what her pearls make: `PlaneWorld.PearlLight`). It holds for every level after, in any level
    (the water's effect only with optics), and marks the run a debug run. Her beam is drawn in the spectrum's colour
    while it is set by hand.
  - `--dbg-f1` opens the menu 20 frames in; `--dbg-light=r,g,b,power,coherence,width,pulse` starts her light set by
    hand. Tests: `OpticsTests`.
- **Her shots take her light as her beam has it (0.75.1).** Where it is relevant, her shots follow the same light as
  her beam (`ShapeOpticsVolley`, `ShotColour`, PlaneOptics.cs):
  - **Colour:** each shot carries her colours when it is thrown (`PlaneShot.Light`: her colour words mixed, or Neon
    Rave's one at a time) and is drawn in them, its halo, lantern and trail too (`CombatView.ShotTint`: a prism's hue
    first, then her light set by hand, then her colour words). They act on what it hits as her beam's do, at once:
    - ember leaves it burning on;
    - frost chills it 0.35 of the way to a standstill (faster with more frost);
    - bloom and sunfire mark it for when it is freed;
    - lull (bloom + frost) makes it lose her;
    - ember on what frost has stilled shatters it.
    Bosses take no colours.
  - **Power and pulse:** with optics or her light set by hand, her potency, power and pulse scale their damage, and so
    their light.
  - **Width and reach:** her beam's width scales their size, and her reach in this water scales their range.
  - **Coherence:** it sets what the water takes from them, as for her beam (§17).
  - Her light's physics is now worked out at the start of each step, so the first shot of a level takes it too.
    Tests: `OpticsTests`.
- **0.78.0:**
  - The crossover's HUD, top left (`HudView.DrawBars`; since 0.94.0, in place of plain bars): her bell seen from above
    (a scalloped rim, a moon jelly's four horseshoe gonads), filling from the bottom with the active pearl's charge in
    its colour; charged, the gonads light up and breathe, and it flashes as the pearl is used. The pearl sits at the
    bell's lower left. Her health is a row of beads of her light to the bell's right (one per pip, a half bead a half
    pip, empty ones dark glass); last in the row (0.95.0) her light's energy, its lumens, as a mote of light with its
    count beside it at the shells' size; her shells under the row. The old bell gauge stays dormant.
  - Arrows at the screen's edges point to places off screen in their colours (`EdgeArrows`,
    `TopDownMain.Arrows.cs`): rooms, caches, shrines, light springs, and the boss's arena while its boss waits. They
    replace the minimap in the crossover. Since 0.92.0 only the boss's arena and the shop show from the start; every
    other place (a cache, a clam, a shrine, a den, a crab's deal, a haven, a hole down, a light room, a light spring) only once she
    has been there (`PlaneWorld.Visited`, `LightRoom.Found`, `LightSpring.Found`) or (0.93.0) a school of fish in her
    light has shown it her (`PlaneWorld.Revealed`; a secret too, "?", until she has been in), and none once it is done
    (`PlaneWorld.PoiDone`: a cache's shells all gathered, a clam opened and its pearl taken or its murkling out, every stand of a shop, a shrine or a
    deal sold, a curse den cleared; a light room opened; a spring spent with no energy of hers to lay).
  - In the reef's soft edge nothing is cut away round her (no see-through). She rises twice as high (`GooLift` 2.2)
    and is drawn up to 35% larger.
  - Her shots pop bubble vaults at once (`VaultShotTests`).
  - The flat camera is tilted a little: 78° (`CameraRig.FlatTilt`; the old view was 60°).
- **From the cubes branch (0.79.0):**
  - **The POI emblems** (`EdgeArrows`, the cubes branch's): each place is a dark disc ringed in its colour with its sign
    inked in the middle. It waits at the screen's edge on the line toward its place, a pointer on its ring, and slides
    onto the place once that is on the screen, fading as she arrives. Signs: "$" a shop, "*" a treasure cave, "o" a
    shell cache, "%" a crab's deal, "^" a shrine, "+" a light spring, "X" the boss's arena while its boss waits.
  - **Its sounds** (`Sfx`, `audio/sfx/CREDITS.md`): a squish as her light strikes, a bloop as a murkling emerges
    (fainter the farther off), a swish as she dashes, a big bubble as a vault pops, a wet splat as an ink bomb lands.
    Since 0.86.0 her shots are heard one by one (`Sfx.PlayShots`): for each projectile of a volley a 0.32 s slice of
    `audio/sfx/shots.mp3` from a random place in it, faded in and out, 25 ms apart, a touch apart in pitch, at −15 dB
    (−3 dB more each time the count doubles); it replaces the recorded pops. A creature her light hits kicks
    (`kick-hit.mp3`, `Sfx.PlayKick`, −8 dB, in place of the squish, which bosses and vases keep), and one she defeats
    goes down (`mob-down.mp3`, `Sfx.PlayMobDown`, −6 dB); each at most once a step (0.87.0). The going-down is pitched
    by the creature's full health (0.88.0, `Sfx.DownPitch`, from the defeat event's `Health`): a basic murkling's at its
    own pitch, three semitones lower each time the health doubles and higher for smaller ones (×0.5 to ×1.6); the
    biggest defeated that step sets it.
    **The sea's own sounds** (0.109.0), mastered to one loudness so each plays at 0 dB (events at −21 LUFS, like the
    recorded hits once their own volume is taken; her beam's switch −24; the danger loop −24 at full; her swimming −28;
    the background −30, its swells evened out): the **background** (`bg_ambient.ogg`) loops always, from the title on
    (`MusicManager`, on the master bus, outside the water's muffle); **her swimming** (`moving_ambient.ogg`,
    `Sfx.Moving`) loops while she swims (over 0.6 m/s), from a random place in it each time she sets off, swelling in
    over a quarter second and dying away over 0.4 s; **the danger** (`danger_increase.ogg`, `Sfx.Danger`) loops from
    the first exclamation mark, louder with each (a quarter at the first, full at the most), following them over a few
    seconds and fading out once the danger clears (and while paused, dying or diving); a clam opening on its pearl
    (`pearl_nearby.ogg`, `Sfx.PlayPearl`, with its knock of shell); her end (`fail_trumpet.ogg`, `Sfx.PlayFail`, as
    she begins to die); her beam switched on and off (`light_on.ogg`, `light_off.ogg`, `Sfx.PlayLight`, in place of the
    pull-chain, which her glow keeps); and `spooky.ogg` (`Sfx.PlaySpooky`) ready for spooky moments, not played yet.
  - **Its title menu** (`TitleMain`, `TitleMain.Phases`, `MenuSpray`): it opens out of the whitening gonad loader onto
    Clementine in open water with fish. Her beam lights the items, the chosen one dissolves, her push wave clears the
    rest, and Play dives to her jellies.
    - Its water is the first depth's here, not the cube reef's.
    - The title launches only a jelly's dive (`RunLaunch.Pending`): no seeded, saved or new run of its own, and the
      save, seed and shots pieces are gone.
  - The dive clock's ring stood right of the bars (gone with the clock in 0.95.0), and the danger marks right of the shells.
- **The dive loads the level below whole (0.79.1):** she holds at the lip until the level below is made, its
  corruption laid and its sun's shade cast. That shade is drawn on the level below as the iris opens, so she never
  lands on a level that shows lit and is then covered in shade (DIVE.md §3).
- **The reef rounded (0.80.0, after the cube branch's smooth reef):** on template levels the rock is raised from a
  signed distance to the water's edge (between the samples), blurred over about 1.8 m (`RoundedEdge`), not from the
  1 m grid's yes or no. Where the rock meets the swim line follows that smooth edge, so its corners round off and its
  1 m steps are gone (a jutting corner stands lower). The rise to the rock's top is eased into it by a smooth minimum
  (`SmoothMin`, `RoundLip` 3 m), a rounded lip rather than a corner. The water is exactly the cave's, so where she
  swims is unchanged, and corridors and marks keep their depth. Arch placement reads the heights, so a level's arches
  may differ from before.
- **The lit world, phase 3 (0.81.0): plants that answer light, fluorescence, light rooms.** The rules are in
  LIGHT-AND-GRAPH.md §19.
  - Six plants:
    - sun anemones open in light and give what they hid;
    - shy anemones shut in light and open in the dark;
    - giant kelp grows a stalk while light is held on it (green the fastest) and hides what lies behind it;
    - sunflower sponges store light and become lamps the murklings go to;
    - night lilies open and glow only in the dark;
    - fluorescent corals drink blue light and glow green.
  - Each level has 1 or 2 light rooms: a ring of the biome's plant round a prize (shells and light specks) that opens
    when they are all awake at once.
- **The lit world, phase 4 (0.82.0): local water, sun-stone and shadow-stone** (LIGHT-AND-GRAPH.md §20):
  - **Stones:** sun-stone stands across a passage only while lit; shadow-stone only in the dark (light it to pass).
  - **Haloclines:** her light bends crossing them, and is thrown back from their dense side at a grazing angle.
  - **Brine pools:** nothing is seen across them.
  - **Silt:** it lies in the Shipwreck and the Ruins, and her dash kicks it up there; while thick it hides what lies
    across it.
  - **Vent shimmer:** it wobbles her shots.
  - **Plankton blooms:** they glow when lit, a decoy for the murklings.
- **The lit world, phase 5 (0.83.0): animals that answer light, corrupted traits, the light pearls** (LIGHT-AND-GRAPH.md §21):
  - **Animals:** glass shrimp flee light; lantern shoals follow a steady blue light; cuttlefish glow back the colour
    lighting them; mantis shrimp strike what comes close while lit; firefly squid lead her to a secret when her light
    pulses at their beat.
  - **Traits:** some murklings are chitin-mirrors (hit their soft side), refractors (hit their edge), band-wards (hurt
    only by her other colours) or light-eaters (fed by weak light: focus them).
  - **Pearls:** Abyssal Blue, Ember Coal and Lens of the Deep are offered; Polariser Lens and Siltwalker are built but
    dormant (they don't change her shots).
- **The lit world, phase 6 (0.90.0): the spectral survey, the keystones, the orrery** (LIGHT-AND-GRAPH.md §22):
  Atlantis's light rooms are orreries: a ring of corals round a quartz heart, with two turnable mirror plates; it opens
  when the corals glow and her beam is thrown off its mirrors onto the heart.

