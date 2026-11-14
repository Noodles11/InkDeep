# The cube world: one continuous reef, streamed in cubes

A proposal and its build plan. The dive's graph of separate reefs (LIGHT-AND-GRAPH §6) is replaced by one reef she
explores in every direction and dives through layer by layer. Cubes are the invisible partition the game generates,
loads, unloads and saves the reef by; she never sees one.

Sections 1–12 are the design, 13 the performance budget, 14 the prototype, 15 the build after it, 16 what is still
open. Numbers marked *(tune)* are starting values to settle in the prototype.

---

## 1. What she experiences

- One reef, no levels and no loading screens. She swims any way the water goes; the reef carries on past every
  horizon she reaches.
- Under her, wherever there is no floor, the next layer down shows through, dim and blurred; what lies under that is
  lost in murk.
- She may dive wherever the water below is open, down through dozens of layers to the reef's bottom. She may climb
  back one layer, never two.
- A gentle current leans her toward water she has not seen. Behind her, two cubes back, a strong current shuts the
  way: the reef is explored onward, not farmed.
- The first layer is a warm-up: bright, low in menace, no creatures. A first run learns to swim, light and dive there;
  a veteran dives straight through it.
- Havens are where the dive is saved, to be continued after quitting. Dying ends the run: the next starts from the top.

## 2. Terms and coordinates

