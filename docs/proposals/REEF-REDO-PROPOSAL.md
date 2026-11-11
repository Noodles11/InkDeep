# Proposal: the reef made anew, a living illustration

The levels today are a heightfield: rock walls rise through her plane, open floor lies under it, and the felt-tip look
(look 6) holds the reef back in flat marker tones. This proposal replaces both the shape and the look. It keeps the
sim, the creatures, the places, the dive and the plan of a run. Nothing here is built yet.

## 1. The goal in one breath

Water everywhere. The reef is a deep, many-layered living structure under and around her, not smooth rock in her way.
It is painted softly, so it sits back and the jellies and mobs carry the picture. One glance at a screenshot tells
how deep she is. Every move she makes stirs the water, and the reef answers.

Visual direction: Naiad (HiWarp). What to take from it:

- Flat, soft, painted shapes with gentle gradients. No hard outlines on scenery. Few textures.
- A world that answers the player: reeds part, petals drift, ripples spread, animals follow and scatter.
- Calm colour, with the player and the creatures as the brightest, most saturated things on screen.

## 2. The space: open water over a layered reef

- **Mostly water.** About 85% of her plane is open. Only what crosses her plane blocks her: coral heads, branch
  tips, the legs of arches, the stalks of table corals, kelp columns. Everything else lies below or rises above.
- **Layers, read from the camera down.**

  | Layer | Height from her plane | What is there | How it is drawn |
  |---|---|---|---|
  | Canopy | +2 to +6 m | table-coral tops, overhanging branches, kelp crowns | dark, soft, translucent silhouettes; fade over her (as canopies do now) |
  | Her plane | −1 to +2 m | the coral that blocks, fish, mobs, places | full colour, sharpest, a soft rim light from above |
  | Under-reef | −2 to −8 m | thickets, holes and arches she sees down through, fish swimming among them | one step hazier and bluer |
  | Deep reef | −8 to −18 m | the reef's lower tiers, big silhouettes | two steps hazier; shapes only |
  | Floor or void | −18 m and down | sand and rubble on shallow depths; nothing on the deep ones | a soft gradient, the depth's own colour |

- **Holes, branches, pathways.** The reef is woven, not walled. Lanes of open water wind through coral thickets.
  The thickets have gaps she can slip through (shortcuts), rings and arches to pass under or through, and table
  corals to swim beneath. The lanes cross over and under each other: a lane at her plane runs over an under-reef
  tunnel that a school of fish uses.
- **The camera's tilt does the depth work.** At 60° tilt the layers slide against each other as she moves
  (parallax). That motion is the single strongest cue that the water is deep, and it costs nothing.

## 3. Generation

- **Keep the graph, change what it carves.** The cave graph (`CaveLayout`: a chain from the start to the way down,
  branches and loops, places on it) stays as the plan of lanes. Instead of carving water out of rock, it marks where
  the reef must leave her plane open.
- **Reef as pieces, not a heightfield.** Between the lanes the generator grows coral structures from a small kit of
  procedural pieces:
  - branching coral (staghorn: tubes that fork, grown as short L-systems)
  - table coral (a plate on a stalk: blocks at the stalk, open underneath)
  - arches and rings (holes to pass through)
  - heads and boulders (brain coral, rounded masses)
  - sea fans, sponge tubes, anemone beds, soft corals, and kelp columns
  Each piece has a height range, so it lands on one layer or spans several. Density thickens away from the lanes and
  thins toward them, so the lanes read as clearings.
- **Woven paths.** Where a lane meets a thicket, the generator may place an arch chain over it, a ring through it or
  a table-coral underpass. Some side paths run on the under-reef layer only, for fish.
- **The floor** stays a heightfield, but low-resolution and smooth (sand, rubble, a shelf edge). Deeper depths drop
  it out of sight: the Trench and the Abyss are reef pillars over a void.
