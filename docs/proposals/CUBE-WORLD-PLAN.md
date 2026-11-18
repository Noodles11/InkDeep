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
they exist. A window 24 squares across is about 1.5 km, well inside what a `float` holds to the millimetre.

**No footprint: the reef runs on.** Nothing is laid out ahead of her. A cube exists only once she comes near it, made
from the seed and its own coordinates (§4), so it matches its neighbours and the cubes over and under it whichever is
made first, and the reef carries on every way she swims. A run reads each layer in a **window** of 24 × 24 squares
*(tune)* centred where she comes onto that layer (§5.3: the game's systems read a layer as one map that size); no cube
sticking out of the window is ever made or laid, and a gate at the window's line leads into rock. The reef itself is
the same whatever window it is read in (no spec reads the window), so a layer's window can be set anywhere, any time.
While she is on a layer, the window of the one under it (one she has never been on) follows her, so the cube under her
is always made; as she dives onto it, it is made again round her arrival should she come down more than three squares
off its middle. A layer she has been on keeps its window: climbing back, or diving again onto it, only where it holds.

**Why 64 m squares.** The camera sees about 62 × 35 m (35° at 55 m). With S = 64 her view rarely reaches past the
cubes next to hers; where it does, the dark of the rock stands in (§5.5). At her 6 m/s cruise a cube is
some 10–20 s straight across, 30–60 s as the water winds: long enough to ready what comes next, short enough that she
is always arriving somewhere.

## 3. What a cube holds

A cube of layer Z is a heightfield on the 1 m grid over its two squares, as a level is today, measured from its layer's swim plane, with one
new idea, the **void**:

| At a point | It is |
|---|---|
| height > 0 | Rock: blocks her and everything else. |
| a roof over water | Rock over a roofed gate's throat or a tunnel bored through rock (§4.4): she swims under it. |
| 0 ≥ height > void | Water over a **shelf**: a floor she sees and cannot dive through. |
| height = void | Water with nothing under it: the layer below shows through. |

**She may dive at a point** when, for her whole body's radius: this cube is void there, and the cube below is water
there with no roof over it. A shelf here, rock below ("an obstacle") or a roof below all refuse the dive. **She may
climb** at a point when the same holds read from the layer above. One rule, both ways.

Void lies only over the layer below's caverns (§4.3), so looking down through it she always sees that layer's open
water; shelves, roofs and the caverns' shapes decide where.

## 4. Generation: plan first, then geometry

Everything is a pure function of the world seed and coordinates. No cube asks a neighbour anything, so cubes can be
made in any order, on any thread, and always agree.

### 4.1 The connectivity plan (boundary conditions)

Three small specs, each hashed from the seed and the id of the thing two or three cubes **share**:

- **`EdgeSpec(EdgeId)`**: open or closed, and the seed of its shared wall's shape (its line's, the same on every
  layer: §4.3). Each edge is open by its own chance, 55% *(tune)*; a cube left with none open has the one of its six
  nearest to opening opened, so no cube is ever shut in. Both rules read only the edge and the two cubes beside it, so
  no layer is planned as a whole: a layer is not certain to join up everywhere, but nearly all of it does (§12), and
  she can always dive on. An open edge has one gate, or two on 35% of edges, each 6–13 m wide, its sides never nearer a corner
  than 13 m. Two cubes may share two edges (along a long side), each with its own gates.
- **`CornerSpec(CornerId)`**: rock 8–10 m round the corner, its size wandering slowly with depth, so a corner stands
  on down the layers as one pillar. With gates kept off the corners, no water ever joins two
  cubes that share only a corner, so she cannot cross diagonally.
- **`FloorSpec(FloorId)`**: per square, one well, or two (50%), discs 5–7 m in radius where the upper cube must be
  void and the lower open water. They are the sure ways down; any other spot where §3's rule happens to hold is a way
  down too. A square's wells lie in two opposite quadrants of it, 9–15 m from its middle along each axis, the pair of
  quadrants set by the floor's parity: the wells she arrives by and those she may leave by are never in the same
  quadrant, and at least 18 m apart.