| Term | Meaning |
|---|---|
| **Square** | One cell of a regular grid, `CellCoord(X, Y, Z)`: X east, Y south (the map's axes today), Z the layer, 0 at the top, growing downward. Edges, corners, floors and wells are the squares'. |
| **S** | A square's side: **64 m** *(tune)*, two of today's 32 m render chunks. |
| **Cube** | The piece the reef is made, streamed and kept by: a **brick of two squares**, laid in a **herringbone**, half of them lying (east–west, 2S × S) and half on end (north–south, S × 2S). `CubeId(X, Y, Z, OnEnd)`, named by its first square (the west or north one). One layer thick. |
| **Layer / depth** | All cubes of one Z. "Depth" in the rules below means a layer. Every layer is laid the same. |
| **Layer drop** | Swim plane to swim plane: 10 m, more than the rock stands tall, so the layer below never reaches up into hers. |
| **Edge** | The face two horizontal neighbouring squares share. `EdgeId(X, Y, Z, axis)`, named from its lower-coordinate square, so both sides name it the same. A cube has six outer edges; the edge between its own two squares has no wall. |
| **Floor face** | The face a square shares with the one below. `FloorId(X, Y, Z)`, between Z and Z+1. |
| **Corner** | A grid point of a layer. A cube has six on its outline: its four corners and the middles of its long sides, where the herringbone's bricks meet in threes. `CornerId(X, Y, Z)`. |

**The herringbone.** Which cube a square belongs to follows from (x − y) mod 4 alone: 0 is a lying cube's west
square, 1 its east square, 3 a standing cube's north square, 2 its south square. So the lying cubes step down the
diagonal a square at a time, each with a standing cube under its west half: the herringbone's L-shaped pairs. Finding
a point's cube is two divisions and a remainder; no table is kept.

World positions are global: a point of layer Z is `(x, y)` in metres in square `(floor(x / S), floor(y / S))` and that
square's cube. Nothing is ever re-based, so shots, particles, lights and creatures cross cube lines without knowing
they exist. A reef 9 squares across is under 600 m, well inside what a `float` holds to the millimetre.

**The reef's footprint:** 9 × 9 squares a layer *(tune)* round the column she first dives into. The reef is the cubes
wholly inside it, the largest set of them joined by their sides; a square of the footprint whose cube sticks out is
rock. Edges on the reef's rim are always closed, so the reef has an outside wall rather than an endless plain.

**Why 64 m squares.** The camera sees about 62 × 35 m (35° at 55 m). With S = 64 her view never reaches past the
eight squares round hers, so the cubes of a 3 × 3 window of squares are always enough. At her 6 m/s cruise a cube is
some 10–20 s straight across, 30–60 s as the water winds: long enough to ready what comes next, short enough that she
is always arriving somewhere.

## 3. What a cube holds

A cube of layer Z is a heightfield on the 1 m grid over its two squares, as a level is today, measured from its layer's swim plane, with one
new idea, the **void**:

| At a point | It is |
|---|---|
| height > 0 | Rock: blocks her and everything else. |
| a canopy over water | An arch or overhang (today's `Canopy`): she swims under it. |
| 0 ≥ height > void | Water over a **shelf**: a floor she sees and cannot dive through. |
| height = void | Water with nothing under it: the layer below shows through. |

**She may dive at a point** when, for her whole body's radius: this cube is void there, and the cube below is water
there with no canopy over it. A shelf here, rock below ("an obstacle") or an arch below all refuse the dive. **She may
climb** at a point when the same holds read from the layer above. One rule, both ways.

About 60% of a cube's water is void *(tune)*: seeing down and diving are the rule, shelves and arches the features
that shape where.

## 4. Generation: plan first, then geometry

Everything is a pure function of the world seed and coordinates. No cube asks a neighbour anything, so cubes can be
made in any order, on any thread, and always agree.

### 4.1 The connectivity plan (boundary conditions)

Three small specs, each hashed from the seed and the id of the thing two or three cubes **share**:

- **`EdgeSpec(EdgeId)`**: open or closed, and the seed of its shared wall's shape. Each layer's open edges first
  hold a spanning tree of its cubes (Kruskal over the edges between them, in a seeded order), so every cube of a
  layer can be reached across them; each other edge is open by a 35% chance *(tune)*; edges on the reef's rim are
  always closed. An open edge has one gate, or two on 35% of edges, each 6–13 m wide, its sides never nearer a corner
  than 13 m. Two cubes may share two edges (along a long side), each with its own gates.
- **`CornerSpec(CornerId)`**: rock 8–10 m round the corner. With gates kept off the corners, no water ever joins two
  cubes that share only a corner, so she cannot cross diagonally.
- **`FloorSpec(FloorId)`**: per square, one well, or two (50%), discs 5–7 m in radius where the upper cube must be
  void and the lower open water. They are the sure ways down; any other spot where §3's rule happens to hold is a way
  down too. A square's wells lie in two opposite quadrants of it, 9–15 m from its middle along each axis, the pair of
  quadrants set by the floor's parity: the wells she arrives by and those she may leave by are never in the same
  quadrant, and at least 18 m apart.

A cube's boundary conditions are its six outer edges' specs, its six corners' specs, and the floor specs under and
over its two squares.

### 4.2 The geometry

1. **What the edges decide** (`CubeTerrain.Shared`), a function of the edge alone, so both cubes write the same:
   - its **wall**, a fin of rock along the line whose middle wanders up to 1 m off it and which swells by up to
     1.5 m, but always covers at least 3 m either side (so the apron, and the rock's rim pass round it, are made from
     the edge alone): from 6 m to 13 m thick;
   - each **gate's throat**, cut through the wall and on 10 m into both cubes, swaying from side to side and flaring
     where it opens into each;
   - each **corner's** rock.
2. **The cube's own water**, from its own hash, wherever the edges do not decide, across both its squares (no wall
   between them): a chamber round each well (over it and under it), a pocket hugging each outer edge (60% of edges),
   and two to five chambers more, 7–12 m across. Gate
   mouths, wells and chambers are joined by a spanning tree of the shortest tunnels (bowed curves 5.6–8.8 m wide)
   plus a loop or two. Rims wobble by low noise; then **bays** are bitten into the rock within 5 m of the water where
   the bay noise is high, so walls wander rather than trace the plan. Every gate of a cube reaches every other and
   every well.
3. **Shelves, void and arches**: water is shelf or void by a noise mask at global coordinates (so it runs on across
   cube lines); round a well down it is void, wobbling outward from its radius; arches are laid over the middle of
   a tunnel or two away from the wells, and the floor under an arch is a shelf.
4. **Heights**: all noise (rock relief, shelf floors) is read at **global** coordinates. Rock stands 3–6.5 m, lowered
   to 1.2 m right beside the water. A shelf's floor drops into the void along the shelf noise's contour, a slope
   rather than 1 m steps, and sinks smoothly into a well.
5. **Apron**: the cube's arrays carry 2 m of its neighbours' samples round them (the ground's smoothing reads two
   samples past a cell), all of it decided by the edges, so meshes, normals and soft masks are right up to the line.

**Generation never retries.** Today's generate-validate-retry cannot run while she swims. The construction above is
valid by design; `LevelValidator`'s checks move into the test suite, run over thousands of seeds and coordinates.

### 4.3 Two stages

- **Terrain**: heights, void, canopies, the dive mask. Depends on nothing but seed and coordinates. Cheap.
- **Stocking**: creatures, pickups, decoration, mouths of sub-levels. Depends on the terrain and on what the director
  (§9) grants the cube.

Keeping them apart is what makes the preview layer and far neighbours nearly free.

## 5. Streaming

### 5.1 States

| State | Holds | Which cubes |
|---|---|---|
| **Active** | Terrain, meshes, stocking, full simulation. | The cube she is in. |
| **Preloaded** | Terrain, meshes, stocking ready; creatures asleep till she is near. | The cubes across her cube's outer edges that she can reach through an open gate the wake current (§8.2) has not shut. |
| **Preview** | Terrain and its full ground. | The other cubes of the 3 × 3 squares round hers (they are in the camera's view). |
| **Below** | Terrain and a simplified ground (2 m grid). | The cubes of the 3 × 3 squares under hers, seen through the void. With a dive coming, they are readied as Preview, the one she lands in as Preloaded. |
| **Mask** | Terrain only, not drawn. | The cubes of the 3 × 3 squares over hers, while she may climb there (to know where she may). |
| **Unloaded** | Its record (§10), nothing else. | Everything else. |

Two notes against the first sketch of the idea:

- "The current cube and its six neighbours" is the six outer edges' cubes across (a herringbone brick has six
  neighbours), plus the cubes below and above. The layer above is never drawn (it would hide her from a camera
  looking down); only its terrain is kept, to know where she may climb.
- Neighbours she cannot reach still need their **ground**, because the camera shows 31 m to either side of her. What
  is saved for them is everything else: no stocking, no creatures, no simulation. That is where the cost is.

**Prediction:** the cube past the square beyond a gate she is heading for has its terrain and ground made early once
she is within 20 m of that gate and moving toward it.

### 5.2 The streaming manager

`ReefStream` (core, no Godot) owns which cubes are in which state; `ReefStreamView` (game) owns their nodes. Neither
the player code nor the generator knows about loading.

- **Wants:** each tick it derives the wanted state of every cube from her position, her heading, the open edges and
  the wake current. A cube's work is kept in pieces (terrain, preview mesh, full mesh, stocking); its state rises as
  soon as the pieces it needs are ready, and each piece is let go only once it has gone unneeded for 3 s, what is
  built on the terrain before the terrain (so crossing a line back and forth, where a cube is wanted one way on one
  side and another on the other, does nothing).
- **Requests:** missing terrain, stocking and meshes go to one worker queue, ordered by need: a dive under way, then
  the cube ahead, then Preloaded, then Preview, then prediction. A request whose cube is no longer wanted is cancelled.
- **Applying:** a cube's meshes are made on the worker too (a mesh resource may be made off the main thread), so
  the main thread only hangs them on nodes from a pool, under a budget of 1.5 ms a frame. The simulation does its own
  collision against the heightfield, so there is no physics world to change and collision is ready the moment
  terrain is.
- **Pins:** a cube is not unloaded while anything depends on it: she is in it, a creature awake is in it, a shot or
  wave is crossing it, a boss battle uses it (the arena and the cubes at both its gates), a seal or reward animation
  is running in it. Each pin is a counted reference with a reason, shown in the debug overlay.
- **Depth changes:** it readies the cube below for arrival (§7) and swaps layers.

### 5.3 The simulation reads a window, not a map

`PlaneWorld` takes one `LevelMap` today and many of its parts index a square from (0, 0): collision, the nav grid,
fog of war, the corruption field, the sun map. The change:

- **`IReefField`**: `HeightAt(p)`, `IsOpen(p)`, `Gradient(p)`, in global metres, with `ReefCollision` for bodies
  against it. The cube world (`CubeField`) answers from a 3 × 3 table of squares, each holding its loaded cube (two
  integer divisions and an array read); a classic level answers from its one map, so sub-levels (havens, leak caves,
  labyrinths) keep working unchanged.
- **Anything outside loaded terrain reads as rock.**
- **Per-cube grids:** fog of war and corruption live in the cube (and its record), not in a level-wide array.
- **Navigation:** the flow field to her is already rebuilt every few ticks; it becomes a 96 × 96 m grid centred on
  her, which is smaller than a level's grid today.

### 5.4 Things that cross cube lines

- **Creatures** belong to the world, not to a cube's node. Each has a stable id (the cube that spawned it and its
  number there). A cube's record marks which of its spawns have been made, so a creature is never made twice however
  many times its cube loads. A creature sleeps beyond 45 m of her and wakes inside 40 m, wherever the lines fall. When
  a cube unloads, creatures in it are written into **that** cube's record where they stand.
