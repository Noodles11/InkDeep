# The dive

The game is a dive: generated levels, one after another, deeper and deeper. Each level's hole opens when its leak is
sealed, and swimming over it sinks her into the next level. There is no open map. Lantern Bay's folk are met in havens,
calm levels that come now and then between the others. This replaces the open reef and the region maps of
docs/archive/RPG-PROPOSAL.md: its bay, folk, mirror, Tide Fan and leak caves live on here, as this document describes, and the
reef's map, zones, cave mouths in regions, homeward current, charts, lost light and three active slots are gone.

## 1. Jellies and dives

- **A jelly is a save slot.** The title's jellyfish cards (docs/TITLE-MENU.md) are the three slots. Choosing
  one continues her dive where it stands, or starts a new one.
- **A dive** (`Jelly.Dive`, a `SuspendedRun`) runs Depth 1 to Depth 5 down a graph of reefs (`DiveGraph`,
  docs/LIGHT-AND-GRAPH.md §6): five rows a depth, the boss alone on the fifth, each reef 2–3 holes down to
  reefs of the row below, then loops to the next cycle. It is saved at the start of every reef, with the reef she is
  on (its lane) and the reefs she has been on (`SuspendedRun.Lane`, `Reefs`).
- **Its seed** comes from the jelly's own seed and how many dives she has ended (`Dives.SeedOf`), so every dive is a
  different reef.
- **The tutorial.** A new jelly's first dive starts in the tutorial (Depth 0); once she has dived out of it
  (`Jelly.Tutored`) her dives start in Depth 1.

## 2. What she keeps

- **Always hers:** her level, experience, skill points and skills, the folk she has freed, and what is banked.
- **Banked:** the pearls she owns between dives (`Jelly.Passives`, her active one among them: she carries one at a
  time, as a run always has) and her shells (`Jelly.Shells`).
  A new jelly starts with the run's starting pearls.
- **Carried:** everything found in a dive since the last haven, pearls and shells alike. Reaching a haven banks it all (§4).
- **Dimmed** (her light spent): the dive ends. The reef holds still round her while her beams fold away and her aura
  draws in, her bell goes out like a lamp, and what light she had leaves her, motes of it rising off her as her bell
  fades to nothing (`TopDownMain.Death`, about three seconds). Then the dive's summary: how deep she reached, what
  she lost and kept, the reefs she crossed (and the havens among them), the leaks she sealed, the light she freed
  (creatures and cores taken) and her shells. What she carried is lost, what is banked and always hers stays, and the
  next dive starts at the top again. (Verification: `--dbg-die=seconds` spends her light that long onto the reef.)
- **The save** (`SaveCodec`, version 3) keeps the dive where it stands: its seed, the reef she is on (with its lane),
  the reefs she has been on, the moment she left (resumed exactly), and the dive's totals for its summary. The graph
  itself is made again from the seed. A save from before the graph takes up at the start of the same row's first reef
  (lane 0), all else it carried kept: the moment it held was of a level no longer made the same.

## 3. Camera

While the reef is calm the camera stands 55 m from her, about 36 × 54 m of ground, a tenth of a level
(`CameraRig.Distance`). **In a fight it draws in** to 38 m (`FightZoom`; 47 m in a boss's arena, so the whole arena
stays in view): as soon as a corrupted creature is within 12 m of her, seen or not, or she is hit (`PlaneWorld.Calm`),
easing in over about half a second and back out slowly once it is calm again (`CameraRig.Tense`). So she is larger
when it matters, and the camera drawing in is itself a warning of something near. The sun's shadows reach 90 m
(`SunLight`), and the minimap spans 150 m, about the whole level (`MinimapView.Span`). The Sunlit Shallows take
their dimmed look (`ReefLook.OpenShallows`) so the wider view never washes out.

## 4. Havens

A haven is a small safe sub-level behind a reef's mouth (`PoiKind.Haven`), as a leak cave or a secret's labyrinth is
(§6): made with its reef from its own sub-seed (`TopDownGenerator.GenerateHaven`, `HavenLayout`), entered by swimming
into the mouth at the end of its spur, left by its way out, back out of the mouth onto the reef.

