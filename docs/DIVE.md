# The dive

The game is a dive: generated levels, one after another, deeper and deeper. Each level's hole opens when its leak is
sealed, and swimming over it sinks her into the next level. There is no open map. Lantern Bay's folk are met in havens,
calm levels that come now and then between the others. This replaces the open reef of docs/REEF-WORLD.md and the region
maps of docs/RPG-PROPOSAL.md.

## 1. Jellies and dives

- **A jelly is a save slot.** The title's jellyfish cards (docs/TITLE-MENU-PROPOSAL.md) are the three slots. Choosing
  one continues her dive where it stands, or starts a new one.
- **A dive** (`Jelly.Dive`, a `SuspendedRun`) runs Depth 1 to Depth 5, five levels a depth with a boss on the fifth,
  then loops to the next cycle as runs always have (`LevelPlan.NextOf`). It is saved at the start of every level.
- **Its seed** comes from the jelly's own seed and how many dives she has ended (`Dives.SeedOf`), so every dive is a
  different reef.
- **The tutorial.** A new jelly's first dive starts in the tutorial (Depth 0); once she has dived out of it
  (`Jelly.Tutored`) her dives start in Depth 1.

## 2. What she keeps

- **Always hers:** her level, experience, skill points and skills, the folk she has freed, and what is banked.
- **Banked:** the pearls she owns between dives (`Jelly.Passives`, `Actives`, `Slots`) and her shells (`Jelly.Shells`).
  A new jelly starts with the run's starting pearls.
- **Carried:** everything found in a dive since the last haven, pearls and shells alike. Reaching a haven banks it all (§4).
- **Dimmed** (her meter full): the dive ends. What she carried is lost, what is banked and always hers stays, and the
  next dive starts at the top again.

## 3. Camera

The camera stands back far enough that one screen shows about 15% of a level: 55 m from her at zoom 1, 69 m at the
players' 0.8, about 44 × 67 m of ground (`CameraRig.Distance`). The sun's shadows reach 140 m to match
(`SunLight`), and the minimap spans 150 m, about the whole level (`MinimapView.Span`). The Sunlit Shallows take
their dimmed look (`ReefLook.OpenShallows`) so the wider view never washes out.

## 4. Havens

A haven is a level of its own between two others: Lantern Bay's water (`data/maps/haven.json`, drawn by hand, the same
every time), in the bay's warm look, all of it haven water where nothing harms her. Its gate chamber's floor is the
hole on to the level it stands before (`HandLayout.Hole`), open from the start.

- **When** (`Dives.NextOf`, `HavenTuning`): never straight after a haven, nor in the tutorial; after two levels without
  one, 35% of the time (rolled from the dive's seed and the level); after three, always. A haven never takes a level's
  place: it is `LevelId.Haven`, the stop before that level.
- **Who is there:** a home round the lagoon for every neighbour she has freed, each with their service
  (docs/NPC-BOSSES-PROPOSAL.md): Tull's Tide Fan, Kip's mirror, Fennel's brood bed, Ottilie's pearl collector, Bubbles's
  dusk dance. The mirror stays silted until Kip is home.
- **Always:** the shop off the market. Arriving heals her whole and banks all she carries (`BankDive`): her pearls and
  shells are hers for good from then on.
- **Verification:** `--dbg-dive --dbg-haven --depth=N --level=L` starts in the haven before that level (with
  `--dbg-freed=tull,kip,…` for its folk).

## 5. Folk as the depths' bosses

The dive has five depths below the tutorial, and each one's boss (on its fifth level) is a neighbour as the Leak made
her (`Dives.FolkBossOf`, `TopDownMain.DiveBoss`, `FolkBosses`): Depth 1 Grandmother Tull, Depth 2 Kip, Depth 3 Fennel,
Depth 4 Ottilie, Depth 5 Bubbles, each at her own level. Freeing her saves her as freed at once, unlocks her for every
later haven, and from then on her depth's boss is the Leak's own, Drippus Prime.

## 6. Leak caves

The curse dens are leak caves now: where a level's curse den would be, at the end of its spur, a mouth is cut in the
rock (`PoiKind.LeakCave`, `TopDownGenerator.CutLeakMouths`, `LevelMap.Mouths`), weeping the Leak's ink. It leads into
a cave of its own, half a level's water (`DiveCaveTuning.Area`, `CaveLayout`'s area scale), a little meaner than its
level, its leak at its dead end.

- **Inside, it harms her** (`PlaneOptions.DiveCave`, `DiveCaveTuning.Sting`, 1.6 HP a second at any depth) for as long
  as its leak is open.
- **She can leave at any time,** by its way out, back out of its mouth into the level (`TopDownMain.DiveCaveWay`).
- **Sealing its leak** stops the harm at once, heals her whole and leaves a pearl she could be offered beside it
  (`RewardDiveCave`); its upwelling carries her out, and the mouth closes behind her.
- **The level waits as she left it:** going in keeps its state (`PlaneWorld.SaveLevelAnchors`, which leaves her run as
  it is) and coming out puts it back, with all she gained in the cave and its harm still hers.
- **The level keeps its own leak:** sealing that opens the hole onward, so the caves are a choice, never the way down.
- **Verification:** `--dbg-dive --at=mouth` starts by a level's mouth, `--dbg-steer=x,y` swims her in, and
  `--dbg-caveback=seconds` takes her back out that long after.

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
7. **The open reef removed:** its map, zones, mouths, homeward current and charts.
