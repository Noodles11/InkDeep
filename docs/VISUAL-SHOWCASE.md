# The game's look: the Blue Hollow

First built as one showcase reef (Depth 3, "The Blue Hollow"), it is now the game's look. Every depth is painted in it
(`ReefLook.For`: the Blue Hollow itself at Depth 3, lighter, clearer and sunnier toward the top (`ShowcaseUp`), darker
and more her own light's toward the bottom (`ShowcaseDown`), each depth keeping its name); havens keep the bay's
palette and leak caves their dark, painted the same way. The title is painted too. `--showcase=0` draws the game in
the felt-tip look it had before (docs/ART-LOOKS.md, look 6).

## The look it is after

The reef as it was painted on 27 October (art look 0: textured rock, sand with caustics, depth told by soft focus),
in a deep clear blue, after two references: a lit night reef (pale sand pooling the light, navy water, god rays) and
a coral canyon seen from above (blue-slate ledges thick with coral in pink, violet, orange and yellow, a sandy channel
between).

## What is built

- **Its palette** (`ReefLook.Showcase`, "The Blue Hollow"): a cool grey water with only a breath of blue, slate-grey
  rock, pale grey sand; coral in four colours; a cool pale sun, some god rays, caustics; a heavy vignette. Painted in
  look 0 (`ArtLook.Painted`, set by `TopDownMain.ApplyLook` and the title).
- **Her light brings the colour** (`topdown_post.gdshader`'s `unlit_saturation`, `unlit_dim`; `CameraRig.SetShowcase`):
  outside her glow and her beam (and what sealed leaks have woken) the reef keeps 30% of its colour and 88% of its
  light, so it reads grey until she lights it, and her light is plain to see.
- **Fog under her plane** (`below_fog`): the water thickens below the swim height, 80% of the way to the water's grey
  over the first 5 m down, thinner where her light reaches.
- **Crisp water** (`CameraRig.SetShowcase`): the deeps barely blurred and a thin veil over them, so the reef reads as
  in the references, not through a haze. The water's blue tint and the cool shadow grade are eased so the coral keeps
  its colour (`topdown_post.gdshader`, `reef_showcase`).
- **Coral crust** (`reef_surface.gdshaderinc`): thicker, broken into polyps and heads, in pink and violet with patches of
  orange and yellow.
- **Her light, nearly white** on this reef (on blue rock her warm light turned it magenta): her beam's fan and its
  spot, her glow in the water and on the reef.
- **Her light shows the reef** (`reef_show.gdshaderinc`): what her aura and her beam fall on, cut off by the rock
  between, lifts in its own colours. (Under a drawn look it also swaps the drawn skin for the painted one there.)
- **Verification:** `--dbg-sealat=seconds` seals the leak nearest her then, its wave with it.
- **The reef wakes**: each sealed leak sends a wave of her light out across the reef (30 m/s), and behind it the reef
  keeps a glow of its own, the more the more of its leaks are sealed (`reef_wake`, `reef_wave`).

- **Coral gardens** (`LevelView.Gardens`): sea rods, tube sponges, staghorn, sea fans, elkhorn and brain corals packed
  in drifting patches on the walls' tops and ledges, and in a band along their feet at the edge of the open floor.

- **Her beam as light in the water** (`her_beam.gdshader`, `BeamView`): a rave laser's fan. Lines of her light, about
  one a degree, are each cast from her bell on their own (`PlaneWorld.LineReach`) and stopped by what they meet, rock
  or a creature's body. So the beam's end follows the edge of whatever it falls on, a thin glow 0.3 m deep tracing it,
  and the lines nothing stops go on past. Thin bright lines, each flickering on its own beat, run through a soft
  haze, and the water's plankton glints where it crosses them.

## Still to do

- Fish scattering from her beam.
- The god rays and the light pools on the sand tuned against the night-reef reference.