- **Leash:** a creature does not follow her into water that is not loaded as Active or Preloaded, nor through a gate
  the wake current holds.
- **Shots, waves, particles** live in global coordinates and need nothing. A shot ends when it meets unloaded water
  (which reads as rock).
- **Lights** are world objects in one list; the shader takes the nearest N to the camera, as now. A light is kept
  while it is within its reach plus 8 m of the camera's view, whatever state its cube is in, so none blinks out at a
  line. A cube going down to Unloaded hands its lights over only once they pass that test.
- **Fog, ambient light, water colour** are functions of layer and global position, never of the cube.

### 5.5 Rendering without seams

- **Meshes:** eight 32 m chunks a cube and its arches, built off the main thread with the level's own surface builder.
  Chunk edges share their rim samples and take normals from the apron. Quads all in the void are left out: there the
  layer below shows.
- **Map textures** (fog of war, corruption, baked sun shadow) that shaders sample by world position become one
  256 × 256 texture addressed **toroidally**: square (X, Y) is written at tile (X mod 4, Y mod 4) and the sampler
  wraps (four tiles a side, so a cube of two squares never wraps onto itself). When she crosses a line, only the tiles
  of newly loaded cubes are rewritten; nothing scrolls.
- **Flora and rocks:** one `MultiMesh` per kind per cube, filled off-thread, shown in one step.
- **Pools:** creatures' views, pickups, particle emitters and decoration nodes are taken from and returned to pools,
  never created or freed while she swims.