A cube's boundary conditions are its six outer edges' specs, its six corners' specs, and the floor specs under and
over its two squares.

### 4.2 The geometry

1. **What the edges decide** (`CubeTerrain.SharedField`), as signed distances (positive in rock) from the edges and
   corners alone, one field for the whole layer, so every cube that holds a point works out the same:
   - its **wall** along the line, whose middle wanders up to 1 m off it and each of whose faces swells by up to 3 m on
     its own, but which always covers at least 3 m either side (so the apron is made from the edges alone); past its
     own stretch of line it tapers away. Its wander is read at the layer's depth from its line's own noise, so the
     same wall runs on down the layers, moving a little from each to the next;
   - each **gate's throat**, cut through the wall and on 10 m into both cubes (closing over 2 m past that), swaying
     from side to side and flaring where it opens into each; 75% of gates are **roofed** *(tune)*, the wall's rock
     running on over the throat (§4.4), the rest open clefts;
   - each **corner's** rock.
2. **The cube's own water**, across both its squares (no wall between them): the reef's **caverns**, wherever the
   stacked cavern field (§4.3) is over its cut (0.2, about a fifth of the reef *(tune)*), and a chamber round each
   well (over it and under it). Gate mouths and wells are joined by a spanning tree of the shortest tunnels (bowed
   curves 5.6–8.8 m wide, from the cube's own hash) plus a loop or two; a cavern not joined to them by then is filled
   if small (under 30 m²) or bored through to the nearest water that is. 75% of tunnels *(tune)* are **bored**: where
   they run through rock (outside the caverns of this layer and the one above) they are roofed (§4.4). Rims wobble by
   low noise; then **bays** are bitten into the rock within 5 m of the water open to the sky where the bay noise is
   high, so walls wander rather than trace the plan (none off a roofed tunnel: no skylights in its roof). Every gate
   of a cube reaches every other and every well.
3. **Shelves and void**: water is void where the shelf noise (global coordinates, so it runs on across cube lines)
   opens it **and** the layer below has a cavern there (§4.3), else shelf; round a well down it is void, wobbling
   outward from its radius; under a roof it is a shelf.
4. **The face**: the cube's own water as a signed distance (positive in rock, widened 0.6 m, blurred), joined with
   the edges' field: the walls' rock over it, the throats' water over that. Within 2 m of an outer edge (the apron)
   only the edges' field counts; over the next 3 m the cube's own blends in. The face is the field's zero line, which
   runs smoothly between the grid's points: no 1 m steps anywhere.
5. **Heights** on the field: all noise (rock relief, shelf floors) is read at **global** coordinates. Rock rises from
   its face over 5 m to its own 3–6.5 m; the floor falls from the face to a shelf's or the void's depth over 1–4 m
   (the deeper, the further). A shelf's floor is never flat (`CubeTerrain.Relief`): a slow noise stepped into terraces
   a few metres across, each top all but level and its edge lowered gradually into a slope to the next, 1.8 m deep in
   all (half on the warm-up layer). The cube's own faces grow **knobs and bays** (`CubeTerrain.Knobs`): a noise about
   6 m across pushes the face out into the water or back into the rock by up to 3.2 m, a knob only as far as its way
   keeps 6 m across (knobs from both sides together, its room read over 4 m round it), a bay only as far as its wall
   keeps 3 m; the noise drifts on a little from each layer to the next, as the walls do. Near the cube's edges, where
   the shared field decides, there are none. A shelf's floor drops into the void along the shelf noise's contour as a bank, and sinks
   smoothly into a well. The ground's edge over the void and a roof's outline follow their fields' smooth lines too
   (`LevelView.SurfaceMesh`'s field).
6. **Apron**: the cube's arrays carry 2 m of its neighbours' samples round them (the ground's smoothing reads two
   samples past a cell), all of it decided by the edges, so meshes, normals and soft masks are right up to the line.

**Generation never retries.** Today's generate-validate-retry cannot run while she swims. The construction above is
valid by design; `LevelValidator`'s checks move into the test suite, run over thousands of seeds and coordinates.

**Kinds of cube** (`CubeKind`, `CubeTerrain.KindOf`). Each cube has a character, from the seed and the cube alone.
Only its own carving changes; its edges stay as its neighbours have them. A boss's arena is always Plain.

| Kind | Share | What it is |
|---|---|---|
| Plain | 40% | The reef's caverns as they come. |
| Hall | 20% | A great wobbling basin of open water, 13–18 m across, carved in its middle and joined to its nearest water by a wide way. |
| Spires | 20% | Rock pillars, 1.3–2.3 m in radius, stood on a jittered lattice in its wider open water, each with 3.5 m of water all round it, none near its gates' mouths or wells. Laid on the face's field itself, so the blur never wears them away. |
| Drop | 20% | One of its two squares falls away into the void far more than elsewhere: a cliff over the layer below. The other square keeps its floor, so it still has room for its sockets. |

### 4.3 One reef down the layers: the strata

A layer is not made alone: its depth fields are **stacked noise maps**, one every 5 layers *(tune)*, each layer a
smooth blend of the four round it (a cubic B-spline down the stack, kept to the maps' own spread;
`CubeSpace.Strata`). Layers next to each other read nearly the same, and over about ten layers (100 m) the reef has
wholly changed. What reads them:

- **The caverns** (§4.2 step 2): the reef's open water, so a layer's caverns lie largely where the layer above has its
  own, shifted and reshaped a little. About two thirds of a layer's water open to the sky lies where the next layer's
  does, a third where a layer eight down does (`CubeTerrainTests.Each_layer_carries_on_the_reef_of_the_one_above`).
- **The walls and corners** (§4.1, §4.2 step 1): the same lines every layer, their wander read at the layer's depth.
- **The void** (§4.2 step 3): only over the layer below's caverns, so the void and the open water under it are one
  shaft.
- **How far her walls rise** (§5.5): on up past the camera where the layer above is rock; under the layer above's
  caverns only to about its floor (`CubeTerrain.UpperOpen`), so the walls she sees rising are the reef above going on.

### 4.4 Roofs

Rock carried over water she swims through (`CubeTerrain.Canopy`): the throats of roofed gates, inside their wall's
band, and tunnels bored through rock, each with the rock 2 m round it. From above a roofed gate leaves its wall whole,
an opening carved through it; a bored tunnel runs hidden through the rock between caverns. A roof is drawn as a slab
(`CubeShapes.Roofs`) whose underside lies 3.6 m over her plane at its edge (the lintel over each mouth) and which rises
with the walls round it inside, made over each cube's own squares only, so neighbours' roofs meet at full height.
Roofs are rock over the water on the layer's map (`LevelMap.Roof`), so the cutout takes a roof with its wall, whole.
No roof lies within 2 m of a well, nor over a boss's basin.

### 4.5 Two stages

- **Terrain**: heights, void, roofs, the dive mask. Depends on nothing but seed and coordinates. Cheap.
- **Stocking**: creatures, pickups, decoration, mouths of sub-levels. Depends on the terrain and on what the director
  (§9) grants the cube.

Keeping them apart is what makes the preview layer and far neighbours nearly free.

## 5. Streaming

### 5.1 States

| State | Holds | Which cubes |
|---|---|---|
| **Active** | Terrain, meshes, stocking, full simulation. | The cube she is in. |
| **Preloaded** | Terrain, meshes, stocking ready; creatures asleep till she is near. | The cubes her cube's open gates lead to, those the wake current (§8.2) has not shut. |
| **Preview** | Terrain and its full ground. | A cube a gate of a Preloaded one leads to, while that gate is within 50 m of her (in the camera's view), so no water she sees ends at a line; with a dive (or a climb) coming, the cubes the gates of the one she will land in lead to. |
| **Below** | Terrain and a simplified ground (2 m grid). | The cube under hers, seen through her void. With a dive coming it is readied as Preloaded: she lands in it. |
| **Mask** | Terrain only, not drawn. | The cube over hers, while she may climb there (to know where she may). |
| **Unloaded** | Its record (§10), nothing else. | Everything else: a cube behind a closed edge is never made for her. |

So a run begins with one cube Active, the few its gates lead to, and the one under it; every other cube is made only
as she swims near it. The layer above is never drawn (it would hide her from a camera looking down); only its
terrain is kept, to know where she may climb. What is not drawn round her the dark of the rock fills (§5.5).

**Prediction:** the cube past the square beyond a gate she is heading for has its terrain and ground made early once
she is within 20 m of that gate and moving toward it, if a gate of the cube between leads into it.

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

### 5.3 The simulation reads its layer as a level

The game's systems (collision, her light, the nav grid, the maps, creatures, bosses, the save's snapshot) read one
`LevelMap`. A layer is given to them as one: a map as wide as the window, begun as rock (made as soon as she
arrives on the layer above it, round her arrival there; made again round where she comes onto it, should that lie
off its middle), in its own metres: where it lies in the reef is `LevelMap.ReefOrigin`, and the game takes its points
into the reef's metres and back wherever it meets the cubes. `CubeReef` makes each cube's terrain the first time the stream asks for it (on any
thread, kept while its layer is) and lays it onto its layer's map on the main thread the frame after (`Settle`), so a
layer's map holds only the cubes she has come near, and her layer's reef life, its cutout mask and its sun map follow
as each is laid. Water not laid yet reads as rock, so nothing swims or shoots into it. A layer is then a level to the
game: `PlaneWorld` is made on it as she arrives, sub-levels behind its
mouths are classic levels as before, and a layer she climbs back to keeps its map.

- **`IReefField`** (`HeightAt`, `IsOpen`, `Gradient`) with `ReefCollision` stays the one interface for rock; a level's
  map and the window of cubes (`CubeField`) both answer it.
- **What streams is the drawing:** `ReefStream` and `ReefStreamView` show the cubes round her as she swims (§5.2,
  §5.5); the simulation never waits for them.

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

- **Meshes:** eight 32 m chunks a cube and its roofs, built off the main thread with the level's own surface builder.
  Chunk edges share their rim samples and take normals from the apron. Quads all in the void are left out: there the
  layer below shows.
- **Map textures** (fog of war, corruption, baked sun shadow) that shaders sample by world position become one
  256 × 256 texture addressed **toroidally**: square (X, Y) is written at tile (X mod 4, Y mod 4) and the sampler
  wraps (four tiles a side, so a cube of two squares never wraps onto itself). When she crosses a line, only the tiles
  of newly loaded cubes are rewritten; nothing scrolls.
- **Flora and rocks:** one `MultiMesh` per kind per cube, filled off-thread, shown in one step.
- **Pools:** creatures' views, pickups, particle emitters and decoration nodes are taken from and returned to pools,
  never created or freed while she swims.
- **What is not drawn:** every square round her with no cube of her layer drawn is filled at her plane in the cut
  rock's near-black (`ReefStreamView.Pall`, laid again as her square changes or cubes come and go), so the reef never
  ends in a cut edge with the water or the layer below showing past it.
- **The layer below:** the same chunk meshes at half resolution (2 m grid), no flora, no stocking, drawn 8 m down
  through the void with the water's depth fog. Its own void is filled with flat murk, so nothing under it is ever
  needed. Her light reaches it faintly.

## 6. Fog of war and unclaimed water

A cube is **claimed** the first time she swims into one of its gate throats (or dives into it). Until then only its
seam bands are ever revealed; the rest of it stays unseen however near she stands. Claiming is when the director
(§9) fixes what the cube holds, so nothing she has looked at can change afterwards. "A new cube she visits" in the
rules below means a claim.

## 7. Diving and climbing

- **Where:** §3's rule. No hint shows over a spot that allows it.
- **Controls:** down and up on the controller's d-pad dive and climb, and on the keyboard the arrows ↓ and ↑ (the
  bindings Dive and Climb; the keys fixed, as the arrows also aim).
- **Cooldown:** 30 s after any change of layer before the next, either way. The prompt shows the time left.
- **No chain dives:** after arriving, she may not dive within S / 2 (32 m) of where she arrived. If no reachable
  spot in her cube is that far (a small sealed pocket), the limit shrinks to 80% of the distance of the farthest
  one, so she can never be stuck.
- **Climbing:** only to the layer just above the deepest she has reached. On that layer the climb prompt never shows.
- **The animation:** today's dive (1.6 s) kept, with the camera moving in to 60% of its distance over the first half
  and back out over the second. Climbing is the same played upward: she rises, the layer she leaves falls away into
  blur beneath her.
- **No loading:** the cube under her has its stocking readied as soon as she has hovered over divable water for half
  a second or the cooldown has under 5 s to run. Arrival cubes hold no creatures (§9), so this is light. The
  animation's middle loops, Clementine sinking (or rising) through murk, until the layer she goes to is all there
  (every cube the stream wants round her arrival drawn, its reef life worked out ahead, a new layer's arena made on
  the pool as the dive begins; six seconds at most); there is never a splash or a loading screen.
- **What changes underneath:** the layer she leaves becomes "the layer above": its cubes' records are kept, their
  nodes freed, only dive masks held. Shots and particles in flight are dropped, hidden by the animation.

## 8. Currents

### 8.1 The frontier drift

In the Active cube a flow field points along the water toward the nearest gate into an **unclaimed** cube (when there
is none, toward the nearest unvisited well). It pushes her at 0.6 m/s *(tune)*, 13% of her swim speed: felt when she
drifts, trivial to swim against. It fades to nothing within 10 m of a mouth, a reward or a haven, and in a boss arena.
It is not drawn: it is felt (in the game, neither current shows; the prompt says when one holds her back).

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

`CubeDirector` keeps, per layer, what has been granted, and across layers the boss streak. A cube's grant is rolled when it is claimed, from the seed, the layer and its claim number, so the same seed
swum the same way gives the same reef.

Clam rooms, secret rooms, curse rooms and havens are all **sub-levels behind a mouth**, reached by the game's rides
and left back to the layer as she left it: a haven is the game's haven, a secret room its labyrinth, a curse room its
leak cave (the game's curse dens are its leak caves), and a clam room a small level of its own holding one
(`TopDownGenerator.GenerateRoom`). A cube's terrain carries up to two **sockets**: alcoves cut into its walls as a
level's cave opening is, at least 12 m inside the cube, 14 m from a gate's mouth, 9 m past a well and 20 m apart
(nine cubes in ten have one). A granted place opens its mouth in a socket. A cube with no free socket passes its
grant on to the next claim.

| Place | Rule |
|---|---|
| **Clam room** | Once a layer, on its 1st, 2nd or 3rd claim: one chance in three on the 1st, one in two on the 2nd if not yet granted, certain on the 3rd; 0 once granted. |
| **Secret room** | Each claim: 1 in 3. No limit. |
| **Curse room** | Each claim: 1 in 5. No limit. |
| **Haven** | Each claim: 1 in 4, until the layer has two; then 0. |
| **Boss** | At most one a layer; §9.1. |
| **Arrival cube** | The cube she dives into: no creatures, ever. It may hold mouths. |
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
| 3 or more | 100% |

When a layer holds one, its **arena** is fixed as she arrives: a cube two to four cubes from where she arrived along
open edges, with at least two open edges, none of its squares within two of hers (so it has never been drawn). Its
terrain is made again and laid over the layer's map:

- A basin 24 m in radius opened in its middle, and a way cut to it from every gate. Its edges, seam bands and wells
  are untouched, so its neighbours never notice.
- Nothing else is in it: no creatures, no mouths, no drift. Its wells are sealed (down and up) until the boss is
  freed, and nothing dives into it from above.
- While the battle runs its ways out are held shut, and they open when it ends.
- Its boss is the neighbour the Leak made the boss of the depth the layer stands for (layer 1 the first depth's), as on
  the dives, unless she has freed her; else one of the Drowned Court from the layer's pool (`BossCourt`,
  BOSS-GENERATOR §15).

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
over them. The save names the terrain generator it was made under (`CubeDiveSave.Terrain`); one made under another
is let go, since its seed no longer makes the same reef. That is how (0,0) is the same (0,0) after the ring in §8.2.

**What is dropped:** on diving to a new deepest layer, the records of the layer two up are discarded; she can never
return there. Kept for the whole dive: the director's counters and the boss streak.

**Checkpoints:** entering a haven writes the save: seed, her run (as banked there), the director, the records of her
layer and the one above, and the haven's cube and mouth. Continuing a dive after quitting puts her back in that
haven. Dying ends the run, and the save with it: the next run starts at the top.

## 11. What happens to today's systems

| Today | In the cube world |
|---|---|
| `DiveGraph`, rows and lanes, holes ringed by kind, the chart | Retired. The pause menu's chart becomes the layer's claimed cubes. |
| `LevelPlan`, `PlaceRules`, level templates | Replaced by `CubeDirector` (§9) and per-cube stocking (`CubeStock`). |
| `CaveLayout` carving, `TerrainNoise`, `WallStrata`, `ReefTerrain` | Kept, called per cube with global coordinates. |
| `LevelValidator` | Becomes tests (§4.2). |
| Havens, leak caves, labyrinths (`PrepareBehind`) | Kept as they are: classic `LevelMap`s behind mouths. |
| Leaks and the awake / asleep reef | Leaks only in leak caves. The open layers have no awake / asleep state. |
| `LevelView`, `LevelShape` | Split into per-chunk shape building and a pooled chunk view. |
| `PlaneWorld` | Made on a layer's stitched map as on a level (§5.3); its creatures marked by the cube they were stocked in. |
| Snapshot saves of a level in play | Replaced by records and haven checkpoints (§10). |
| The tutorial's two levels | Retired: the warm-up layer (§9) is where she learns. |

## 12. Tests that define "correct"

Core tests, run over many seeds and coordinates:

- **Determinism:** a cube made twice, in either order with its neighbours, on any thread, is identical byte for byte.
- **Seams:** for every edge, both cubes' samples in the seam band and apron are equal. Same for corners.
- **Gates:** never within 13 m of a corner; no water joins two cubes that share only a corner.
- **Connectivity:** inside a cube every gate reaches every other gate and every well. No cube is shut in; every edge
  is the same whatever window its layer is read in; from the window's middle, at least 85% of a layer's cubes are
  joined by open edges inside it. A layer made round a far point (past the reef's naught too) holds the reef there.
- **Made as she swims:** a layer's map holds only the cubes made, each laid on it as it is made; at rest the stream
  holds her cube, those its gates lead to (and past a gate in view), the one under hers, and nothing else.
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
| Ground of one cube (8 chunks and roofs, as meshes) | worker | ≤ 100 ms |
| Simplified ground of one cube | worker | ≤ 6 ms |
| Applying finished work | main | ≤ 2 ms a frame |
| Stream bookkeeping | main | ≤ 0.1 ms a tick |
| Field lookup (`IReefField`) | main | within 10% of today's `Map` read |

Steady state in memory: her cube and the three to six its gates lead to with terrain and ground (a few more past
gates in view), one below with a simplified ground, one above as terrain only, at most seven stocked; and a layer's
map as wide as the window (five float arrays of 1537², about 47 MB) for her layer, the one above and the one below. Only one cube is simulated
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

- A footprint of 5 × 5 squares (the herringbone's cubes wholly inside it), three layers. Shelves, roofs, wells,
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
| 2 | Shared walls and throats, the cube's own carving, bays, shelves, roofs, void, apron: `CubeTerrain.Make(space, coord)` | Done: seam, connectivity and well tests pass (`CubeTerrainTests`); terrain within budget. |
| 3 | A picture of a reef of 6 × 6 squares, three layers (one sheet a layer), drawn by `CubeTerrainTests` when `CUBE_SHEET` names a folder | Done. |
| 4 | `IReefField` and `ReefCollision` (`src/Core/Gen`); her stroke and her move against what blocks her in `BodyMotion`, shared by `PlaneWorld` and the prototype's `CubeWorld`; `CubeField`, the window of cubes round her | Done (`CubeWorldTests`): through a gate her speed and step hold to 0.1% across the cube line; a long swim over five cubes is the same tick for tick with a 3 × 3 window moving with her as with every cube held. |
| 5 | `ReefStream`: wants, queue, states, pins, unload delay; `TaskCubeWorker` on the thread pool | Done (`ReefStreamTests`, with a worker the test runs by hand and with the thread pool's): her cube's work first, terrain before what is built on it, two jobs out at a time; crossing a line back and forth lets nothing go; what she leaves goes after 3 s, pinned cubes not; work no longer wanted given up; the layer left kept as masks while she may climb; the bookkeeping 25 µs a frame. |
| 6 | `CubeMain` (`--cubes`): Clementine, her light and the camera, the game's own views; `CubeShapes` (a cube's ground and simplified ground, as meshes, on the worker); `ReefStreamView` (pooled nodes, a time budget a frame) | Done: a 90 s swim round a ring of ten cubes (`--cube-route --cube-perf=90`) shows no seam, no missing cube in view, and no frame over 20 ms (§13). The toroidal map textures wait for fog of war (step 9). |
| 7 | The layer below: its simplified ground (its full one once a dive is coming), the water's fog under her plane, one flat murk floor following her at that layer's own void | Done: through the void she sees the layer below, blurred, and murk under it; nothing deeper is drawn. Over the ring swim no cube within 40 m of her, on her layer or below it, is ever undrawn. |
| 8 | Diving and climbing: `CubeDiving` (§3's rule both ways, the 30 s cooldown, the no-chain reach, climbing only from the deepest layer; `CubeDivingTests`); the prompt; Shift or d-pad ↓ to dive, Q or d-pad ↑ to climb; the game's dive with the camera drawing in to 60% and out, the iris, the layer swap; the middle looping while the cube she arrives in is not ready | Done: `--cube-dives` dives twice and climbs once by itself; with the 30 s cooldown (105 s run) nothing waited for a cube (held 0.00 s), the worst frame 16.8 ms, the worst during a change of layer 6.7 ms. |
| 9 | `LayerRecords` and `ReefRecords` (claims, shut edges, fog of war a bit a metre, the layer left opened again and records two above her deepest let go); `CubeCurrents` (the drift down a field of steps over the water to the nearest gate into unclaimed water, else a way down; the wake current out of each shut edge's throats, with her held at its line); streaks of brighter water for both (`cube_current.gdshader`); the route planned leg by leg round shut edges | Done (`CubeRecordTests`): claimed once, kept as she left it, the ring of three cubes back into the first, the edge two crossings back shut, no dash through it, the drift leading her 18 m nearer new water in 40 s idle. In the scene, two minutes of the route: six cubes claimed, four edges shut, no frame over 20 ms. Fog of war is kept but not yet drawn (the prototype has no map). |
| 10 | Measure against §13 with the overlay and `--capture` runs; write up | Done: §14.4. |

### 14.3 The decision

Go on to §15 when: every test of §12 that the prototype covers passes; every row of §13 is met; a swim of ten cubes
and three layers shows no seam, pop or pause; and the reef is worth exploring by eye. If terrain reads as boxes (a
grid visible in where the walls are), the fix is tried in the prototype first: wider gate ranges, seam bands of
varying depth, chambers allowed to straddle an open edge by being written from the edge spec.

### 14.4 Where the prototype stands

Measured on the development machine (RTX 3090), vsync off while measuring: a 120 s swim of the route
(`--cubes --cube-route --cube-perf=120`: 13 moves of the window, 6 cubes claimed, 4 edges shut behind her) and a 105 s
run down two layers and back up one with the real 30 s cooldown (`--cubes --cube-dives --cube-perf=105`).

| §13's row | Budget | Measured | |
|---|---|---|---|
| A frame | none over 20 ms | 3.2 ms on average, 7.2 ms at worst swimming, 13 ms at worst with the dives; none over 20 ms | met |
| A frame during a change of layer | none over 20 ms | 7.2 ms at worst; nothing ever waited for a cube (held 0.00 s) | met |
| A cube in view not drawn | never | never, on her layer or the one below | met |
| Terrain of one cube | 20 ms | 21–24 ms on average, 30 ms at most | a little over |
| Ground of one cube | 100 ms | 72 ms on average, 120 ms at most | over at worst |
| Simplified ground | 6 ms | 1.1 ms | met |
| Applying a frame's work | 2 ms | 2.3–2.6 ms at worst (one mesh shown) | a little over |
| Stream bookkeeping | 0.1 ms | 21 µs | met |
| Reading the field | within 10% of a map's read | 73% of it, read as collision reads | met |

The three rows over budget are all work the frame never waits for: terrain and ground are made on the worker, a
cube's ahead of when she can see it (no cube in view was ever undrawn); and the 2.5 ms of showing a mesh is the one
mesh a frame that passes the 1.5 ms budget, the worst of it the first time a kind of mesh is drawn. The ways to bring
them in, if wanted: the carving's distance tests by the cube's grid rather than per primitive; 16 m ground chunks
(half the cost each to show).

Against §14.3:
- **Tests:** every test of §12 the prototype covers passes (determinism, seams, gates and corners, connectivity,
  wells, the ring, the wake current); the rest (creatures, the director, the long random walk) wait for §15.
- **§13:** met, but for the three worker-side rows above.
- **Ten cubes and three layers:** no seam, no missing cube, no pause, the ring and the dives as above.
- **Worth exploring by eye:** judged so, and built into the game (§15). Its rock's face is smooth (§4.2).

How it was seen (the prototype scene is gone now: the game is the cube reef): `godot -- --cubes` (WASD, the mouse to aim, Shift or the d-pad's down to dive, Q or its up to climb,
F3 for the overlay), `--cube-route` or `--cube-dives` to watch her do it, `--cube-perf=seconds` for the numbers.

## 15. In the game

A new dive is in the cube reef, in the game itself (`TopDownMain.Reef.cs`): her HUD, light, pearls and maps as on any
level. It is the only game: there is no other mode left. A verification run is in the reef too (`--cube-boss`, `--cube-chart=`, `--cube-save=`, `--cube-resume=`, `--autopilot` help verify it).

1. **The layer as a level** (§5.3): done.
2. **Creatures:** each cube she claims buds its stock (`CubeStock`): 3 + 4 × the layer's Menace of the roster's
   murklings, 9 m apart, none within 18 m of where she came in, at most one prime (from layer 2), from the seed, the
   cube and its claim alone; never on the warm-up layer or in the cube she dives into. Over 48 m from her they sleep.
   Leaving a layer keeps what is left of each cube's stock in its record; coming back puts it back where it was. Each
   claimed cube lays its inkseeds too (CORRUPTION, Inkseeds), the warm-up layer's as well, kept and put back alike.
3. **The director and sockets** (§9): done.
4. **The arena and the boss table** (§9.1): done.
5. **Records and haven checkpoints** (§10): reaching a haven saves the dive there (`CubeDiveSave` in the dive's save);
   continuing puts her back in that haven. The pause menu's chart is her layer's claimed cubes.
6. **The seeded levels' dive retired:** every dive is in the cube reef; diving down holes to a level below, the hole
   views and the dive graph's chart are gone, and GAME, DIVE and LIGHT-AND-GRAPH describe the reef. The dive graph and
   level plans stay in Core, where the generator (the sub-levels) and the test suite's levels are made with them.


## 16. Still open

Each has the default this plan assumes; none blocks the prototype.

1. **The boss chance after she has just freed one** is not given in the rules; 35% is assumed.
2. **How deep the reef goes** ("dozens"): 36 layers assumed. It only sets where the bottom is.
3. **The warm-up layer's places:** assumed to be as anywhere (secrets, clam room, havens) but no curse rooms.
4. **The window** (24 × 24) and **S** (64 m): each layer's map is placed round where she arrives on it, so diving on is
   endless; on one layer she can still swim only about 750 m from her arrival. A window sliding with her as she swims
   would make each layer endless too.
5. **The cube grid's trace:** with the walls' faces wandering and no pockets against them, the lines no longer read
   as fins; the throats' ends can still leave a small point of rock. If the grid reads in play, the next tries are
   fewer walls (open edges with wide gates) and chambers written by the edge across an open line.
