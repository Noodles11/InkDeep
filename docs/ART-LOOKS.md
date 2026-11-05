# Art looks

The game is drawn in **look 6, felt tip and highlighter**: a minimal illustration, after a reference of ink lines and
flat washes of blue. Looks 1–5 are earlier studies, and 0 the old painted-photographic look; each repaints the reef in
its own surface shading (`reef_art.gdshaderinc`, used by the ground and the free rock) and changes how the depth pass
states distance (`topdown_post.gdshader`). Looks 4 and 5 draw only round dots: the picture plane is the view along the
camera's middle, and off it, on rock seen nearly edge-on (a wall's face low on the screen, a drop's lip), a dot would show
drawn out into a dash or a line; where its stretch on screen passes 1.25 it fades, gone by 1.6 (`art_round`).

## Look 6: felt tip and highlighter (the game's look)

- **The reef held back, so the creatures carry the picture.** Each material one flat marker tone, a whisper of its own
  colour left, held toward the depth's water; the height stepped into wide flat layers 2.6 m apart (the deeper, the
  more of the water's blue); the marker's faint parallel streaks for its only texture. Light in two tones. The sun's
  dapples as a few crisp flat patches. A pale catch of light along the walls' tops.
- **The felt tip on the reef:** a bold wavering navy line where the walls meet her plane, and faint broken strokes along
  each layer's edge up the walls.
- **The drawing over everything drawn before the water pass** (the reef, the creatures, the bosses, the kelp): filled
  in four flat marker tones (stepped in perceived brightness, the steps soft), the colour lifted for what is at her
  plane (×1.25) and held back on the floor below it and the walls above (×0.85); and a felt tip's line round every
  silhouette, where the depth breaks by a third of a metre or more between a pixel and its neighbours, along both sides
  of the break, its width wavering. The line is boldest at her plane and light on the floor's own things. On a cave's
  dark it is a fluorescent teal instead of navy, as a highlighter on black paper. Laid on before the water's veil, so
  what lies deep is softened with it.
- **Depth** by flat layers of water (2.2 m steps), each hazier, barely blurred: no lens blur below her plane.
- **Lights like a highlighter:** her glow on the floor a small flat wash with a slightly ragged edge, in her light's own
  colour paled toward white (not her warm orange tint), faint; her glow in the water and on the reef round her fainter
  and nearer white too; her bubbles' halos stronger; the environment's glow stronger and wider (`ArtLook.Apply`), no
  ambient occlusion, a little more saturation, the vignette heavier for contrast.
- **No god rays in the game** (`ReefLook.DrawRays`): the sun's shafts are drawn only on the title's sea.
- **The paper:** a fine tooth and the faint unevenness of a dried wash over the whole sheet.
- **Kept as they are**, drawn after the water pass over the drawing: Clementine, her light's shots, the magic and
  corruption particles, and the shimmer of hot water over a leak.
- **Code:** `ArtLook` (src/Game/Fx) holds the look in use (project.godot's `reef_art` global, 6); `--look=N` picks
  another for verification, and `--dbg-inkview` shows the felt tip's lines alone (tinted red in look 6).

## The studies

Choose one with `--look=N` (the `reef_art` shader global).

| Look | The reef's surfaces | Light | Depth (`topdown_post.gdshader`) | Ink |
|---|---|---|---|---|
| **1 Gouache layers** | The palette held back (toward its own grey and the depth's water). The height cut into flat painted layers 1.8 m apart, their edges soft and ragged; higher layers a touch richer, deeper ones hazed and cooled. Broad, long brush strokes laid along the slopes instead of the fine photographic grain. The sand's ripples smoothed away. | Three soft-edged bands. The caustics become a few soft dapples. | The water under her plane is stated as flat layers (2.2 m steps), each hazier, barely blurred: no lens blur below her plane. | None. |
| **2 Felt tip and wash** | As 1, paler and greyer: a wash. | As 1. | As 1. | The minimap's hand: a bold wavering felt-tip line along every wall where it meets her plane, with a finer second pass beside it; faint, broken contour strokes up the walls every 1.5 m; bundles of short hatching strokes in the walls' shade near her plane, each bundle at its own angle. |
| **3 Flat concept** | The fewest, biggest shapes: rock and sand as flat tones with only a whisper of their own colour, layered 3.5 m apart, almost no texture. A pale rim catches the light along the walls' tops. | Two tones. No caustics. | As 1. | A single thin line along the walls at her plane. |
| **4 Flat and felt tip** | Drawn flat: rock and sand each one flat tone with a whisper of their own colour, no layers, no texture. | None: one flat light, no shading, no caustics. The stippling does all of it. | No blur below her plane at all, and the water a lighter veil (about half), so the stippling under it reads; the tall tops still blur. | Felt-tip stippling states the depth and the shade: small dots, at most one to each 0.13 m cell of a grid on the picture plane (so they keep one spacing on screen and never swim over the reef), each a little off its cell's middle and its own share of the tone: the darker the tone, the more of them are drawn and the fatter they are (never under a pixel across). Sparse just under her plane, crowding down to about 18 m; faces turned from the sun and the walls' cast shadows the same, the darker the shade. Sunlit tops stay clean. The walls are pocked like a sponge: holes and craters of two sizes (up to about 1 m and 0.45 m across their balls, about half the cells holding one), each a round shaft straight into the wall, its mouth where the wall cuts a ball hidden in the rock, with a fine lip on the side toward the sun (open on the lit side). Inside, what the eye sees down it from the camera's angle: half are shallow dishes (their side, then a pale bottom); a third deep pits (their side stippled darker the further down, going solid black where the eye loses it); one in six go right through the wall (their side, then the pale lit water beyond). |
| **5 Felt tip over the reef's colours** | The game's own: its textures and colours, untouched. | As 4: one flat light, no shading, no caustics. | The game's own: below her plane the reef blurs and sinks into the depth's water (so the stippling down there softens away with it). | As 4: the stippling and the sponge's holes. |

Creatures, kelp, Clementine and the HUD keep the old look in every study: only the reef and the water are repainted.