- **Collision stays 2D and deterministic.** The sim never sees meshes. Each piece gives a few primitives at her plane
  (circles and capsules: a stalk, a branch tip, an arch's foot). `PlaneWorld.Clear` tests them through a spatial
  grid. The validator floods that slice for reachability, the 90 m way down and the area rule (open water now counts
  nearly everything, so the rule becomes the level's swimmable extent).
- **Fish routes.** Schools follow the lanes and the under-reef paths and thread the holes, so fish are seen swimming
  through the reef, not over it.

## 4. The look: painted, quiet, deep

- **Depth reads from colour alone.** Each layer below her plane is tinted toward the depth's water: lighter value,
  less saturation, hue toward the water. The steps are soft. Each depth has its own ladder (see 4.1), so a
  screenshot says where she is.
- **Painted surfaces.** Each coral kind gets two or three flat tones (lit, shade, a rim), a world-space brush-stroke
  texture that breaks the flats softly, and edges that dissolve into the water through a brush mask. No felt-tip
  line on the reef. The creatures keep their outline and colours, so they always sit in front of the scenery.
- **The reef never competes.** It stays below the creatures in value contrast and saturation. Only cleansing light
  and Clementine glow. Corruption keeps its ink look.
- **Light.** A soft sun from above lights the tops of the coral at her plane. God rays stay, softer. Light pools on
  the floor as painted dapples, never sharp caustics. On deep levels her light paints the reef round her, and that
  is the only colour there.
- **Life in the water.** Drifting motes, marine snow, distant fish silhouettes on the deep layers, slow shafts of
  light. All of it is quiet and low contrast.

### 4.1 The depth ladder (first draft)

| Depth | Her plane | Under-reef | Deep / floor | Signature |
|---|---|---|---|---|
| Shallows | warm coral and sand tones, sunlit | turquoise haze | pale sand, sun dapples | staghorn and tables, bright fish |
| Kelp | olive and amber | green haze | dark green | kelp columns as the reef, light through the fronds |
| Galleon | rust and brass on timbers | grey-teal | dim silt | coral on wreck timbers and rubble |
| Carnival | the Leak's neon, flickering | violet | deep purple | tank colours spreading through the coral |
| Trench | slate and teal biolume | navy | void | pillars over nothing |
| Abyss | only what her light finds | black | black | she is the lighting |

## 5. Every movement felt

This is the Naiad lesson, and it is the part to build first.

- **A water-flow field round her.** A small texture that follows the camera (about 64 m across, 256² cells) holds
  the water's motion. Her swim, her jets and her dashes write velocity into it, and it fades and drifts. Mobs and big
  fish write into it too, so a threat stirs the water before it arrives.
- **Everything soft reads the field.**
  - Sea fans, soft corals, anemones, kelp and sea grass bend away and spring back.
  - Marine snow and motes are carried along her wake.
  - Sand puffs up from the floor where she passes low; silt hangs and settles.
  - Small fish dart off, and schools part round her and close again behind her.
  - Polyps on the coral at her plane close as she brushes past, and open again.
  - A soft refraction ring spreads when she sets off, turns or dashes (`WaterSwells`, as now).
- **Cheap by design.** One texture update a frame, read in the vertex shaders of the instanced flora and in the
  particle shaders. No per-object sim.

## 6. Lightweight

- **Instanced pieces.** About 20–30 meshes per depth in MultiMeshes, by 32 m tile, with a visibility range. Sway and
  polyps happen in the vertex shader.
- **No terrain shadow maps.** Ambient occlusion is baked into vertex colours when a piece is placed. Soft shadow
  pools under tables and arches are painted decals. This retires `SunMap` and the sun's shadow cascades.
- **The deep layers are cheap.** Below about 10 m, pieces swap to low-poly versions; on the deepest layer,
  silhouette cards.
- **A lighter post pass.** Fog by layer replaces most of the blur taps. One soft blur only for the canopy above her.
- **Targets.** A dive level generated in under a second, the next one ready in the background as now, and 60 fps on
  the game's minimum GPU with headroom.

## 7. What changes in the code

| Area | Change |
|---|---|
| `src/Core/Gen/TopDown` | a new `ReefLayout` (lanes from the graph, pieces, the floor); `LevelMap` gains a list of pieces and a collision slice; the heightfield becomes the floor only |
| `LevelValidator` | floods the collision slice; rules restated for open water |
| `PlaneWorld` | `Clear`, `Move` and `WallNormal` read the slice's primitives through a grid |
| `src/Game` | a new `ReefView` (piece meshes, the floor, flow field) replaces the terrain half of `LevelView`; a new look replaces look 6 in `reef_art` and `topdown_post` |
| Minimap | drawn from the slice: lanes as water, thickets as the depth's reef tone |
| Kept | creatures, bosses, places, caves' rules, the dive, the havens, `ReefLook` (extended with the layer ladder) |

## 8. Order of work

Each step ships on its own.

1. **A look prototype** to settle the look before any game code changes: one scene, one depth, the layers, a few
   pieces, Clementine's stand-in swimming through, the flow field. Built either as a test scene in Godot or as a
   browser page (three.js) you can open on your phone, as the cave charts were.
2. **The flow field and reactive flora** on today's levels. It improves the feel at once and is independent of the
   rest.
3. **The reef pieces and the collision slice** in Core, with their tests.
4. **`ReefView` and the new look**, depth by depth, starting with the Shallows.
5. **The deep depths** (pillars over the void), then retire the old terrain path and look 6.

## 9. Open questions

- The prototype: a Godot test scene, or a browser page?
- Leak caves: keep them as caves in rock (a contrast to the open reef), or open woven reef too?
- Look 6 (felt tip and highlighter) is retired by this. Should the creatures keep their felt-tip outline as their
  readability cue, or move to a soft rim light that fits the painted look?
