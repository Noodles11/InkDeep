# The reef world: biomes, area levels and dungeon caves

The open reef is one map holding four biomes, laid out like an act of a Diablo game. Lantern Bay is the town in its
south-west corner. Each biome is a zone with its own name, look and area level, and the levels rise the farther she
swims from the bay. The zones are joined by a few passes, so crossing from one to the next is a moment. The caves are
the dungeons. Each one has a name, a level and one to three floors going down, and its leak is at the bottom. A
homeward current takes her back to town from anywhere and waits to take her back out.

This builds on docs/RPG-PROPOSAL.md (the bay, leak caves, cave openings, lost light) and replaces its plan of separate
region maps joined by currents (§2.2, §3). The Twilight Trench and the Abyss come later, as a second act: a map of
their own below the shelf.

## 1. The map

```
  0 m                       420 m                      840 m
  ┌───────────────────────────┬──────────────────────────┐
  │ KELP JUNGLE               │ CORAL CARNIVAL           │
  │ area level 4–6            │ area level 10–12         │
  │ dense kelp forests,       │ farthest from the bay,   │
  │ narrow green lanes        │ the Leak's own neon      │
  │                     pass ═╪═ pass                    │
  ├──── pass ─────────────────┼──────────── pass ────────┤
  │ SUNLIT SHALLOWS           │ SUNKEN GALLEON           │
  │ area level 1–3            │ area level 7–9           │
  │ ┌──────────┐              │ the wreck's field,       │
  │ │LANTERN   │         pass ═╪═ amber dust, brass      │
  │ │BAY (town)│              │                          │
  └─┴──────────┴──────────────┴──────────────────────────┘ 840 m
```

- **One map, 840 m a side** (`data/maps/reef.json`, region `reef`), four quadrants of 420 m, each a biome zone.
  - The Sunlit Shallows (south-west) is today's reef map, moved into its quadrant.
  - Lantern Bay stays in the Shallows' corner, the map's south-west, as its haven.
- **Zones** (`HandLayout.Zones`, `LevelMap.Zones`, `LevelMap.ZoneAt`): each has a box, a name and title, the depth
  whose look it takes (ReefLook), its Menace and its area level.

  | Zone | Quadrant | Look | Menace | Area level |
  |---|---|---|---|---|
  | Sunlit Shallows | SW | the Shallows' own (Bay's, in the haven) | 0 | 1 |
  | Kelp Jungle | NW | Kelp | 0.15 | 4 |
  | Sunken Galleon | SE | Galleon | 0.35 | 7 |
  | Coral Carnival | NE | Carnival | 0.5 | 10 |

- **Passes.** Each zone meets its neighbours through one or two passes, narrow gorges between its last chambers and
  the next zone's first. The Carnival can be reached only through the Kelp Jungle or the Galleon. A zone's chambers
  are drawn in its own terms:
  - **Kelp Jungle:** many small chambers, with kelp beds thick in every one.
  - **Sunken Galleon:** wide open flats strewn with rubble, broken by long ridges.
  - **Coral Carnival:** round arenas ringed by tall reef.
- **Building it.** It is built once a session off the main thread, starting on the title screen as the bay does now.
  It takes about 16 s in the generator and a few more for its meshes. If that shows, the map is cached to disk.

## 2. Crossing zones

- **The look follows her.** As she swims toward a zone's edge, the water's look blends from her zone's to the next
  over the 30 m either side of the border (`TopDownMain.StepZoneLook`), as it already does out of the bay.
- **Its name over her.** Crossing a border shows the zone's title and its area level, as the bay's does: "Kelp Jungle
  · Area level 4".
- **Its life.** A zone's own Menace drives what lives in it and how hard it is (§4). The ink on the open reef
  thickens zone by zone.

## 3. Caves as dungeons