- **Where** (`LevelPlan.Havens`, `PlaceRules.Haven`): about a third of the reefs from a depth's second row (34%), and
  always the first reef of each depth's second row, so every dive meets one; never on a boss's reef. The tutorial's
  second level has one. (No reef of the dive's graph is a haven: Lantern Bay's hand-drawn reef is retired.)
- **Its shape** (`HavenLayout`, 80 m square, mirrored at random): a gate chamber where her way in is cut, a lagoon in
  its middle with the shop's cave off its far side, a market nook and a nook with the Tide Fan either side; the mirror
  in the lagoon, a home for each neighbour round the water.
- **Always bright and fully uncovered:** all of it is haven water (`LevelMap.Haven`), in the bay's warm look
  (`ReefLook.Bay`), as awake as a reef with every leak sealed. No leaks, no ink, no creatures; nothing harms her.
- **Always:** its shop, in its cave (a reef has no shop of its own now). Arriving heals her whole and banks all she
  carries (`BankDive`); what she buys, absorbs or is paid there is banked at once.
- **What else it offers** (`TopDownMain.PickHavenOffers`): a pick of the services she has unlocked (the folk she has
  freed, docs/FOLK.md), from one to all of them, its own for each haven: Tull's Tide Fan, Kip's mirror, Fennel's brood
  bed, Ottilie's pearl collector, Bubbles's dusk dance. What is not offered there is shut there (the mirror silted).
- **Its mouth stays open:** she may go back in while she is on the reef.
- **Verification:** `--dbg-havenin=seconds` takes her into the reef's haven that long after she is on it (a reef with
  one: `--dbg-dive --depth=2 --level=2`).

## 5. Folk as the depths' bosses

The dive has five depths below the tutorial, and each one's boss (on its fifth level) is a neighbour as the Leak made
her (`Dives.FolkBossOf`, `TopDownMain.DiveBoss`, `FolkBosses`): Depth 1 Grandmother Tull, Depth 2 Kip, Depth 3 Fennel,
Depth 4 Ottilie, Depth 5 Bubbles, each at her own level. Freeing her saves her as freed at once, unlocks her for every
later haven, and from then on her depth's boss is the Leak's own, Drippus Prime.

## 6. Leak caves

The curse dens are leak caves now: where a level's curse den would be, at the end of its spur, a mouth is cut in the
rock (`PoiKind.LeakCave`, `TopDownGenerator.CutLeakMouths`, `LevelMap.Mouths`), weeping the Leak's ink. It leads into
a cave of its own, half a level's water (`DiveCaveTuning.Area`, `CaveLayout`'s area scale), a little meaner than its
level, its leak at its dead end. Each is made with its reef, from its own sub-seed (`LevelShape.Caves`,
`TopDownMain.PrepareCaves`), so the ride in and out is a short fade, the loader showing only if a cave is not ready.

- **Inside, it harms her** (`PlaneOptions.DiveCave`, `DiveCaveTuning.Sting`, 1.6 HP a second at any depth) for as long
  as its leak is open.
- **She can leave at any time,** by its way out, back out of its mouth into the level (`TopDownMain.DiveCaveWay`).
- **Sealing its leak** stops the harm at once, heals her whole and leaves a pearl she could be offered beside it
  (`RewardDiveCave`); its upwelling carries her out, and the mouth closes behind her.
- **The level waits as she left it:** going in keeps its state (`PlaneWorld.SaveLevelAnchors`, which leaves her run as
  it is) and the reef as it was made; coming out puts it back, out of the mouth she went in by, with all she gained in
  the cave and its harm still hers.
- **So does the cave:** one she leaves unsealed waits as she left it, should she go back in (till she leaves the reef).
- **The level keeps its own leak:** sealing that opens the hole onward, so the caves are a choice, never the way down.
- **Verification:** `--dbg-dive --at=mouth` starts by a level's mouth, `--dbg-steer=x,y` swims her in, and
  `--dbg-caveback=seconds` takes her back out that long after; `--dbg-cavein=seconds` takes her into a reef's first
  cave that long after she is on it, `--dbg-caverounds=n` so many times.

### 6.1 Secret labyrinths