- **The layer below:** the same chunk meshes at half resolution (2 m grid), no flora, no stocking, drawn 8 m down
  through the void with the water's depth fog. Its own void is filled with flat murk, so nothing under it is ever
  needed. Her light reaches it faintly.

## 6. Fog of war and unclaimed water

A cube is **claimed** the first time she swims into one of its gate throats (or dives into it). Until then only its
seam bands are ever revealed; the rest of it stays unseen however near she stands. Claiming is when the director
(§9) fixes what the cube holds, so nothing she has looked at can change afterwards. "A new cube she visits" in the
rules below means a claim.

## 7. Diving and climbing

- **Where:** §3's rule. Over a spot that allows it the HUD's dive prompt shows, as over a hole today.
- **Controls:** down and up on the controller's d-pad dive and climb (on the keyboard, the dive key and a climb key).
- **Cooldown:** 30 s after any change of layer before the next, either way. The prompt shows the time left.
- **No chain dives:** after arriving, she may not dive within S / 2 (32 m) of where she arrived. If no reachable
  spot in her cube is that far (a small sealed pocket), the limit shrinks to 80% of the distance of the farthest
  one, so she can never be stuck.
- **Climbing:** only to the layer just above the deepest she has reached. On that layer the climb prompt never shows.
- **The animation:** today's dive (1.6 s) kept, with the camera moving in to 60% of its distance over the first half
  and back out over the second. Climbing is the same played upward: she rises, the layer she leaves falls away into
  blur beneath her.