- **Each mouth has a danger** (`HandPoint.Danger`, 0 to 2). Its cave's level is its zone's area level plus its danger,
  so each zone has caves of three levels: the Shallows' are 1–3 and the Carnival's are 10–12. A cave's level is fixed,
  as an area's is in Diablo.
- **Floors.** A danger 0 cave has one floor, danger 1 has two and danger 2 has three.
  - Every floor but the last ends in a hole down to the next, where the leak would be (the dive she knows from seeded
    levels, ridden down into the dark).
  - The deepest floor ends in the leak, guarded by a murkling prime that is waiting there.
  - Each floor is a seeded cave grown from the cave's seed and its floor number. It is darker and tighter the deeper
    it is: corridors narrower, more side chambers, more murklings.
- **Its name.** Each cave is named from its zone's words and its seed, for example "The Weeping Grotto" (Shallows),
  "The Tangled Hollow" (Kelp), "The Flooded Hold" (Galleon) or "The Hall of Grins" (Carnival). As she comes in, its
  title shows with the floor: "The Flooded Hold · Floor 2 of 3 · Level 8".
- **Read from outside.** The rim of a dangerous mouth weeps more ink, so a danger 2 mouth looks darker and wetter than
  a danger 0. Nearing a mouth, within 12 m, its name, level and floors show over it until she swims away.
  - The Tab map marks every mouth she has charted with its level.
- **Out again.** The way in is held by the Leak's current while the leak is open, as now.
  - Sealing the leak on the deepest floor turns the Leak's pull into an upwelling where the leak was. Swimming into it
    carries her up through every floor and out of the cave's mouth onto the reef.
  - On a cave of one floor she can still swim back out by its way in.
- **Lost light** in a cave waits on the floor she dimmed on. In a cave she had sealed, it waits at its mouth as now.

## 4. Creatures by zone and level

- **Rosters.** Each zone has a roster drawn from the creatures the open reef's adventure already has: its reef fish
  (clownfish, blue tangs and chromis schools, which the Leak can turn), its flounders (Hearthguards once turned) and
  its murklings. The corruption-field creatures of the seeded runs (blightroots, blotters, thumpsquirts, lashjaws,
  inkwardens) need the ground's ink, which the reef has none of, so they stay out until they are reworked for it. New
  creatures for each biome are designed separately, design first.
  - **Fish** (`ReefFishTuning.ZoneFishMore`, `ZoneLife`): six times a level's over the whole map. Every zone keeps a
    share of the places they could live in: the Shallows all, the Kelp Jungle 80%, the Galleon 40% (few fish) and the
    Carnival 25% (the fish hide).
  - **Packs** (`PlaneWorld.PlaceReefPacks`, `ReefTuning.Packs`): murklings roaming a zone's chambers, none in the
    haven, by her start or within 14 m of a cave's mouth.

    | Zone | Packs | Murklings in each | Led by a prime |
    |---|---|---|---|
    | Shallows | 4 | 1–2 | never |
    | Kelp Jungle | 8 | 2–3 | never |
    | Sunken Galleon | 10 | 3–4 | 10% |
    | Coral Carnival | 12 | 4–5 | 30% |

  - **Waiting** (`ReefTuning.Awake`, 60 m): only the fish and murklings within this of her live. The rest hold still
    where they are until she comes near, as a Diablo map's far monsters wait. This keeps the 840 m reef cheap to run.
    The views also draw only what is within 50 m of her, and only the kelp beds she is near sway.
  - **Caves:** murklings and primes, more on every floor down (its Menace).
  - **Menace** on the open reef is the zone's she is in (`PlaneWorld.Menace`), so what lives round her is as fierce as
    her zone.

- **Level.** A creature's level is its zone's area level on the open reef, or its cave's level in a cave. Each level
  past the first adds 25% to its corruption meter (its health) and 12% to the harm it does. Her own levels keep pace
  through her skill points.
- **Experience.** It stays as it is: a sealed leak pays `25 × cave level^0.8`, now times the cave's floors.