A secret room is a sub-level too: where a level's secret would be, at the end of its spur, a mouth is cut as a leak
cave's is (`TopDownGenerator.CutSecretMouths`; `LevelMap.MouthKinds` says which mouth leads where), and across the water
before it stands a sealed wall with an X carved in it (`SealKind.Secret`), till her light fills the X and it slides
down. No ink weeps from it. Behind it is a labyrinth (`TopDownGenerator.GenerateLabyrinth`), made with its reef from its
own sub-seed.

- **The maze** (`Maze`): a grid of cells (9 a side at the first depths, one more each depth down to 13), grown as a tree
  from its entrance (mostly from the newest cell, now and then any: long winding runs with side branches), so every cell
  is reached; then about one dead end in eight is opened into a neighbour, for a few loops. Its heart is the dead end
  farthest from the entrance by its own ways.
- **Its walls are the reef's rock**: passages 4 m wide (wavering a little) between cells 8 m apart, the rock between and
  round them raised, terraced and eased as any level's, with the same decoration. It takes the look of the reef above it
  (`LookDepth`). Its way in and out is cut in its entrance cell's outer wall, going the way she swam into the mouth.
- **Reliable:** the maze is whole by construction; each attempt is checked whole (the heart swum to from the way in)
  and another attempt made if the walls' wavering pinched a passage shut.
- **Its reward lies at its heart:** the secret's shells, or now and then a pearl instead (`PlaneWorld.PlaceShells`,
  `SecretPearl`). Nothing lives in it: no murklings, fish, leaks or springs, and its water does not harm her.
- **Solved, its water carries her home** (`PlaneWorld.LabyrinthSolved`, its way home: DESIGN-TOPDOWN §4.7): once its
  reward is taken, a current wakes along the way from its heart to its way out and carries her there and out of its
  mouth, back onto the reef.
- **No map in it:** neither the minimap nor the whole level's map shows inside.
- **She can leave at any time** by swimming out by its way out; there is no key for it (her active pearl's key does
  nothing in a labyrinth).
- **The reef waits as she left it**, as for a leak cave. Leaving with the reward still there, the labyrinth waits as she
  left it too; its reward taken, the mouth closes behind her.
- **Verification:** `--dbg-mazein=seconds` opens a reef's first secret wall and takes her into its labyrinth that long
  after she is on it; `--dbg-mazeout=seconds` takes her back out that long after she is in; `--at=secret-in` starts before an
  opened secret mouth.

## 7. Requests and chains

- **The Tide Fan's requests** (`Requests.Dive`, `Requests.FillDive`): one fan for the whole dive, tied afresh in each
  haven once Tull is home, its asks at the depth she is diving and naming no cave. She carries two at most, works them
  on any level, and unties them at the next haven:
  - **Seal** three leaks on the way down (`RequestTuning.DiveSeals`; a leak cave's leak counts too);
  - **Prime:** free a murkling prime;
  - **Den:** seal the leak at the bottom of a leak cave;
  - **Ash:** wash a number of patches of ash off leak caves' walls.
  The posters' lines speak of the deep instead of a mouth. On a level, what a carried ask wants is ringed on the maps
  (the open leaks for a seal ask, the open leak caves' mouths for a cave or ash ask), with the hole once it is open, and
  the minimap's pointers show the way to each.
- **Requests and chains are saved at once** (`KeepJelly`): writing them never banks what she carries. In a haven, what
  she buys, absorbs or is paid is banked as it happens (`KeepBanked`).
- **The folk's chains** (`Chains.All`) lead on down the depth below the one their neighbour was the boss of, their
  steps naming depths, never places (`Chains.DepthZone`, `CycleZone`): reach a depth (or the next cycle), free primes
  or seal leaks in a depth, untie a request, reach a haven ("bring it home"), or find something at the bottom of a leak
  cave in a depth (going into one there finds it). The last step pays the neighbour's active pearl.

## 8. Building it

Each step is playable on its own:

1. **Jellies dive** (built): the slots start and continue dives, and dimming ends one (§1, §2).
2. **The camera** stands back (§3) (built).
3. **Havens** and their services (§4) (built).
4. **Folk bosses** for the depths (§5) (built).
5. **Leak caves** in place of the curse dens (§6) (built).
6. **Requests and chains** as dive goals (§7) (built).
7. **The open reef removed** (built): its map, zones, mouths, homeward current, charts, lost light, three active slots and
   great caves.