- **No loading:** the cube under her has its stocking readied as soon as she has hovered over divable water for half
  a second or the cooldown has under 5 s to run. Arrival cubes hold no creatures (§9), so this is light. If it is
  somehow not ready, the animation's middle loops, Clementine sinking (or rising) through murk, until it is; there
  is never a splash or a loading screen.
- **What changes underneath:** the layer she leaves becomes "the layer above": its cubes' records are kept, their
  nodes freed, only dive masks held. Shots and particles in flight are dropped, hidden by the animation.

## 8. Currents

### 8.1 The frontier drift

In the Active cube a flow field points along the water toward the nearest gate into an **unclaimed** cube (when there
is none, toward the nearest unvisited well). It pushes her at 0.6 m/s *(tune)*, 13% of her swim speed: felt when she
drifts, trivial to swim against. It fades to nothing within 10 m of a mouth, a reward or a haven, and in a boss arena.
It is drawn with `CurrentView`'s streaks, faint.

### 8.2 The wake current

When she claims or re-enters a cube through an edge, **the edge she crossed before that one** is shut by a current
pouring out of it toward her. It is a wall in effect: the push rises through the throat and at the edge line her
position is simply held, so no dash or pearl carries her through. It stays shut for as long as she is on that layer.

- She can always go back one cube, never two.
- A ring still works: round three or more cubes that meet about a point and back into the first shuts the edges
  behind her and brings her into the first cube, exactly as she left it (§10), through an edge she has not used.
- She can never be trapped: the edge she last crossed is always open, and every cube has wells.

*A choice made here:* the first sketch said she may not return to "the cube before last". Read as a rule on cubes it
can be dodged by stepping back and forth; shutting edges for good cannot be, and is simpler to show and to save.

## 9. The director: what a claimed cube holds

`ReefDirector` keeps, per layer, the count of claimed cubes and what has been granted, and across layers the boss
streak. A cube's grant is rolled when it is claimed, from the seed, the layer and its claim number, so the same seed
swum the same way gives the same reef.

Clam rooms, secret rooms, curse rooms and havens are all **sub-levels behind a mouth**, as havens, leak caves and
labyrinths are today. A cube's terrain carries up to two **sockets** (alcoves in a wall, at least 14 m from any gate);
a granted place opens its mouth in a socket. A cube with no free socket passes its grant on to the next claim.

| Place | Rule |
|---|---|
| **Clam room** | Once a layer, on its 1st, 2nd or 3rd claim: one chance in three on the 1st, one in two on the 2nd if not yet granted, certain on the 3rd; 0 once granted. |
| **Secret room** | Each claim: 1 in 3. No limit. |
| **Curse room** | Each claim: 1 in 5. No limit. |
| **Haven** | Each claim: 1 in 4, until the layer has two; then 0. |
| **Boss** | At most one a layer; §9.1. |
| **Arrival cube** | The cube she dives into: no creatures, ever. Not a haven. It may hold mouths. |
| **The warm-up layer** | Layer 0: bright, its menace low, no creatures anywhere, no curse rooms and no boss. Its other places as anywhere. |

At most two places a cube; when more are rolled, the order kept is haven, clam room, curse room, secret.

### 9.1 Bosses

On arriving at a layer the director rolls whether it holds a boss, from how many layers she has left without freeing
one:

| Layers left without freeing a boss | Chance this layer |
|---|---|
| 0 (she freed the last one, or this is layer 1) | 35% *(open, §16)* |
| 1 | 75% |
| 2 | 95% |
| 3 or more | 100%, and it is her 2nd claim on the layer |

When a layer holds one, it is her 2nd to 5th claim (rolled), the 2nd when certain. That cube becomes the **arena**:

- Its terrain is the cube's own with a basin 24 m in radius opened in its middle and its shelves removed. The edges,
  seam bands and wells are untouched, so its neighbours never notice.
- It keeps two gates: the one she came in by (**back**) and the one opposite, or failing that the gate farthest from
  it (**forward**). Its other gates are plugged with a rockfall in the arena's own throat. A cube with only one gate
  cannot be an arena; the boss passes to her next claim.