## 5. The homeward current

- **Calling it.** On the open reef or in a cave, she calls the homeward current by holding its key (H; the D-pad's
  down on a controller; rebindable in Settings). She gathers it for 2 s, a swirl growing under her as it rises round
  her. Any harm breaks it, and she must let go of the key and call it again.
- **The ride.** She is carried home on a current ride and arrives at the lantern post in the bay.
- **The way back.** A homeward swirl stays 5 m north of the post (the station swirl we have). Swimming into it rides
  her back to where she called it:
  - On the open reef, back to that very spot.
  - In a cave, back to the floor she called it from, at its way in. The cave is as it was when she first came in: the
    leak heals when she leaves, as now.
  - The swirl is gone once she has ridden it back, or once she leaves the bay by its gate.

## 6. How it is built

Each step is playable on its own:

1. **The map and its zones** (built). `reef.json` at 840 m with the four zones, six passes and 22 minor mouths (seven in the
   Shallows, five in each other zone, each zone with caves of every danger), and after them the great caves' mouths (docs/NPC-BOSSES-PROPOSAL.md). Zone looks blend as she crosses
   (`TopDownMain.StepPlaceLook`, `ReefLook.ForZone`: the Shallows' own look with its glare taken off,
   `ReefLook.OpenShallows`), and zone titles and levels show. Kelp grows by zone (`TopDownGenerator.KelpByLook`: the
   Kelp Jungle 1.6 times a Kelp level's for its area, the Shallows 0.8, the Galleon 0.4, the Carnival 0.25). Caves
   take their level and Menace from their zone and danger (`OpenReef.CaveLevel`, `CaveMenace`), and the Tide Fan's
   requests name no cave more than two levels past hers (`RequestTuning.Reach`).
2. **Caves as dungeons** (built).
   - **Floors:** `OpenReef.CaveFloors`, `LevelPlan.ForCave`'s floor (a floor its own level id, `LevelPlan.FloorCycles`,
     and `FloorMenace` 0.1 meaner each floor down) and `LevelMap.HoleDown`. A floor above the deepest keeps its
     shaft and holds no leak and no waiting prime, and its leak counts as open (`PlaneWorld.CaveLeakOpen`): its way
     in stays held and it buds murklings.
   - **Rides in the dark:** swimming over a hole (`TopDownMain.WayThrough`, `RideWay.Down`) sinks her into it as the
     screen goes black over 0.7 s, and she comes down into the next floor's start from above as it lifts. A sealed
     leak's upwelling (`RideWay.Up`, `PortalView.Upwell`) works the same way within 1.8 m of where the leak was.
   - **Its name and title** come from `OpenReef.CaveName`.
   - **The mouth from outside:** its weeping ink (`PortalView.AddInk`, `mouth_ink.gdshader`) and its label within
     12 m (`MouthLabel`).
   - **The Tab map:** "Lv N" by every charted mouth (`TopoMap.Notes`).
   - **Experience:** a sealed leak pays its experience once for every floor of its cave.
   - **Verification:** `--reef=cave:N` starts in the cave behind mouth N, `--floor=N` on its floor N, `--at=hole` over
     a floor's hole and `--at=leak-on` on its leak (with `--dbg-sealvent`, the upwelling then carries her out).
3. **Rosters and levels** (built). Each zone's fish and packs (§4), and level scaling: a murkling's meter
   (`PlaneWorld.LevelMeter`, `ReefTuning.MeterPerLevel`) by the level where it buds, and the harm she takes
   (`PlaneWorld.HurtScale`, `ReefTuning.HarmPerLevel`) by the level where she is (`PlaneWorld.LevelAt`).
4. **The homeward current** (built): TopDownMain.StepHomeward (its key, InputSetup.Homeward), the ride home and the
   way back (WayThrough, PortalView.Gather, PortalView.Homeward). Verification: --dbg-homeward holds its key
   from the start.
