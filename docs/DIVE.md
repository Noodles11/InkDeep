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
  warm-up layer: 36 layers of 64 m squares, each layer's cubes bricks two squares long laid as a herringbone
  (`CubeReef`, `CubeSpace`). Nothing is laid out ahead: a cube is made only as she comes near it (her cube, those its
  gates lead to, the one under it), from the seed and its own place, so it always matches the cubes round it, over it
  and under it. The game shows each layer as a level as wide as a 24-square window (about 1.5 km) centred where she
  comes onto it (`CubeReef.Layer`), each cube laid onto it as it is made, its rock streamed in round her (`ReefStream`,
  `ReefStreamView`): so however far she has come, every new layer runs on as far every way from her arrival, and diving
  on is endless. A layer is its own level in its own metres; the reef's cubes lie in the reef's (`LevelMap.ReefOrigin`
  between them), and a change of layer moves her, the camera and all that is drawn from one to the other unseen.
- **Its seed** comes from the jelly's own seed and how many dives she has ended (`Dives.SeedOf`), so every dive is a
  different reef.
- **The light by depth:** each layer wears the adventure's painted reef, its coral gardens and all; the top layer is
  bright, the bottom one wholly dark (its rock seen only in her light), and each layer between a little darker than the
  one above, the dark gathering toward the bottom (`ReefLook.Layer`).
- **Chalk:** the reef's rock and floor are drawn as chalk (`reef_surface.gdshaderinc`): one flat colour each, the
  depth's own rock and sand drained toward grey and lifted, broken only by noise masks (broad soft blotches, faint
  strata along the height, a fine dusty grain); matte, no relief, no caustics.
- **What is drawn:** a cube's ground is one mesh per 32 m chunk, its cut cap with it (`CubeShape`); the flora one
  batch per species per 64 m tile (`FloraViews`); a charge stone's shards one mesh. None of the flora or the charge
  stones casts a shadow.
- **One tall structure:** the layers are slices of one reef (CUBE-WORLD-PLAN §4.3): each layer's caverns, walls and
  corners lie largely where the layer above has its own, and the reef changes wholly only over about ten layers. Her
  layer's walls rise 60 m above their own rock where the layer above is rock (past the camera's own height, so no
  wall's top is ever seen: each runs on past the screen's edge until the cutout takes it), and under the layer above's
  open water only to about its floor, as they are drawn (topdown_ground's rise; play keeps the walls' real heights),
  the higher the softer (topdown_post's tall blur); the layer she goes to grows its walls as she reaches it. **The
  walls grow** as they rise (topdown_ground's `grow`): pushed out and drawn in along their facing by a 3D noise of
  the world's metres (bulges, undercuts, overhangs, heads swelling over narrower necks) and stepping back in ledges
  every few metres of height, with live rock's lumps and darker pits over the face; nothing moves at a wall's foot,
  where she meets it. Most
  gates between cubes and most tunnels through rock are **roofed** (§4.4): openings carved through the walls, the rock
  running on whole over them. A wall or roof between the camera and her, or
  the water 5 m round her on the camera's side, is cut open (so walls crowding in beside her go too, however close the
  camera has drawn in a fight).
- **The water below:** under her layer the water is thick and soft (topdown_post's deep veil): the layer below shows
  through it only as faint shapes, much blurred and four fifths hidden in the depth's water, and nothing under that
  layer (nor any fish down there) is seen at all.
- **What is not drawn:** every square round her with none of her layer's cubes drawn (not made yet, behind a closed
  edge, past the window) is filled at her plane as the cut rock's inside is (`ReefStreamView.Pall`): the same
  near-black, so the reef never ends in a cut edge with the water or the layer below showing past it.
- **The warm-up layer** (layer 0) is where she learns: bright, its menace low, no creatures, no curse rooms and no
  boss. Veterans dive straight through it. (There is no tutorial level.)
- **Diving and climbing** (CUBE-WORLD-PLAN §7): ↓ (the arrow key) or the d-pad's down over the void, ↑ or its up under a well of
  the layer above (only one layer above the deepest she has reached). Thirty seconds between changes of layer, and no
  diving again within 32 m of where she arrived. No hint shows for it (nor why not yet); only as she
  tries to go back the HUD says what holds her. The dive's middle loops, her sinking (or rising) on through the murk,
  until the layer she goes to is all there: every cube she will see on arrival drawn, its reef life worked out, a new
  layer's boss arena made (rolled as the dive begins); at most six seconds.
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
  in the seeded levels before the cube reef, or under an earlier terrain generator than today's
  (`CubeDiveSave.Terrain`), is let go: she dives anew.

## 3. Camera

While the reef is calm the camera stands 55 m from her, about 36 × 54 m of ground, a tenth of a level
(`CameraRig.Distance`), and it holds there however many creatures are round her. **Only a boss's battle draws it in**,
to 47 m (`BossZoom`, so the whole arena stays in view), easing in over about half a second and back out slowly once
the battle is over (`CameraRig.Tense`). **In a secret labyrinth** it leaves her and holds over the maze's middle, risen
just enough that the whole maze (and 2 m round it) fits the screen (`CameraRig.Frame`, `CameraRig.Fit`). The sun's shadows reach 90 m
(`SunLight`). The Sunlit Shallows take
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
her saves her as freed at once and unlocks her for every later haven; from then on, and below layer 5, the boss is one
of the **Drowned Court** (BOSS-GENERATOR §15): the Palace's old household, a pool for each band of layers (the
Gardens 1–5, the Sunk Stair 6–12, the Dark Halls 13–24, Under the Keystone 25–36), one drawn per boss layer from the
reef's seed, never the same on two boss layers running.

### 5.1 The danger clock