- Nothing else is in it: no creatures, no mouths, no drift. Its wells are sealed over until the boss is freed.
- Both gates seal when the battle begins (today's boss seals) and open when it ends.

Leaving the layer with its boss unfreed, or before meeting it, counts as a layer left without one. The warm-up layer
counts as neither.

## 10. Memory and persistence

Loaded things and remembered things are kept apart.

**`CubeRecord`** (a few hundred bytes to a few KB), kept for every claimed cube of her layer and the layer above:

- claimed, and its claim number; its grant (places, arena or not, plugged gates);
- which spawns have been made, which are freed or gone; creatures written back into it, where they stood;
- pickups taken, seals opened, sub-levels done;
- its fog of war, as a 64 × 64 bit mask;
- which of its edges the wake current holds.

A cube's terrain, meshes and nodes are **never** saved: they are regenerated from the seed, and the record is laid
over them. That is how (0,0) is the same (0,0) after the ring in §8.2.

**What is dropped:** on diving to a new deepest layer, the records of the layer two up are discarded; she can never
return there. Kept for the whole dive: the director's counters and the boss streak.

**Checkpoints:** entering a haven writes the save: seed, her run (as banked there), the director, the records of her
layer and the one above, and the haven's cube and mouth. Continuing a dive after quitting puts her back in that
haven. Dying ends the run, and the save with it: the next run starts at the top.

## 11. What happens to today's systems

| Today | In the cube world |
|---|---|
| `DiveGraph`, rows and lanes, holes ringed by kind, the chart | Retired. The pause menu's chart becomes the layer's claimed cubes. |
| `LevelPlan`, `PlaceRules`, level templates | Replaced by `ReefDirector` (§9) and per-cube stocking rules. |
| `CaveLayout` carving, `TerrainNoise`, `WallStrata`, `ReefTerrain` | Kept, called per cube with global coordinates. |
| `LevelValidator` | Becomes tests (§4.2). |
| Havens, leak caves, labyrinths (`PrepareBehind`) | Kept as they are: classic `LevelMap`s behind mouths. |
| Leaks and the awake / asleep reef | Leaks only in leak caves. The open layers have no awake / asleep state. |
| `LevelView`, `LevelShape` | Split into per-chunk shape building and a pooled chunk view. |
| `PlaneWorld` | Reads `IReefField`; owns creatures globally (§5.3, §5.4). |
| Snapshot saves of a level in play | Replaced by records and haven checkpoints (§10). |
| The tutorial's two levels | Retired: the warm-up layer (§9) is where she learns. |

## 12. Tests that define "correct"

Core tests, run over many seeds and coordinates:

- **Determinism:** a cube made twice, in either order with its neighbours, on any thread, is identical byte for byte.
- **Seams:** for every edge, both cubes' samples in the seam band and apron are equal. Same for corners.
- **Gates:** never within 13 m of a corner; no water joins two cubes that share only a corner.
- **Connectivity:** inside a cube every gate reaches every other gate and every well.
- **Wells:** every floor face has at least two spots where §3's rule holds, and from any arrival point one of them is
  reachable and allowed by §7.
- **Ring:** claim a ring of four cubes, change things in the first, come round: it is as it was left.
- **No duplicates:** load and unload a cube ten times; its creatures exist once.
- **Wake current:** after A → B → C, A–B cannot be crossed at any speed; B–C can.
- **Director:** over 10,000 simulated layers the grants respect every limit in §9; a clam room is always granted by
  the 3rd claim; the boss table's shares hold.
- **No stall:** a random walk of 500 claims and 40 dives never reaches a state with no legal move.

## 13. Performance budget

Target: 60 fps with no frame over 20 ms while crossing cube lines or changing layer, on the machine whose fans
prompted 0.44.0's optimisation.

| Work | Where | Budget |
|---|---|---|
| Terrain of one cube (two squares) | worker | ≤ 20 ms |
| Stocking of one cube | worker | ≤ 20 ms |
| Ground of one cube (8 chunks and arches, as meshes) | worker | ≤ 100 ms |
| Simplified ground of one cube | worker | ≤ 6 ms |
| Applying finished work | main | ≤ 2 ms a frame |
| Stream bookkeeping | main | ≤ 0.1 ms a tick |
| Field lookup (`IReefField`) | main | within 10% of today's `Map` read |

Steady state in memory: the cubes of the 3 × 3 squares round her (five to seven cubes) with terrain and ground, as
many below with a simplified ground, as many above as terrain only, at most seven stocked. Only one cube is simulated
in full, and the nav grid is smaller than a level's today.

Measured so far (one thread for the terrain; the scene on an RTX 3090): a cube's terrain 21 ms, its ground 72 ms
(110 ms at most), its simplified ground 1.4 ms; the stream's bookkeeping 18 µs a frame. Swimming a ring of ten cubes
for 90 s: 3.3 ms a frame on average, 6.1 ms at worst, none over 20 ms; showing a cube's meshes 2.3–2.5 ms at worst in
a frame; a cube in view undrawn for 0.14 s at most. (Measured with vsync off: Windows paces a window behind another
down to about 13 frames a second, which reads as 79 ms frames whatever the scene does.)

How it stays cheap, in one list: nothing generated on the main thread; no retries; two-stage generation; preview at
a quarter of the triangles; pooled nodes; `MultiMesh` flora; toroidal map textures; creatures asleep by distance; an
unload delay so lines can be crossed freely; one worker queue with cancellation.

The prototype's overlay shows each row of the table live, plus the pins and every cube's state.

## 14. The prototype

On its own branch, `inkdeep-cubes`, forked from `m6-redefine`; nothing merges back until §14.3 is met.

### 14.1 Scope

- A footprint of 5 × 5 squares (the herringbone's cubes wholly inside it), three layers. Shelves, arches, wells,
  closed edges, the reef's rim.
- Clementine, her light, her movement and collision, the camera. The debug overlay.
- Streaming with all four states, the layer below shown, diving and climbing with the camera move, the cooldown and
  the no-chain rule, the frontier drift and the wake current.
- **Not in it:** creatures, bosses, pearls, shells, sub-levels, havens, the director, saving. Records hold only
  claims, fog and shut edges.

It runs as its own scene (`--cubes`) with a slim `CubeWorld` simulation, so `PlaneWorld` is not touched yet. Her
movement and collision are first moved behind `IReefField` so both worlds share them.

### 14.2 Steps

| # | Step | Done when |
|---|---|---|
| 1 | `CellCoord`, `CubeId` (the herringbone), the edge, corner and floor ids, `ReefHash`, `EdgeSpec`, `CornerSpec`, `FloorSpec` (`src/Core/Gen/Cubes`) | Done: determinism, herringbone and gate tests pass. |
| 2 | Shared walls and throats, the cube's own carving, bays, shelves, arches, void, apron: `CubeTerrain.Make(space, coord)` | Done: seam, connectivity and well tests pass (`CubeTerrainTests`); terrain within budget. |
| 3 | A picture of a reef of 6 × 6 squares, three layers (one sheet a layer), drawn by `CubeTerrainTests` when `CUBE_SHEET` names a folder | Done. |
| 4 | `IReefField` and `ReefCollision` (`src/Core/Gen`); her stroke and her move against what blocks her in `BodyMotion`, shared by `PlaneWorld` and the prototype's `CubeWorld`; `CubeField`, the window of cubes round her | Done (`CubeWorldTests`): through a gate her speed and step hold to 0.1% across the cube line; a long swim over five cubes is the same tick for tick with a 3 × 3 window moving with her as with every cube held. |
| 5 | `ReefStream`: wants, queue, states, pins, unload delay; `TaskCubeWorker` on the thread pool | Done (`ReefStreamTests`, with a worker the test runs by hand and with the thread pool's): her cube's work first, terrain before what is built on it, two jobs out at a time; crossing a line back and forth lets nothing go; what she leaves goes after 3 s, pinned cubes not; work no longer wanted given up; the layer left kept as masks while she may climb; the bookkeeping 25 µs a frame. |
| 6 | `CubeMain` (`--cubes`): Clementine, her light and the camera, the game's own views; `CubeShapes` (a cube's ground and simplified ground, as meshes, on the worker); `ReefStreamView` (pooled nodes, a time budget a frame) | Done: a 90 s swim round a ring of ten cubes (`--cube-route --cube-perf=90`) shows no seam, no missing cube in view, and no frame over 20 ms (§13). The toroidal map textures wait for fog of war (step 9). |
| 7 | The layer below: its simplified ground (its full one once a dive is coming), the water's fog under her plane, one flat murk floor following her at that layer's own void | Done: through the void she sees the layer below, blurred, and murk under it; nothing deeper is drawn. Over the ring swim no cube within 40 m of her, on her layer or below it, is ever undrawn. |
| 8 | Diving and climbing: `CubeDiving` (§3's rule both ways, the 30 s cooldown, the no-chain reach, climbing only from the deepest layer; `CubeDivingTests`); the prompt; Shift or d-pad ↓ to dive, Q or d-pad ↑ to climb; the game's dive with the camera drawing in to 60% and out, the iris, the layer swap; the middle looping while the cube she arrives in is not ready | Done: `--cube-dives` dives twice and climbs once by itself; with the 30 s cooldown (105 s run) nothing waited for a cube (held 0.00 s), the worst frame 16.8 ms, the worst during a change of layer 6.7 ms. |
| 9 | `LayerRecords` and `ReefRecords` (claims, shut edges, fog of war a bit a metre, the layer left opened again and records two above her deepest let go); `CubeCurrents` (the drift down a field of steps over the water to the nearest gate into unclaimed water, else a way down; the wake current out of each shut edge's throats, with her held at its line); streaks of brighter water for both (`cube_current.gdshader`); the route planned leg by leg round shut edges | Done (`CubeRecordTests`): claimed once, kept as she left it, the ring of three cubes back into the first, the edge two crossings back shut, no dash through it, the drift leading her 18 m nearer new water in 40 s idle. In the scene, two minutes of the route: six cubes claimed, four edges shut, no frame over 20 ms. Fog of war is kept but not yet drawn (the prototype has no map). |
| 10 | Measure against §13 with the overlay and `--capture` runs; write up | Numbers in hand for the decision. |

### 14.3 The decision

Go on to §15 when: every test of §12 that the prototype covers passes; every row of §13 is met; a swim of ten cubes
and three layers shows no seam, pop or pause; and the reef is worth exploring by eye. If terrain reads as boxes (a
grid visible in where the walls are), the fix is tried in the prototype first: wider gate ranges, seam bands of
varying depth, chambers allowed to straddle an open edge by being written from the edge spec.

## 15. After the prototype

Each stage ends with the game playable.

1. **`PlaneWorld` on `IReefField`:** collision, nav, light stops, fog and corruption per cube. Classic maps still run
   through the same interface (sub-levels, tests).
2. **World-owned creatures:** stable ids, sleep by distance, records, the leash, pools. The murkling roster stocks
   cubes by layer.
3. **The director and sockets:** secret rooms, curse rooms, clam rooms and havens behind mouths, by §9's rules.
4. **The arena and the boss table.**
5. **Records and haven checkpoints;** the chart of claimed cubes.
6. **Retire** the dive graph, level plans, the hole views and the loading splash between reefs; rewrite GAME, DIVE
   and LIGHT-AND-GRAPH to the reef as it then is.

## 16. Still open

Each has the default this plan assumes; none blocks the prototype.

1. **The boss chance after she has just freed one** is not given in the rules; 35% is assumed.
2. **How deep the reef goes** ("dozens"): 36 layers assumed. It only sets where the bottom is.
3. **The warm-up layer's places:** assumed to be as anywhere (secrets, clam room, havens) but no curse rooms.
4. **The footprint** (9 × 9) and **S** (64 m) are the two numbers the prototype is most likely to change.
5. **Stair-stepped cliffs:** where rock drops into the void its face shows 1 m steps, the rock/water outline being
   a yes/no on the 1 m grid. The fix is to carve with a continuous margin (signed distance to the chambers, tunnels,
   bays, walls and throats) and lay heights on it, as the game's own reef terrain does; the seam tests guard it.
6. **The cube grid's trace:** the shared walls along cube lines still show, faintly, as a grid of rock fins between
   the caverns. If it reads in play, the next tries are fewer walls (open edges with wide gates) and chambers written
   by the edge across an open line.
