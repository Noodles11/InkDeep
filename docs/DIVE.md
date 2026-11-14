# The dive

The game is a dive down one continuous reef, the **cube reef** (docs/proposals/CUBE-WORLD-PLAN.md): layer under
layer, each layer a reef of cubes she swims across without a break, the layer below seen through its void. Over the
void she dives to the layer below; under a well of the layer above she may climb back one layer. As she swims into
cubes she has not been in, they are stocked with murklings and may open mouths into places of their own: havens, leak
caves, secret labyrinths and clam rooms. Lantern Bay's folk are met in the havens. There is no open map, and no
levels one after another: the layers are the depths.

## 1. Jellies and dives

- **A jelly is a save slot.** The title's jellyfish cards (docs/TITLE-MENU.md) are the three slots. Choosing
  one takes her dive up in the haven it was saved in, or starts a new one.
- **A dive** (`Jelly.Dive`, a `SuspendedRun` with its `CubeDiveSave`) runs down the cube reef's layers from the
  warm-up layer: 36 layers of a 9 × 9 footprint of 64 m squares, each layer's cubes bricks two squares long laid as a
  herringbone (`CubeReef`, `CubeSpace`). The game shows each layer as a level (`CubeReef.Layer`), its rock streamed in
  round her (`ReefStream`, `ReefStreamView`).
- **Its seed** comes from the jelly's own seed and how many dives she has ended (`Dives.SeedOf`), so every dive is a
  different reef.
- **The warm-up layer** (layer 0) is where she learns: bright, its menace low, no creatures, no curse rooms and no
  boss. Veterans dive straight through it. (There is no tutorial level.)
- **Diving and climbing** (CUBE-WORLD-PLAN §7): Shift or the d-pad's down over the void, Q or its up under a well of
  the layer above (only one layer above the deepest she has reached). Thirty seconds between changes of layer, and no
  diving again within 32 m of where she arrived. The HUD's prompt says how, or why not yet.
- **Currents** (§8): a gentle drift leads her toward cubes she has not been in; crossing into a cube shuts the edge she
  crossed before that one with a current she cannot swim against, for as long as she is on the layer.

## 2. What she keeps

- **Always hers:** her level, experience, skill points and skills, the folk she has freed, and what is banked.
- **Banked:** the pearls she owns between dives (`Jelly.Passives`, her active one among them: she carries one at a
  time, as a run always has) and her shells (`Jelly.Shells`).
  A new jelly starts with the run's starting pearls.
- **Carried:** everything found in a dive since the last haven, pearls and shells alike. Reaching a haven banks it all (§4).
- **Dimmed** (her light spent): the dive ends. The reef holds still round her while her beams fold away and her aura
  draws in, her bell goes out like a lamp, and what light she had leaves her, motes of it rising off her as her bell
  fades to nothing (`TopDownMain.Death`, about three seconds). Then the dive's summary: how deep she reached, what
  she lost and kept, the leaks she sealed, the light she freed
  (creatures and cores taken) and her shells. What she carried is lost, what is banked and always hers stays, and the
  next dive is a new reef, from the top. (Verification: `--dbg-die=seconds` spends her light that long onto the reef.)
- **The save** is made in a haven (CUBE-WORLD-PLAN §10): reaching one keeps the dive there, with what is banked, and
  with it the reef's records (`CubeDiveSave`): her layer and the deepest she reached, the cubes she has claimed on her
  layer and the one above (what she saw of each, the places in it, what is left of its stock), the edges the current
  holds, the director's counts and boss streak, and each layer's arena. Continuing makes the reef again from the seed,
  lays the records over it, and puts her in that haven. Quitting anywhere else keeps the last haven's save. A dive saved
  in the seeded levels before the cube reef is let go: she dives anew.

## 3. Camera

While the reef is calm the camera stands 55 m from her, about 36 × 54 m of ground, a tenth of a level
(`CameraRig.Distance`). **In a fight it draws in** to 38 m (`FightZoom`; 47 m in a boss's arena, so the whole arena
stays in view): as soon as a corrupted creature is within 12 m of her, seen or not, or she is hit (`PlaneWorld.Calm`),
easing in over about half a second and back out slowly once it is calm again (`CameraRig.Tense`). So she is larger
when it matters, and the camera drawing in is itself a warning of something near. The sun's shadows reach 90 m
(`SunLight`), and the minimap spans 150 m (`MinimapView.Span`). The Sunlit Shallows take
their dimmed look (`ReefLook.OpenShallows`) so the wider view never washes out.

## 4. Havens

A haven is a small safe sub-level behind a mouth in the reef (`PoiKind.Haven`), as a leak cave or a secret's labyrinth
is (§6): made from its own sub-seed (`TopDownGenerator.GenerateHaven`, `HavenLayout`), entered by swimming into the
mouth, left by its way out, back out of the mouth onto the layer.

- **Where** (`CubeDirector`, CUBE-WORLD-PLAN §9): a cube she claims opens a haven one time in four, until its layer
  has two, its mouth in one of the alcoves cut into the cube's walls (its sockets).
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
- **It is where the dive is saved** (§2).
- **Its mouth stays open:** she may go back in while she is on the layer.
- **Verification:** `--dbg-havenin=seconds` takes her into the layer's first haven that long after she is on it.

## 5. Bosses