- **Each layer keeps a clock** (`TopDownMain.StepDanger`, `DangerTuning`), started unseen the first time she comes onto
  it. It runs while she is on the layer (not while the game is held: the pause menu or any other; nor in a cave or a
  haven off it, nor during a change of layer) until the layer's boss is freed; it is saved with the dive (`CubeDiveSave.DangerClock`). A layer
  that holds no boss keeps its clock running: the pressure to move on.
- **Its ten minutes** (`DangerTuning.Minutes`, configurable) are cut into five equal steps. In the first, murklings
  come as the Menace alone has them; each step after, they come 30% more often and 25% more of them may stay round her
  (`RateStep`, `CapStep`), to the most at the last step, held from then on. Freeing the layer's boss puts it back to
  the first step for good, to be explored in peace.
- **Murklings come out of the dark** (`PlaneWorld.StepDarkMurklings`): any spot 9–20 m from her that no light reaches
  (`DangerTuning.Dark`), clear of rock, never in a haven or the boss's arena. No ink is needed: the cube reef keeps no
  corruption on its floor. One every 9 s ÷ (1 + Menace), times the step's rate, while fewer than 4 + 5 × Menace (times
  the step's share) are within 26 m of her. They come as the roster's (basic, thrower, shoal), budding where they come.
- **The only sign** is on the HUD: right of her gauge's bell, under her shells, an exclamation mark for each step past
  the first (none at first, four at the most), in a warning red, each trembling on its own, the more of them the harder.
- **Verification:** `--danger-speed=N` runs the clock N times as fast.

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

- **Sealed by a cracked wall** (`PlaneCrackSeals.cs`, `CrackSealView`):
  - *Where:* a secret's way in is only ever placed behind a wall the camera faces, its passage running north into the
    rock, so its seal is seen (`TopDownMain.Grant`: secrets are placed first; with no such socket left, the cube has no
    secret).
  - *What it is:* the reef's rock as it stood before the passage was cut (`CubeTerrain.Uncut`, drawn by
    `CubeShapes.Plug` over the cube's ground with the same skin, rise and caps), so its face is flush with the wall
    round it: no alcove, and from above no seam where it meets the rock; the cutout takes it with the wall round it. A jagged vertical crack runs up the middle of its face, from a little under her plane up the
    risen wall, glowing faintly from inside, its light breathing slowly, and cut with the wall (`crack_seal.gdshader`).
  - *What it hides:* nothing of the passage shows while it stands. Its sim rock (`WeakRock`) stands at the face's line at
    her plane and stops her, the creatures and her shots.
  - *Breaking it:* only her dash driven into its crack (within 0.75 m of its middle, heading in) gets through. It
    crumbles into chunks that tumble out and sink into the sand, with a deep crack and a rumble, felt ("A hidden way
    opens").
  - *For good:* the cube's record keeps it open (`CubeRecord.Opened`, in the dive's save); one she has been through is
    never sealed again.
  - *Verification:* `--dbg-crack` seals the nearest camera-facing socket by her and dashes her into it.
- **Found by its arrows** (`CubeStock.Cues`, `SecretCueView`, `secret_cue.gdshader`): its mouth shows no emblem until
  she has been in (`CubeRecord.Visited`, kept in the dive's save). Round it, up to three arrows are carved into the
  floor at the walls' feet, 7–26 m from it, each in sight of the water before it and pointing to it. They are wholly
  unseen until her beam falls on them (not her glow); then the groove glows a pale moonlit blue, a shimmer running along
  it toward the mouth. From the seed and the mouth alone.

- **The maze** (`Maze`): a grid of cells (9 a side at the first depths, one more each depth down to 13), grown as a tree
  from its entrance (mostly from the newest cell, now and then any: long winding runs with side branches), so every cell
  is reached; then about one dead end in eight is opened into a neighbour, for a few loops. Its heart is the dead end
  farthest from the entrance by its own ways.
- **Its walls are the reef's rock**: passages 4 m wide (wavering a little) between cells 8 m apart, the rock between and
  round them raised, terraced and eased as any level's, with the same decoration. It takes the look of the layer above it
  (`LookDepth`). Its way in and out is cut in its entrance cell's outer wall, going the way she swam into the mouth.
- **Reliable:** the maze is whole by construction; each attempt is checked whole (the heart swum to from the way in)
  and another attempt made if the walls' wavering pinched a passage shut.
- **Its reward lies at its heart,** packed in a bubble vault (GAME §5): the secret's shells, or now and then a pearl
  instead (`PlaneWorld.PlaceShells`, `SecretPearl`); her light pops it. It is solved once the vault is popped and its
  reward taken. Nothing lives in it: no murklings, fish, leaks or springs, and its water does not harm her.
- **Solved, its water carries her home** (`PlaneWorld.LabyrinthSolved`, its way home: DESIGN-TOPDOWN §4.7): once its
  reward is taken, a current wakes along the way from its heart to its way out and carries her there and out of its
  mouth, back onto the reef.
- **Under dark fog** (topdown_post's `maze_fog`, `CameraRig.SetMazeFog`): all of it lies in a near-black fog that
  drifts slowly, the open water round the maze too, but for her, what light shows (her glow and beam, her light's
  bubbles, a lantern's or a pearl's light) and its prize (its vault, 2.6 m round it; then each pearl or shell it held,
  1.2 m). Its flora is drawn solid so the fog lies over it (`FloraViews.Faded`), and the screen's edges darken into it.
- **No map in it:** neither the emblems nor the whole level's map show inside.
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
  the emblems mark each.
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
the seeded levels and their dive graph (CUBE-WORLD-PLAN §15). The cube reef is the only game: every way in is a jelly's
dive (the title's Play); the seeded levels' generator lives on only to make the places behind mouths.
