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
  outside her glow and her beam (and what sealed leaks have woken) the reef keeps 15% of its colour and 20% of its
  light: very dark, almost not to be seen, until she lights it, and her light is plain to see. The player calibrates
  the picture's brightness in the pause menu (`ViewOptions.Brightness`, the environment's brightness: a slider and
  three marks of near-black, set so the faintest is only just to be seen). Her light on the reef falls where the eye sees
  her beam and her glow (`rs_seen`: each point of the reef is lit as the point of her plane on its line of sight is),
  so nothing lights a patch of floor below the beam on the tilted view. Lantern Bay is always awake: all of it shows.
- **The order of the eye** (`CameraRig.SetShowcase`, `topdown_post.gdshader`; the Bible's Sixth-of-a-Second Test): her
  and her light first, then threats and pickups, then walls and ways, the reef's beauty last.
  - *Values drawn together* outside her light (`unlit_flatten`, 35% of the way to a quiet dark, as dim as the rest): the unlit reef
    is a calm ground, not a picture competing with the play.
  - *Nothing of the reef outshines her* (`highlight_knee`): the reef's brightest is rolled off from 0.5, her pool of
    light on pale sand included; she and her light, drawn after the pass, stay the brightest on screen.
  - *Her seat* (`her_seat`): the reef right under her is held up to half darker over about a metre, so her bell stands
    off her own pool of light.
  - *The camera holds its distance* with creatures round her; only a boss's battle draws it in (docs/DIVE.md §3).
- **Fog under her plane** (`below_fog`): the water thickens below the swim height, 80% of the way to the water's grey
  over the first 5 m down, thinner where her light reaches.
- **Crisp water** (`CameraRig.SetShowcase`): the deeps barely blurred and a thin veil over them, so the reef reads as
  in the references, not through a haze. The water's blue tint and the cool shadow grade are eased so the coral keeps
  its colour (`topdown_post.gdshader`, `reef_showcase`).
- **Coral crust** (`reef_surface.gdshaderinc`): thicker, broken into polyps and heads, in pink and violet with patches of
  orange and yellow.
- **Her light is cyan** (`HerColor.DefaultLight`; her jelly's look may set another), only a little paled on the reef,
  so it stands out against the ink-drawn reef's blue: her beam's fan and its spot, her light in the water and on the
  reef, her light bubbles (their hearts whiter). Her body stays tangerine.
- **Her light shows the reef** (`reef_show.gdshaderinc`): what her aura and her beam fall on, cut off by the rock
  between, lifts in its own colours. (Under a drawn look it also swaps the drawn skin for the painted one there.)
- **Verification:** `--dbg-sealat=seconds` seals the leak nearest her then, its wave with it.
- **The reef wakes**: each sealed leak sends a wave of her light out across the reef (30 m/s), and behind it the reef
  keeps a glow of its own, the more the more of its leaks are sealed (`reef_wake`, `reef_wave`): half its colour back
  while some still pour, **the whole painting once every leak is sealed** (`reef_show`). The reef's beauty is the
  reward of cleansing it.
  The wave shows the hidden murklings it passes over as its front touches them (within 1.5 m of it), all of each,
  fading out over 1.2 s after (`BlightView.Wave`, `murkling`'s `wave_seen`).

- **Each layer's scenery** (`LevelView.Gardens`, `LevelView.Landmarks`, `FloraMeshes`; `--dbg-theme=N` dresses every
  map as layer N's):
  - *Sunlit Shallows:* the coral gardens below.
  - *Kelp Jungle:* giant kelp at the walls' feet, and forests of it grown up out of the void from the floor far below.
  - *Sunken Galleon:* broken planks among the gardens, and now and then a wreck's hull in the sand: keel, ribs, a strake
    of planking, a fallen mast.
  - *Coral Carnival:* gardens packed denser, with tube sponges.
  - *Twilight Trench:* sea fans and sea rods, and clusters of vent chimneys, their lips rimmed with an ember of mineral.
  - *The Abyss:* few living things, lone vertebrae, and now and then a whale fall: a spine, ribs arching over the sand,
    its skull.
  The landmarks (wrecks, vents, whale falls) stand on a coarse lattice in the open floor, clear of the walls.
- **The reef answers her light** (`LightMemory`, `topdown_post`'s `glow_memory`):
  - A map round her (64 m, half-metre cells) keeps where her glow and her beam have lately fallen, cut off by rock and
    by inkseed stains. It fades over 5 s once her light has gone on.
  - Where it holds, drifts of fluorescent life on the reef glow in colours of their own: green, orange, magenta and
    cyan. They are dotted like polyps, a little stronger on corals and gardens, and stronger the deeper the layer.
  - So she leaves a fading trail of colour behind her in the grey. While her light is still on a spot the glow is faint
    there, so the reef's own colours show.
- **Coral gardens** (`LevelView.Gardens`): sea rods, staghorn, sea fans and elkhorn (no sponges) packed in drifting
  patches on the walls' tops and ledges, and in a band along their feet at the edge of the open floor; like the
  seagrass, only half the spots are dressed (`LevelView.DecorShare`). No round corals or barrel sponges anywhere: cut
  by the cutout they showed as hollow pots.

- **Wakes** (`WakeField`, `topdown_post`'s `wake_map`): everything that swims (Clementine, every creature, the reef's
  fish, her shells in flight, a boss) leaves a V of disturbed water behind it, as a motorboat does seen from above, drawn
  as the game's moving water is: the picture bent and boiling along each arm, a faint patchy shimmer of light on it, no
  lines. Each wake is its last 0.55 s of way: from each point of it an arm out to each side as far as the water has
  spread (the Kelvin wake's 19.5°), fading as it ages; so the faster it goes, the longer the arms, and stopped, it leaves
  nothing within a moment. Slower than 0.6 m/s it leaves none; it is strongest at 6 m/s. The 64 nearest her, in a map
  64 m round her baked each frame. Lit or not: an unseen murkling's wake gives it away.

- **Her beam as bright as it burns** (`BeamView.BrightOf`): her own beam is drawn by her light's damage against a plain
  one's (her damage stat, her pearls' boosts): twice the damage about 1.6× as bright, three times about 2×, between
  0.6× and 2.5×. Only the drawing: what it lights for the game is as ever.

- **Her beam as a flashlight's, lit as light is** (`BeamView`, `BeamShadow`, `her_beam_volume.gdshader`): each frame
  a grid of 64 × 64 rays is cast in three dimensions from her bell over the whole of her round cone (±22°, a little
  past its rim), each going on until something stops it: the seabed under it, rock rising into it (rock across her
  plane stands at least 1.4 m above it, so it stops the light there as the sim's lines do), or a creature's body, a
  sphere of its own size at her plane (`PlaneWorld.LightBodies`; none while her shots pierce). The rays' reach is a
  texture. The water in the cone glows by as much of her light as gets to each point of it, marched along each pixel's
  line of sight (28 steps, jittered): brightest down its middle and soft to nothing at its rim, dimming with the
  distance (spreading, and taken up by the water), a little brighter seen against the way it goes (forward
  scattering), and dark where the ray to it was stopped first. So every creature in it casts its own round shadow down
  the beam, as wide as its body, and the light flows on round it; the beam ends where it lands, softly, the march
  stopping at the reef in front of it (from the depth). Tiny motes of plankton sparkle in it only where the light
  reaches them. Lines of her light across her plane (`PlaneWorld.LineReach`, about one a degree) are cast as well,
  for how far it reaches and for prism stones' colours; nothing is drawn along them. Her own beam's light on the reef
  (`rs_beam`) is shadowed by the creatures alike (`her_beam_shadow`, the rays across her plane cast straight down onto
  it), and her beam's spotlight lights the reef it falls on, its rim and its shadows soft (the creatures cast them).
  `--dbg-shadow` holds her beam along the most open way from her with two murklings held still in it.

## Still to do

- Fish scattering from her beam.
- The god rays and the light pools on the sand tuned against the night-reef reference.
