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

- **Its palette** (`ReefLook.Showcase`, "The Blue Hollow"): navy water, blue-slate rock, pale blue sand; coral in four
  colours; a cool pale sun, some god rays, caustics; saturation up, a heavy vignette. Painted in look 0
  (`ArtLook.Painted`, set by `TopDownMain.ApplyLook` and the title).
- **Crisp water** (`CameraRig.SetShowcase`): the deeps barely blurred and a thin veil over them, so the reef reads as
  in the references, not through a haze. The water's blue tint and the cool shadow grade are eased so the coral keeps
  its colour (`topdown_post.gdshader`, `reef_showcase`).
- **Coral crust** (`reef_surface.gdshaderinc`): thicker, broken into polyps and heads, in pink and violet with patches of
  orange and yellow.
- **Her light, nearly white** on this reef (on blue rock her warm light turned it magenta): her beam's fan and its
  spot, her glow in the water and on the reef.
- **Her light shows the reef** (`reef_show.gdshaderinc`): what her aura and her beam fall on, cut off by the rock
  between, lifts in its own colours. (Under a drawn look it also swaps the drawn skin for the painted one there.)
- **The reef wakes**: each sealed leak sends a wave of her light out across the reef (30 m/s), and behind it the reef
  keeps a glow of its own, the more the more of its leaks are sealed (`reef_wake`, `reef_wave`).

## Still to do

- Coral as objects, not only a crust: clusters of the flora on the ledges' tops and along the channel's edges, as in
  the canyon reference.
- Her beam as light in the water: shafts with motes lit in them, its edge cut by the rock.
- The wave seen in play and tuned; fish scattering from her beam; the murklings forming out of the dark.
- The god rays and the light pools on the sand tuned against the night-reef reference.