A layer may hold a boss (CUBE-WORLD-PLAN §9.1): on her first arrival on a layer below the warm-up, it is rolled by the
layers she has left without freeing one (35%, 75%, 95%, then certain). Its arena is fixed then: a cube two to four
cubes from where she arrived, a 24 m basin opened in its middle. Its ways out are held shut while the battle runs, and
its wells stay sealed till the boss is freed.

The boss is a neighbour as the Leak made her (`Dives.FolkBossOf`, `FolkBosses`), by the depth the layer stands for:
layer 1 Grandmother Tull, layer 2 Kip, layer 3 Fennel, layer 4 Ottilie, layer 5 Bubbles, each at her own level. Freeing
her saves her as freed at once and unlocks her for every later haven; from then on, and below layer 5, the boss is the
Leak's own, Drippus Prime.

## 6. Leak caves

The curse rooms are leak caves: a cube she claims opens one time in five (never on the warm-up layer), its mouth in
one of the cube's sockets (`PoiKind.LeakCave`, `LevelMap.Mouths`), weeping the Leak's ink. It leads into a cave of its
own, half a level's water (`DiveCaveTuning.Area`, `CaveLayout`'s area scale), a little meaner than its layer, its leak
at its dead end, made from its own sub-seed as she swims in (the ride's fade covers it). The leaks of the game are
only in leak caves: the layers themselves have none.

- **Inside, it harms her** (`PlaneOptions.DiveCave`, `DiveCaveTuning.Sting`, 1.6 HP a second at any depth) for as long
  as its leak is open.
- **She can leave at any time,** by its way out, back out of its mouth into the level (`TopDownMain.DiveCaveWay`).
- **Sealing its leak** stops the harm at once, heals her whole and leaves a pearl she could be offered beside it
  (`RewardDiveCave`); its upwelling carries her out, and the mouth closes behind her.
- **The layer waits as she left it:** going in keeps its state (`PlaneWorld.SaveLevelAnchors`, which leaves her run as
  it is) and the reef as it was made; coming out puts it back, out of the mouth she went in by, with all she gained in
  the cave and its harm still hers.
- **So does the cave:** one she leaves unsealed waits as she left it, should she go back in (till she leaves the layer).
- **The caves are a choice,** never the way down: that is the void.
- **Verification:** `--dbg-caveback=seconds` takes her back out that long after she is in; `--dbg-cavein=seconds`
  takes her into the layer's first mouth that long after she is on it, `--dbg-caverounds=n` so many times.

A **clam room** is a place behind a mouth too: once a layer, on her first, second or third claim, a small level of its
own holding a clam room and its pearl (`TopDownGenerator.GenerateRoom`), calm water, left by its way out.

### 6.1 Secret labyrinths

A secret room is a sub-level too: a cube she claims opens one time in three, its mouth in one of the cube's sockets
(`LevelMap.MouthKinds` says which mouth leads where). No ink weeps from it. Behind it is a labyrinth
(`TopDownGenerator.GenerateLabyrinth`), made from its own sub-seed.

- **The maze** (`Maze`): a grid of cells (9 a side at the first depths, one more each depth down to 13), grown as a tree
  from its entrance (mostly from the newest cell, now and then any: long winding runs with side branches), so every cell
  is reached; then about one dead end in eight is opened into a neighbour, for a few loops. Its heart is the dead end
  farthest from the entrance by its own ways.
- **Its walls are the reef's rock**: passages 4 m wide (wavering a little) between cells 8 m apart, the rock between and
  round them raised, terraced and eased as any level's, with the same decoration. It takes the look of the layer above it
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
- **Verification:** `--dbg-mazein=seconds` takes her into the layer's first labyrinth that long after she is on it;
  `--dbg-mazeout=seconds` takes her back out that long after she is in.

## 7. Requests and chains

- **The Tide Fan's requests** (`Requests.Dive`, `Requests.FillDive`): one fan for the whole dive, tied afresh in each
  haven once Tull is home, its asks at the depth she is diving and naming no cave. She carries two at most, works them
  on any level, and unties them at the next haven:
  - **Seal** three leaks on the way down (`RequestTuning.DiveSeals`; a leak cave's leak counts too);
  - **Prime:** free a murkling prime;
  - **Den:** seal the leak at the bottom of a leak cave;
  - **Ash:** wash a number of patches of ash off leak caves' walls.
  The posters' lines speak of the deep instead of a mouth. On a level, what a carried ask wants is ringed on the maps
  (the open leaks for a seal ask, the open leak caves' mouths for a cave or ash ask), and
  the minimap's pointers show the way to each.
- **Requests and chains are saved at once** (`KeepJelly`): writing them never banks what she carries. In a haven, what
  she buys, absorbs or is paid is banked as it happens (`KeepBanked`).
- **The folk's chains** (`Chains.All`) lead on down the depth below the one their neighbour was the boss of, their
  steps naming depths, never places (`Chains.DepthZone`, `CycleZone`): reach a depth (or the next cycle), free primes
  or seal leaks in a depth, untie a request, reach a haven ("bring it home"), or find something at the bottom of a leak
  cave in a depth (going into one there finds it). A layer counts as the depth it stands for. The last step pays the
  neighbour's active pearl.

## 8. Building it

Built, each step playable on its own: jellies dive (§1, §2); the camera stands back (§3); havens and their services
(§4); folk bosses (§5); leak caves (§6); requests and chains (§7); the open reef removed; and the cube reef in place of
the seeded levels and their dive graph (CUBE-WORLD-PLAN §15). `--classic` (verification) still shows one seeded level,
for what the places behind mouths are made of.
